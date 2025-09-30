using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Scalerize.InfiniteGpu.Desktop.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.System;
using WinRT.Interop;

namespace Scalerize.InfiniteGpu.Desktop
{
    public sealed partial class MainWindow : Window
    {
#if DEBUG
        private static readonly Uri FrontendUri = new("http://localhost:5173");
#else
        private static readonly Uri FrontendUri = new("https://backend.infinite-gpu.scalerize.fr");
#endif
        private readonly bool _isDevelopment;
        private readonly OnnxRuntimeService _onnxRuntimeService;
        private readonly HardwareMetricsService _hardwareMetricsService;
        private readonly WebViewCommunicationService _webViewBridge;
        private readonly BackgroundWorkService _backgroundWorkService;
        private readonly DeviceIdentifierService _deviceIdentifierService;
        private bool _runtimeReady;
        private bool _bridgeReady;
        private bool _bridgeHandlersRegistered;
        private AppWindow? _appWindow;
        private bool _allowClose;
        private TaskCompletionSource<bool>? _shutdownCompletionSource;
        private Window? _window;
        private bool _isShuttingDown;
        private bool _serviceStopped;

        public MainWindow(
            OnnxRuntimeService onnxRuntimeService,
            HardwareMetricsService hardwareMetricsService,
            WebViewCommunicationService webViewCommunicationService,
            BackgroundWorkService backgroundWorkService,
            DeviceIdentifierService deviceIdentifierService)
        {
            _onnxRuntimeService = onnxRuntimeService ?? throw new ArgumentNullException(nameof(onnxRuntimeService));
            _hardwareMetricsService = hardwareMetricsService ?? throw new ArgumentNullException(nameof(hardwareMetricsService));
            _webViewBridge = webViewCommunicationService ?? throw new ArgumentNullException(nameof(webViewCommunicationService));
            _backgroundWorkService = backgroundWorkService ?? throw new ArgumentNullException(nameof(backgroundWorkService));
            _deviceIdentifierService = deviceIdentifierService ?? throw new ArgumentNullException(nameof(deviceIdentifierService));

            InitializeComponent();
            _isDevelopment = DetermineIsDevelopment();
            PrepareAppAsync();
        }


        private async void PrepareAppAsync()
        {
            ConfigureTitleBar();
            SetLoadingState(
                true,
                "Initializing local inference runtime...",
                "Downloading certified execution providers. This may take a moment on first launch."
            );

            _runtimeReady = await _onnxRuntimeService.InitializeOnnxRuntimeAsync();

            if (!_runtimeReady)
            {
                SetLoadingState(
                    true,
                    "Continuing without full acceleration",
                    "We couldn't reach the execution provider catalog. Check your network connection. The app will continue with limited features."
                );

                await Task.Delay(2400);
            }

            SetLoadingState(false);
            await NotifyFrontendRuntimeStateAsync();

            ConfigureDevelopmentAccelerators();
            InitializeWebView();

            ConfigureTrayMenu();
            _backgroundWorkService.Start();
        }

        private async void InitializeWebView()
        {
            AppWebView.CoreWebView2Initialized += OnCoreWebView2InitializationCompleted;
            AppWebView.NavigationCompleted += OnNavigationCompleted;
            AppWebView.NavigationStarting += OnNavigationStarting;
            await AppWebView.EnsureCoreWebView2Async();
        }

        private async void OnCoreWebView2InitializationCompleted(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            var core = sender.CoreWebView2;
            core.Settings.AreDevToolsEnabled = _isDevelopment;
            core.Settings.AreDefaultContextMenusEnabled = _isDevelopment;

            if (!_isDevelopment)
            {
                const string disableContextMenuScript = @"
                    window.addEventListener('contextmenu', (event) => event.preventDefault(), true);
                ";
                _ = core.AddScriptToExecuteOnDocumentCreatedAsync(disableContextMenuScript);
            }
            else
            {
                core.Settings.AreDefaultScriptDialogsEnabled = true;
            }

            // Register navigation failed event
            core.NavigationCompleted += OnCoreNavigationCompleted;

            await ConfigureBridgeAsync();

            NavigateToFrontend();
        }

        private void SetLoadingState(bool isVisible, string? headline = null, string? subtitle = null)
        {
            if (LoadingOverlay is null)
            {
                return;
            }

            LoadingOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

            if (LoadingStatusText is not null && !string.IsNullOrWhiteSpace(headline))
            {
                LoadingStatusText.Text = headline;
            }

            if (LoadingSubtitleText is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(subtitle))
            {
                LoadingSubtitleText.Visibility = Visibility.Collapsed;
                LoadingSubtitleText.Text = string.Empty;
            }
            else
            {
                LoadingSubtitleText.Visibility = Visibility.Visible;
                LoadingSubtitleText.Text = subtitle;
            }
        }

        private void ConfigureTitleBar()
        {
            ExtendsContentIntoTitleBar = true;

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            if (_appWindow is null)
            {
                _appWindow = AppWindow.GetFromWindowId(windowId);
                _appWindow.Closing += OnAppWindowClosing;
            }

            var appWindow = _appWindow;

            if (!AppWindowTitleBar.IsCustomizationSupported() || appWindow is null)
            {
                return;
            }

            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        private void NavigateToFrontend()
        {
            if (AppWebView.Source != FrontendUri)
            {
                AppWebView.Source = FrontendUri;
            }
        }

        private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            // Track navigation attempts and show loading overlay
            Debug.WriteLine($"Navigation starting to: {args.Uri}");
            
            // Show loading overlay when navigating to the frontend
            if (args.Uri.Contains("localhost:5173"))
            {
                SetLoadingState(true, "Loading...", "Connecting to the application interface.");
            }
        }

        private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            // Hide loading overlay when navigation completes successfully
            if (args.IsSuccess)
            {
                SetLoadingState(false);
            }
            
            _ = NotifyFrontendRuntimeStateAsync();
        }

        private void OnCoreNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                Debug.WriteLine($"Navigation failed with error: {args.WebErrorStatus}");
                ShowErrorPage();
            }
        }

        private void ShowErrorPage()
        {
            try
            {
                var errorPagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "ErrorPage.html");
                
                if (File.Exists(errorPagePath))
                {
                    var errorPageUri = new Uri($"file:///{errorPagePath.Replace("\\", "/")}");
                    AppWebView.Source = errorPageUri;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show error page: {ex}");
            }
        }

        private async Task ConfigureBridgeAsync()
        {
            try
            {
                await _webViewBridge.AttachAsync(AppWebView);
                RegisterBridgeHandlers();
                _bridgeReady = true;
                await NotifyFrontendRuntimeStateAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to configure WebView bridge: {ex}");
            }
        }

        private void RegisterBridgeHandlers()
        {
            if (_bridgeHandlersRegistered)
            {
                return;
            }

            _webViewBridge.RegisterEventHandler("frontend:ready", OnFrontendReadyAsync);
            _webViewBridge.RegisterEventHandler("frontend:requestRuntimeState", OnFrontendRequestRuntimeStateAsync);
            _webViewBridge.RegisterMethod("auth:setToken", OnAuthSentToken);
            _webViewBridge.RegisterMethod("runtime:getState", HandleRuntimeGetStateAsync);
            _webViewBridge.RegisterMethod("app:getVersion", HandleAppGetVersionAsync);
            _webViewBridge.RegisterMethod("hardware:getMetrics", HandleHardwareGetMetricsAsync);
            _webViewBridge.RegisterMethod("device:getIdentifier", HandleDeviceGetIdentifierAsync);
            _webViewBridge.RegisterEventHandler("error:retry", OnErrorRetryAsync);
            _webViewBridge.RegisterEventHandler("error:close", OnErrorCloseAsync);

            _bridgeHandlersRegistered = true;
        }

        private Task<JsonNode> OnAuthSentToken(JsonNode? payload)
        {
            string authToken = null;
            if (!(payload is null || payload["token"] is null))
            {
                authToken = payload["token"]?.GetValue<string>();
            }
            _backgroundWorkService.UpdateAuthToken(authToken);
            return Task.FromResult(payload);
        }

        private Task<JsonNode?> HandleRuntimeGetStateAsync(JsonNode? payload)
        {
            var result = new JsonObject
            {
                ["isDevelopment"] = _isDevelopment,
                ["runtimeReady"] = _runtimeReady,
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            return Task.FromResult<JsonNode?>(result);
        }

        private Task<JsonNode?> HandleAppGetVersionAsync(JsonNode? payload)
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var version = entryAssembly?.GetName().Version?.ToString() ?? "0.0.0";

            var result = new JsonObject
            {
                ["version"] = version
            };

            return Task.FromResult<JsonNode?>(result);
        }

        private async Task<JsonNode?> HandleHardwareGetMetricsAsync(JsonNode? payload)
        {
            var snapshot = await _hardwareMetricsService.CollectAsync();

            var result = new JsonObject
            {
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["cpuCores"] = snapshot.CpuCores,
                ["cpuFrequencyGhz"] = snapshot.CpuFrequencyGhz,
                ["videoMemoryAvailable"] = snapshot.GpuTotalRam,
                ["gpuName"] = snapshot.GpuName,
                ["gpuVendor"] = snapshot.GpuVendor,
                ["memoryTotalGb"] = snapshot.MemoryTotalGb,
                ["memoryAvailableGb"] = snapshot.MemoryAvailableGb,
                ["networkDownlinkMbps"] = snapshot.NetworkDownlinkMbps,
                ["networkLatencyMs"] = snapshot.NetworkLatencyMs,
                ["storageFreeGb"] = snapshot.StorageFreeGb,
                ["storageTotalGb"] = snapshot.StorageTotalGb
            };

            return result;
        }

        private async Task<JsonNode?> HandleDeviceGetIdentifierAsync(JsonNode? payload)
        {
            var identifier = await _deviceIdentifierService.GetOrCreateIdentifierAsync();

            var result = new JsonObject
            {
                ["identifier"] = identifier
            };

            return result;
        }

        private Task OnErrorRetryAsync(JsonNode? payload)
        {
            NavigateToFrontend();
            return Task.CompletedTask;
        }

        private async Task OnErrorCloseAsync(JsonNode? payload)
        {
            _allowClose = true;
            DispatcherQueue.TryEnqueue(() => OnTrayIconQuitClick(null, null));
        }

        private Task OnFrontendReadyAsync(JsonNode? payload)
        {
            DispatcherQueue.TryEnqueue(() => SetLoadingState(false));
            return NotifyFrontendRuntimeStateAsync();
        }

        private Task OnFrontendRequestRuntimeStateAsync(JsonNode? payload)
        {
            return NotifyFrontendRuntimeStateAsync();
        }

        private Task NotifyFrontendRuntimeStateAsync()
        {
            if (!_bridgeReady)
            {
                return Task.CompletedTask;
            }

            try
            {
                return _webViewBridge.DispatchEventAsync(
                    "desktop:runtimeState",
                    new
                    {
                        isDevelopment = _isDevelopment,
                        runtimeReady = _runtimeReady
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to dispatch runtime state message: {ex}");
                return Task.CompletedTask;
            }
        }

        private void ConfigureDevelopmentAccelerators()
        {
            if (!_isDevelopment)
            {
                return;
            }

            var f12Accelerator = new KeyboardAccelerator
            {
                Key = VirtualKey.F12
            };
            f12Accelerator.Invoked += OnDevToolsAcceleratorInvoked;
            AppWebView.KeyboardAccelerators.Add(f12Accelerator);

            var inspectAccelerator = new KeyboardAccelerator
            {
                Key = VirtualKey.I,
                Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
            };
            inspectAccelerator.Invoked += OnDevToolsAcceleratorInvoked;
            AppWebView.KeyboardAccelerators.Add(inspectAccelerator);
        }

        private void OnDevToolsAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isDevelopment || AppWebView.CoreWebView2 is not CoreWebView2 coreWebView2)
            {
                return;
            }

            coreWebView2.OpenDevToolsWindow();
            args.Handled = true;
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (_allowClose)
            {
                sender.Closing -= OnAppWindowClosing;
                return;
            }

            args.Cancel = true;

            DispatcherQueue.TryEnqueue(() =>
            {
                sender.Hide();
            });
        }

        public void RestoreFromTray()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _appWindow?.Show();
                Activate();
            });
        }

        public Task PrepareForShutdownAsync()
        {
            if (_shutdownCompletionSource is not null)
            {
                return _shutdownCompletionSource.Task;
            }

            _shutdownCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            DispatcherQueue.TryEnqueue(() =>
            {
                _allowClose = true;
                Close();
            });

            return _shutdownCompletionSource.Task;
        }

        private static bool DetermineIsDevelopment()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        private void ConfigureTrayMenu()
        {
            if (!App.Current.Resources.TryGetValue("TrayMenu", out var menuResource) || menuResource is not MenuFlyout trayMenu)
            {
                return;
            }

            foreach (var menuItem in trayMenu.Items)
            {
                switch (menuItem)
                {
                    case MenuFlyoutItem { Tag: "TrayMenuOpen" } openItem:
                        openItem.Click -= OnTrayIconOpenClick;
                        openItem.Click += OnTrayIconOpenClick;
                        break;
                    case MenuFlyoutItem { Tag: "TrayMenuQuit" } quitItem:
                        quitItem.Click -= OnTrayIconQuitClick;
                        quitItem.Click += OnTrayIconQuitClick;
                        break;
                }
            }

            if (AppTrayIcon is not null && AppTrayIcon.ContextFlyout is null)
            {
                AppTrayIcon.ContextFlyout = trayMenu;
            }
        }

        private void DisposeTrayIcon()
        {
            if (AppTrayIcon is null)
            {
                return;
            }

            AppTrayIcon.Dispose();
            AppTrayIcon = null;
        }

        private void OnTrayIconOpenClick(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private async void OnTrayIconQuitClick(object sender, RoutedEventArgs e)
        {
            if (_isShuttingDown)
            {
                return;
            }


            _isShuttingDown = true;

            if (_window is MainWindow mainWindow)
            {
                await mainWindow.PrepareForShutdownAsync().ConfigureAwait(true);
            }

            AppWebView.CoreWebView2Initialized -= OnCoreWebView2InitializationCompleted;
            AppWebView.NavigationCompleted -= OnNavigationCompleted;
            AppWebView.NavigationStarting -= OnNavigationStarting;
            
            if (AppWebView.CoreWebView2 is not null)
            {
                AppWebView.CoreWebView2.NavigationCompleted -= OnCoreNavigationCompleted;
            }
            
            if (_appWindow is not null)
            {
                _appWindow.Closing -= OnAppWindowClosing;
            }

            _bridgeReady = false;
            _bridgeHandlersRegistered = false;
            _webViewBridge.Dispose();

            _shutdownCompletionSource?.TrySetResult(true);
            _shutdownCompletionSource = null;

            await StopBackgroundServiceAsync().ConfigureAwait(true);


            DisposeTrayIcon();

            App.Current.Exit();
        }


        private async Task StopBackgroundServiceAsync()
        {
            if (_serviceStopped)
            {
                return;
            }

            await _backgroundWorkService.StopAsync().ConfigureAwait(true);
            _serviceStopped = true;
        }
    }
}
