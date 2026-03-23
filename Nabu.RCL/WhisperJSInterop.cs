using Microsoft.JSInterop;

namespace Nabu.RCL
{
    /// <summary>UI state emitted from the Whisper JavaScript layer (overlay visibility, status text, etc.).</summary>
    // ReSharper disable once InconsistentNaming
    public class WhisperUIState
    {
        public bool ShowOverlay { get; set; }
        public string OverlayStatus { get; set; } = string.Empty;
        public string OverlayText { get; set; } = string.Empty;
        public bool IsPreviewText { get; set; }
        public bool ShowSendButton { get; set; }
        public bool IsCancelling { get; set; }
        public bool IsProcessing { get; set; }
    }

    /// <summary>JavaScript interop bridge for the Whisper voice transcription app (app.js).</summary>
    public class WhisperJsInterop : IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private IJSObjectReference? _module;
        private DotNetObjectReference<WhisperJsInterop> _objRef = null!;

        /// <summary>Fired when the Whisper state machine transitions (e.g. IDLE → LISTENING).</summary>
        public event Action<string>? OnStateChanged;
        /// <summary>Fired when live transcription preview text is available.</summary>
        public event Action<string>? OnLivePreview;
        /// <summary>Fired when a final transcription is ready to forward to handlers.</summary>
        public event Action<string>? OnTranscriptionFinal;
        /// <summary>Fired when the active backend changes (Server / Browser / …).</summary>
        public event Action<string>? OnBackendChanged;
        /// <summary>Fired when a status message is emitted (e.g. "Connecting to server…").</summary>
        public event Action<string>? OnStatusMessage;
        /// <summary>Fired when overlay UI state changes (visibility, status, send button, etc.).</summary>
        // ReSharper disable once InconsistentNaming
        public event Action<WhisperUIState>? OnUIStateChanged;

        /// <summary>Creates a new interop instance for the given JS runtime.</summary>
        public WhisperJsInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>Loads the app.js module and registers the .NET callback. Call once before other methods.</summary>
        public async Task InitializeAsync()
        {
            if (_module != null) return;

            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/_content/Nabu.RCL/app.js");
            _objRef = DotNetObjectReference.Create(this);

            await _module.InvokeVoidAsync("registerDotNetCallback", _objRef);
        }

        /// <summary>Initializes the Whisper app (heartbeat, overlay, state machine).</summary>
        public async Task InitAppAsync() => await _module.InvokeVoidAsync("initApp");

        /// <summary>Loads the browser-side Whisper model (WebGPU/WASM).</summary>
        public async Task LoadModelAsync() => await _module.InvokeVoidAsync("loadModel");

        /// <summary>Starts live transcription with the given language.</summary>
        public async Task StartLiveTranscriptionAsync(string language = "english") => await _module.InvokeVoidAsync("startLiveTranscription", new { language });

        /// <summary>Registers for user gesture (click/key/touch) and starts transcription with the given language.</summary>
        public async Task StartOnUserGestureAsync(string language = "english") => await _module.InvokeVoidAsync("startOnUserGesture", new { language });

        /// <summary>Switches from browser to server backend if available.</summary>
        public async Task SwitchToServerAsync() => await _module.InvokeVoidAsync("switchToServer");

        /// <summary>Stops live transcription and releases audio resources.</summary>
        public async Task StopLiveAsync() => await _module.InvokeVoidAsync("stopLive");

        /// <summary>Sets the transcription language and sends it to the server if connected.</summary>
        public async Task SetLanguageAsync(string language) => await _module.InvokeVoidAsync("setLanguage", language);

        /// <summary>Returns the language saved in localStorage (whisper_language), or null.</summary>
        public async Task<string?> GetSavedLanguageAsync() => await _module.InvokeAsync<string?>("getWhisperSavedLanguage");

        /// <summary>Wires the #whisperLanguageSelect change listener (calls setLanguage + localStorage).</summary>
        public async Task WireLanguageSelectChangeAsync() => await _module.InvokeVoidAsync("wireLanguageSelectChange");

        /// <summary>Finishes the current recording and sends it to the server.</summary>
        public async Task FinishRecordingAsync() => await _module.InvokeVoidAsync("finishRecording");

        /// <summary>Clears the transcription output in the UI.</summary>
        public async Task ResetTranscriptAsync() => await _module.InvokeVoidAsync("resetTranscript");

        /// <summary>Cancels the current recording and restarts the transcription session.</summary>
        public async Task CancelAndRestartAsync() => await _module.InvokeVoidAsync("cancelAndRestart");

        /// <summary>Hides the audio visualizer.</summary>
        public async Task HideVisualizerAsync() => await _module.InvokeVoidAsync("hideVisualizer");

        [JSInvokable]
        public void JSCallback_OnStateChanged(string state)
        {
            OnStateChanged?.Invoke(state);
        }

        [JSInvokable]
        public void JSCallback_OnLivePreview(string text)
        {
            OnLivePreview?.Invoke(text);
        }

        [JSInvokable]
        public void JSCallback_OnTranscriptionFinal(string text)
        {
            OnTranscriptionFinal?.Invoke(text);
        }
        
        [JSInvokable]
        public void JSCallback_OnBackendChanged(string backend)
        {
            OnBackendChanged?.Invoke(backend);
        }

        [JSInvokable]
        public void JSCallback_OnStatusMessage(string message)
        {
            OnStatusMessage?.Invoke(message);
        }

        [JSInvokable]
        public void JSCallback_OnUIStateChanged(WhisperUIState state)
        {
            OnUIStateChanged?.Invoke(state);
        }

        /// <summary>Disposes the JS module reference and clears event handlers.</summary>
        public async ValueTask DisposeAsync()
        {
            OnStateChanged = null;
            OnLivePreview = null;
            OnTranscriptionFinal = null;
            OnBackendChanged = null;
            OnStatusMessage = null;
            OnUIStateChanged = null;

            _objRef.Dispose();
            if (_module != null)
            {
                try
                {
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
                catch (TaskCanceledException) { }
            }
        }
    }
}
