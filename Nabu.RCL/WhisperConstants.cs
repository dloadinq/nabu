namespace Nabu.RCL;

/// <summary>Constants used by the Whisper RCL (termination text, backend labels, etc.).</summary>
public static class WhisperConstants
{
    /// <summary>
    /// This text is emitted by the Whisper RCL when a recording is automatically cancelled
    /// due to prolonged silence. Your handlers should check for this string to avoid
    /// interpreting it as a real user command.
    /// </summary>
    public const string TerminationText = "Transcription was terminated due to no available voice activity.";

    /// <summary>
    /// This text is emitted when Whisper returns transcription, but it only contains non-speech sounds
    /// like [throat clearing], (music), or *sigh*.
    /// </summary>
    public const string NoRecognizableSpeechText = "No recognizable speech was detected.";

    /// <summary>
    /// Displayed while the active backend (Server / Browser / WebGPU / WASM) is being detected.
    /// Used by both Blazor (via IWhisperSettings) and RazorPages (WhisperViewComponent).
    /// </summary>
    public const string BackendDetecting = "detecting...";
}
