using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Scalerize.InfiniteGpu.Desktop.Services;

namespace Scalerize.InfiniteGpu.Desktop
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private IServiceProvider? _serviceProvider;
#if DEBUG
        public static readonly Uri BackendBaseUri = new("http://localhost:5116/");
#else
        public static readonly Uri BackendBaseUri = new("https://backend.infinite-gpu.scalerize.fr/")
#endif
        private static Mutex? _singleInstanceMutex;
        private const string AppMutexName = "Scalerize.InfiniteGpu.Desktop.SingleInstance";

        public static IServiceProvider Services =>
            (Current as App)?._serviceProvider ?? throw new InvalidOperationException("The service provider has not been initialized.");

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            // Check for single instance
            _singleInstanceMutex = new Mutex(true, AppMutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running - exit immediately
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _serviceProvider = ConfigureServices();
            _window = _serviceProvider.GetRequiredService<MainWindow>();
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_window is not null)
            {
                _window.Closed -= OnWindowClosed;
            }

            if (_serviceProvider is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
                _serviceProvider = null;
            }

            // Release the mutex when the window closes
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(_ => CreateHttpClient());
            services.AddSingleton<OnnxRuntimeService>();
            services.AddSingleton<HardwareMetricsService>();
            services.AddSingleton<DeviceIdentifierService>();
            services.AddSingleton<WebViewCommunicationService>();
            services.AddSingleton<InputParsingService>();
            services.AddSingleton<OutputParsingService>();
            services.AddSingleton<BackgroundWorkService>();
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            };

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(2),
                BaseAddress = BackendBaseUri
            };
        }
    }
}