namespace Nabu;

/// <summary>
/// Centralised string and numeric constants used across the Nabu widget and its JavaScript interop layer.
/// </summary>
public static class NabuConstants
{
    /// <summary>Status text emitted when transcription ends due to the absence of any detected voice activity.</summary>
    public const string TerminationText = "Transcription was terminated due to no available voice activity.";

    /// <summary>Status text emitted when the audio contained sound but no recognisable speech could be decoded.</summary>
    public const string NoRecognizableSpeechText = "No recognizable speech was detected.";

    /// <summary>Status text emitted when the user explicitly cancels an in-progress transcription.</summary>
    public const string UserCancelledText = "Transcription was cancelled due to user action.";

    /// <summary>Placeholder backend label shown while the widget is probing for an available inference backend.</summary>
    public const string BackendDetecting = "detecting...";

    /// <summary>Raw backend identifier tokens as reported by the JavaScript layer.</summary>
    public static class Backends
    {
        public const string Service = "service";
        public const string Browser = "browser";
        public const string BrowserWebGpu = "browser (WebGPU)";
        public const string BrowserWasm = "browser (WASM)";
    }

    /// <summary>Human-readable display labels corresponding to each backend token in <see cref="Backends"/>.</summary>
    public static class BackendLabels
    {
        public const string Service = "Server";
        public const string BrowserWebGpu = "Browser (WebGPU)";
        public const string BrowserWasm = "Browser (WASM)";
        public const string Browser = "Browser";
    }

    /// <summary>Status strings shown in the overlay UI during various widget states.</summary>
    public static class OverlayStatus
    {
        public const string Done = "Done.";
        public const string KeywordDetected = "Keyword detected!";
    }

    /// <summary>
    /// Milliseconds the widget waits after a transcription completes before dispatching the resolved
    /// command, giving the user time to see the final transcription text.
    /// </summary>
    public const int CommandDispatchDelayMs = 2000;

    /// <summary>
    /// Sentinel key values used by the language selection dropdown to trigger expand/collapse
    /// of the full language list without representing an actual language choice.
    /// </summary>
    public static class LanguageSelect
    {
        /// <summary>Pseudo-key that expands the language list to show all supported languages.</summary>
        public const string ExpandKey = "__more__";

        /// <summary>Pseudo-key that collapses the language list back to the popular languages subset.</summary>
        public const string CollapseKey = "__less__";
    }
}
