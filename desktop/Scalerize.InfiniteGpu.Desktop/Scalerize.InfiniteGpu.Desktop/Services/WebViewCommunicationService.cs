using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    public sealed class WebViewCommunicationService : IDisposable
    {
        private readonly Dictionary<string, List<Func<JsonNode?, Task>>> _eventHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Func<JsonNode?, Task<JsonNode?>>> _methodHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Func<JsonNode?, IReadOnlyList<object>, Task<JsonNode?>>> _methodHandlersWithObjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly object _syncRoot = new();
        private readonly DispatcherQueue _dispatcherQueue;

        private CoreWebView2? _coreWebView2;
        private string? _bootstrapScriptId;
        private bool _disposed;

        public WebViewCommunicationService()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("WebViewCommunicationService must be created on the UI thread.");
        }

        public async Task AttachAsync(WebView2 webView)
        {
            ArgumentNullException.ThrowIfNull(webView);
            EnsureNotDisposed();

            await RunOnUIThreadAsync(async () =>
            {
                EnsureNotDisposed();

                if (webView.CoreWebView2 is null)
                {
                    await webView.EnsureCoreWebView2Async();
                }

                var core = webView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 core is not available.");

                var needsBootstrap = false;

                lock (_syncRoot)
                {
                    if (ReferenceEquals(_coreWebView2, core))
                    {
                        return;
                    }

                    DetachCoreUnsafe();
                    _coreWebView2 = core;
                    _coreWebView2.WebMessageReceived += OnWebMessageReceived;
                    needsBootstrap = true;
                }

                if (!needsBootstrap)
                {
                    return;
                }

                var scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(BridgeBootstrapScript);

                lock (_syncRoot)
                {
                    _bootstrapScriptId = scriptId;
                }
            }).ConfigureAwait(false);
        }

        public void RegisterEventHandler(string eventName, Func<JsonNode?, Task> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
            ArgumentNullException.ThrowIfNull(handler);
            EnsureNotDisposed();

            lock (_syncRoot)
            {
                if (!_eventHandlers.TryGetValue(eventName, out var handlers))
                {
                    handlers = new List<Func<JsonNode?, Task>>();
                    _eventHandlers[eventName] = handlers;
                }

                handlers.Add(handler);
            }
        }

        public void RegisterMethod(string methodName, Func<JsonNode?, Task<JsonNode?>> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(handler);
            EnsureNotDisposed();

            lock (_syncRoot)
            {
                _methodHandlers[methodName] = handler;
            }
        }

        public void RegisterMethodWithObjects(string methodName, Func<JsonNode?, IReadOnlyList<object>, Task<JsonNode?>> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(handler);
            EnsureNotDisposed();

            lock (_syncRoot)
            {
                _methodHandlersWithObjects[methodName] = handler;
            }
        }

        public Task DispatchEventAsync(string eventName, object? payload = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
            EnsureNotDisposed();

            var message = new BridgeOutboundMessage("event-dispatch", eventName, payload);
            var json = JsonSerializer.Serialize(message, _serializerOptions);

            return RunOnUIThreadAsync(() =>
            {
                EnsureNotDisposed();

                var core = _coreWebView2 ?? throw new InvalidOperationException("WebView2 core is not attached.");
                core.PostWebMessageAsJson(json);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var requiresDetach = false;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                requiresDetach = _coreWebView2 is not null;
                _disposed = true;
            }

            if (!requiresDetach)
            {
                return;
            }

            if (_dispatcherQueue.HasThreadAccess)
            {
                lock (_syncRoot)
                {
                    DetachCoreUnsafe();
                }

                return;
            }

            RunOnUIThreadAsync(() =>
            {
                lock (_syncRoot)
                {
                    DetachCoreUnsafe();
                }
            }).GetAwaiter().GetResult();
        }

        private void DetachCoreUnsafe()
        {
            if (_coreWebView2 is not null)
            {
                _coreWebView2.WebMessageReceived -= OnWebMessageReceived;

                if (!string.IsNullOrEmpty(_bootstrapScriptId))
                {
                    try
                    {
                        _coreWebView2.RemoveScriptToExecuteOnDocumentCreated(_bootstrapScriptId);
                    }
                    catch
                    {
                        // Ignored – removing scripts may throw if the core is already disposed.
                    }

                    _bootstrapScriptId = null;
                }
            }

            _coreWebView2 = null;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebViewCommunicationService));
            }
        }

        private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            JsonNode? root;

            try
            {
                root = JsonNode.Parse(args.WebMessageAsJson);
            }
            catch (JsonException)
            {
                return;
            }

            if (root is null)
            {
                return;
            }

            var messageType = root["type"]?.GetValue<string>();

            // Get additional objects if present
            IReadOnlyList<object>? additionalObjects = null;
            if (args.AdditionalObjects?.Count > 0)
            {
                additionalObjects = args.AdditionalObjects;
            }

            switch (messageType)
            {
                case "event":
                    await HandleIncomingEventAsync(root).ConfigureAwait(false);
                    break;
                case "method":
                    await HandleMethodInvocationAsync(root, additionalObjects).ConfigureAwait(false);
                    break;
            }
        }

        private Task HandleIncomingEventAsync(JsonNode message)
        {
            var eventName = message["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return Task.CompletedTask;
            }

            List<Func<JsonNode?, Task>>? handlers;
            lock (_syncRoot)
            {
                if (!_eventHandlers.TryGetValue(eventName, out handlers) || handlers.Count == 0)
                {
                    return Task.CompletedTask;
                }

                handlers = handlers.ToList();
            }

            var payload = message["payload"];
            return InvokeHandlersSequentiallyAsync(handlers, payload);
        }

        private static async Task InvokeHandlersSequentiallyAsync(IEnumerable<Func<JsonNode?, Task>> handlers, JsonNode? payload)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await handler(payload).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow handler exceptions to avoid disrupting other subscribers.
                }
            }
        }

        private async Task HandleMethodInvocationAsync(JsonNode message, IReadOnlyList<object>? additionalObjects = null)
        {
            var methodName = message["name"]?.GetValue<string>();
            var requestId = message["requestId"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(methodName) || string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            // Check for handlers that accept additional objects first
            Func<JsonNode?, IReadOnlyList<object>, Task<JsonNode?>>? handlerWithObjects = null;
            Func<JsonNode?, Task<JsonNode?>>? handler = null;
            
            lock (_syncRoot)
            {
                _methodHandlersWithObjects.TryGetValue(methodName, out handlerWithObjects);
                if (handlerWithObjects is null)
                {
                    _methodHandlers.TryGetValue(methodName, out handler);
                }
            }

            if (handlerWithObjects is null && handler is null)
            {
                await SendMethodErrorAsync(requestId, methodName, "MethodNotFound", $"No method registered with name '{methodName}'.").ConfigureAwait(false);
                return;
            }

            try
            {
                var payload = message["payload"];
                JsonNode? result;
                
                if (handlerWithObjects is not null)
                {
                    result = await handlerWithObjects(payload, additionalObjects ?? Array.Empty<object>()).ConfigureAwait(false);
                }
                else
                {
                    result = await handler!(payload).ConfigureAwait(false);
                }
                
                await SendMethodResponseAsync(requestId, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await SendMethodErrorAsync(requestId, methodName, ex.GetType().Name, ex.Message).ConfigureAwait(false);
            }
        }

        private Task SendMethodResponseAsync(string requestId, JsonNode? result)
        {
            var response = new BridgeMethodResponse("methodResponse", requestId, "success", result, null, null);
            var json = JsonSerializer.Serialize(response, _serializerOptions);

            return RunOnUIThreadAsync(() =>
            {
                var core = _coreWebView2;
                if (core is null)
                {
                    return;
                }

                core.PostWebMessageAsJson(json);
            });
        }

        private Task SendMethodErrorAsync(string requestId, string methodName, string? errorName, string? errorMessage)
        {
            var response = new BridgeMethodResponse(
                "methodResponse",
                requestId,
                "error",
                null,
                errorName ?? "WebViewBridgeError",
                errorMessage ?? $"Method '{methodName}' failed.");

            var json = JsonSerializer.Serialize(response, _serializerOptions);

            return RunOnUIThreadAsync(() =>
            {
                var core = _coreWebView2;
                if (core is null)
                {
                    return;
                }

                core.PostWebMessageAsJson(json);
            });
        }

        private Task RunOnUIThreadAsync(Action action)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                tcs.TrySetException(new InvalidOperationException("Failed to enqueue work on the UI thread."));
            }

            return tcs.Task;
        }

        private Task RunOnUIThreadAsync(Func<Task> asyncAction)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                return asyncAction();
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await asyncAction().ConfigureAwait(false);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                tcs.TrySetException(new InvalidOperationException("Failed to enqueue work on the UI thread."));
            }

            return tcs.Task;
        }

        private const string BridgeBootstrapScript = """
(() => {
    if (window.DesktopBridge || !window.chrome || !window.chrome.webview) {
        return;
    }

    const pendingRequests = new Map();
    let requestCounter = 0;

    function toRequestId() {
        requestCounter += 1;
        return requestCounter.toString();
    }

    function addHandler(eventName, handler) {
        if (typeof handler !== "function") {
            throw new Error("Handler must be a function.");
        }

        const wrapped = (event) => handler(event.detail);
        handler.__desktopBridgeWrapper = wrapped;
        document.addEventListener(`desktop-bridge:${eventName}`, wrapped);
    }

    function removeHandler(eventName, handler) {
        const wrapped = handler?.__desktopBridgeWrapper ?? handler;
        if (typeof wrapped !== "function") {
            return;
        }

        document.removeEventListener(`desktop-bridge:${eventName}`, wrapped);
    }

    const bridge = {
        invoke(methodName, payload) {
            if (typeof methodName !== "string" || !methodName) {
                return Promise.reject(new Error("Method name must be a non-empty string."));
            }

            const requestId = toRequestId();

            const message = {
                type: "method",
                name: methodName,
                requestId,
                payload: payload ?? null
            };

            window.chrome.webview.postMessage(message);

            return new Promise((resolve, reject) => {
                pendingRequests.set(requestId, { resolve, reject });

                setTimeout(() => {
                    if (pendingRequests.has(requestId)) {
                        pendingRequests.delete(requestId);
                        reject(new Error(`Desktop bridge method "${methodName}" timed out.`));
                    }
                }, 120000);
            });
        },
        emit(eventName, payload) {
            if (typeof eventName !== "string" || !eventName) {
                throw new Error("Event name must be a non-empty string.");
            }

            window.chrome.webview.postMessage({
                type: "event",
                name: eventName,
                payload: payload ?? null
            });
        },
        on(eventName, handler) {
            addHandler(eventName, handler);
        },
        off(eventName, handler) {
            removeHandler(eventName, handler);
        }
    };

    window.DesktopBridge = bridge;

    window.chrome.webview.addEventListener("message", (event) => {
        const message = event.data;
        if (!message || typeof message !== "object") {
            return;
        }

        if (message.type === "event-dispatch") {
            const customEvent = new CustomEvent(`desktop-bridge:${message.name}`, {
                detail: message.payload ?? null
            });
            document.dispatchEvent(customEvent);
            return;
        }

        if (message.type === "methodResponse") {
            const { requestId } = message;
            if (!requestId || !pendingRequests.has(requestId)) {
                return;
            }

            const entry = pendingRequests.get(requestId);
            pendingRequests.delete(requestId);

            if (message.status === "success") {
                entry.resolve(message.payload ?? null);
            } else {
                const error = new Error(message.errorMessage ?? "Desktop bridge method failed.");
                error.name = message.errorName ?? "DesktopBridgeError";
                entry.reject(error);
            }
        }
    });
})();
""";

        private sealed record BridgeOutboundMessage(string Type, string Name, object? Payload);

        private sealed record BridgeMethodResponse(
            string Type,
            string RequestId,
            string Status,
            JsonNode? Payload,
            string? ErrorName,
            string? ErrorMessage);
    }
}