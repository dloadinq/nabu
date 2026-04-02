using Microsoft.JSInterop;

namespace Nabu;

/// <summary>
/// Snapshot of the overlay UI state pushed from the JavaScript layer via
/// <see cref="NabuJsInterop.JSCallback_OnUIStateChanged"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public record NabuUIState(
    bool ShowOverlay = false,
    string OverlayStatus = "",
    string OverlayText = "",
    bool IsPreviewText = false,
    bool ShowCancelButton = false,
    bool IsCancelling = false,
    bool IsProcessing = false
);

/// <summary>
/// JavaScript interop bridge for the Nabu voice assistant (<c>app.js</c>).
/// Loads the JS module as a lazy ES module import, registers a .NET callback object so that the
/// JavaScript side can call back into .NET, and exposes typed events for each JS-to-.NET notification.
/// </summary>
/// <remarks>
/// Register as a scoped service. Call <see cref="InitializeAsync"/> once per circuit before using any other members.
/// Implements <see cref="IAsyncDisposable"/>; disposal unregisters the .NET callback and stops the live session.
/// </remarks>
public class NabuJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<NabuJsInterop>? _objRef;

    /// <summary>Raised when the internal JS state machine transitions to a new named state.</summary>
    public event Action<string>? OnStateChanged;

    /// <summary>Raised periodically with an in-progress partial transcription for live preview display.</summary>
    public event Action<string>? OnLivePreview;

    /// <summary>Raised with the completed, finalised transcription text.</summary>
    public event Action<string>? OnTranscriptionFinal;

    /// <summary>Raised when the active inference backend changes (e.g., WebGPU becomes available).</summary>
    public event Action<string>? OnBackendChanged;

    /// <summary>Raised with human-readable status messages intended for display in the UI.</summary>
    public event Action<string>? OnStatusMessage;

    /// <summary>Raised whenever the overlay UI state changes.</summary>
    // ReSharper disable once InconsistentNaming
    public event Action<NabuUIState>? OnUIStateChanged;

    /// <summary>
    /// Raised to block or unblock the manual cancel button. <c>true</c> means cancellation is in progress
    /// and the button should be disabled.
    /// </summary>
    public event Action<bool>? OnManualCancelBlock;

    /// <summary>Raised when the JS side has resolved a voice utterance to a command identifier.</summary>
    public event Action<string, string>? OnCommandResolved;

    /// <summary>Raised when the user selects a new language from the language picker.</summary>
    public event Action<string>? OnLanguageChangeRequested;

    /// <summary>
    /// Lazily imports the <c>app.js</c> ES module and registers this instance as the .NET callback target.
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_module != null) return;

        _module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "/_content/Nabu/app.js");
        _objRef = DotNetObjectReference.Create(this);

        await _module.InvokeVoidAsync("registerDotNetCallback", _objRef);
    }

    /// <summary>Calls the JS <c>initApp</c> function to bootstrap the voice assistant on the page.</summary>
    public Task InitAppAsync()
    {
        return TaskCall("initApp");
    }

    /// <summary>
    /// Starts the voice pipeline in response to a user gesture, required by browser autoplay policies.
    /// </summary>
    /// <param name="language">Whisper language hint to use for the initial session (default: <c>"english"</c>).</param>
    public Task StartOnUserGestureAsync(string language = "english")
    {
        return TaskCall("startOnUserGesture", language);
    }

    /// <summary>Instructs the JS layer to switch to a different transcription language mid-session.</summary>
    /// <param name="language">Target language name accepted by Whisper (e.g., <c>"german"</c>).</param>
    public Task SetLanguageAsync(string language)
    {
        return TaskCall("setLanguage", language);
    }

    /// <summary>
    /// Reads the language preference that was previously persisted to <c>localStorage</c> by the JS layer.
    /// Returns <c>null</c> if the module has not been loaded or no preference was saved.
    /// </summary>
    public Task<string?> GetSavedLanguageAsync()
    {
        return _module?.InvokeAsync<string?>("getSavedLanguage").AsTask() ?? Task.FromResult<string?>(null);
    }

    /// <summary>Attaches the JS change event handler to the language selector element in the DOM.</summary>
    public async Task AddLanguageSelectEventAsync()
    {
        await _module!.InvokeVoidAsync("addLanguageSelectEvent");
    }

    /// <summary>
    /// Notifies the JS command resolver of the current Blazor route so that scope-restricted commands
    /// can be filtered correctly.
    /// </summary>
    /// <param name="route">The current page route (e.g., <c>"/counter"</c>).</param>
    public Task SetPageContextAsync(string route)
    {
        return TaskCall("setPageContext", route);
    }

    /// <summary>
    /// Sends the full list of registered command definition items to the JS layer so it can perform
    /// incremental embedding sync with the server.
    /// </summary>
    /// <param name="items">The command definitions to synchronise.</param>
    public Task SetCommandSyncItemsAsync(IEnumerable<CommandDefinitionItem> items)
    {
        return TaskCall("setCommandSyncItems", (object)items.ToArray());
    }

    /// <inheritdoc cref="OnStateChanged"/>
    [JSInvokable]
    public void JSCallback_OnStateChanged(string state)
    {
        OnStateChanged?.Invoke(state);
    }

    /// <inheritdoc cref="OnLivePreview"/>
    [JSInvokable]
    public void JSCallback_OnLivePreview(string text)
    {
        OnLivePreview?.Invoke(text);
    }

    /// <inheritdoc cref="OnTranscriptionFinal"/>
    [JSInvokable]
    public void JSCallback_OnTranscriptionFinal(string text)
    {
        OnTranscriptionFinal?.Invoke(text);
    }

    /// <inheritdoc cref="OnBackendChanged"/>
    [JSInvokable]
    public void JSCallback_OnBackendChanged(string backend)
    {
        OnBackendChanged?.Invoke(backend);
    }

    /// <inheritdoc cref="OnStatusMessage"/>
    [JSInvokable]
    public void JSCallback_OnStatusMessage(string message)
    {
        OnStatusMessage?.Invoke(message);
    }

    /// <inheritdoc cref="OnUIStateChanged"/>
    [JSInvokable]
    public void JSCallback_OnUIStateChanged(NabuUIState state)
    {
        OnUIStateChanged?.Invoke(state);
    }

    /// <inheritdoc cref="OnCommandResolved"/>
    [JSInvokable]
    public void JSCallback_OnCommandResolved(string id, string txt)
    {
        OnCommandResolved?.Invoke(id, txt);
    }

    /// <inheritdoc cref="OnManualCancelBlock"/>
    [JSInvokable]
    public void JSCallback_SetManualCancelBlock(bool active)
    {
        OnManualCancelBlock?.Invoke(active);
    }

    /// <inheritdoc cref="OnLanguageChangeRequested"/>
    [JSInvokable]
    public void JSCallback_OnLanguageChangeRequested(string selectedValue)
    {
        OnLanguageChangeRequested?.Invoke(selectedValue);
    }

    public async ValueTask DisposeAsync()
    {
        ClearEvents();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterDotNetCallback");
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException)
            {
            }

            try
            {
                await _module.InvokeVoidAsync("stopLive");
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException)
            {
            }

            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
            {
            }
        }

        _objRef?.Dispose();
    }

    private async Task TaskCall(string method, params object?[] args)
    {
        await (_module?.InvokeVoidAsync(method, args) ?? ValueTask.CompletedTask);
    }
    
    private void ClearEvents()
    {
        OnStateChanged = null;
        OnLivePreview = null;
        OnTranscriptionFinal = null;
        OnBackendChanged = null;
        OnStatusMessage = null;
        OnUIStateChanged = null;
        OnManualCancelBlock = null;
        OnCommandResolved = null;
        OnLanguageChangeRequested = null;
    }
}