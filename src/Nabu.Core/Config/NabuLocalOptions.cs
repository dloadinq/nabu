namespace Nabu.Core.Config;

/// <summary>
/// Root configuration options for the local Nabu inference server.
/// Bound from the <c>"NabuLocal"</c> section in <c>appsettings.json</c>.
/// </summary>
public class NabuLocalOptions
{
    /// <summary>The configuration section name used for binding.</summary>
    public const string SectionName = "NabuLocal";

    /// <summary>Base URL the local server listens on. Defaults to <c>http://localhost:50000</c>.</summary>
    public string Url { get; set; } = "http://localhost:50000";

    /// <summary>Relative path to the status HTML page served by the local server.</summary>
    public string StatusHtmlPath { get; set; } = "Status.html";

    /// <summary>Whisper transcription options.</summary>
    public WhisperOptions Whisper { get; set; } = new();

    /// <summary>Voice-activity detection (VAD) options.</summary>
    public VadOptions Vad { get; set; } = new();

    /// <summary>Wake-word detection options.</summary>
    public WakeWordOptions WakeWord { get; set; } = new();
}

/// <summary>Configuration for the Whisper speech-to-text model.</summary>
public class WhisperOptions
{
    /// <summary>Directory where GGML model files are stored or downloaded to.</summary>
    public string ModelsDirectory { get; set; } = "models";

    /// <summary>Default transcription language passed to Whisper (e.g., <c>"english"</c>).</summary>
    public string Language { get; set; } = "english";
}

/// <summary>
/// Configuration for the Silero VAD ONNX model that drives speech start/end detection.
/// </summary>
public class VadOptions
{
    /// <summary>Path to the Silero VAD ONNX model file.</summary>
    public string ModelPath { get; set; } = "models/silero_vad.onnx";

    /// <summary>
    /// Speech probability threshold. A window is classified as speech when the model's output exceeds
    /// this value. Range 0–1; higher values reduce false positives.
    /// </summary>
    public float Threshold { get; set; } = 0.75f;

    /// <summary>Audio sampling rate in Hz. Must match the rate used during recording (typically 16000).</summary>
    public int SamplingRate { get; set; } = 16000;

    /// <summary>Minimum speech duration in milliseconds before a speech segment is considered valid.</summary>
    public int MinSpeechDurationMs { get; set; } = 250;

    /// <summary>Maximum allowed speech duration in seconds before the session is forcibly terminated.</summary>
    public float MaxSpeechDurationSeconds { get; set; } = float.PositiveInfinity;

    /// <summary>
    /// Duration of silence in milliseconds required after the last detected speech before recording
    /// is considered complete and finalisation is triggered.
    /// </summary>
    public int MinSilenceDurationMs { get; set; } = 1500;

    /// <summary>Padding in milliseconds added around speech segments to avoid clipping.</summary>
    public int SpeechPadMs { get; set; } = 30;
}

/// <summary>Configuration for the NanoWakeWord model used for keyword spotting.</summary>
public class WakeWordOptions
{
    /// <summary>
    /// Model identifier passed to the NanoWakeWord runtime (e.g., <c>"hey_jarvis_v0.1"</c>).
    /// Determines which wake-word phrase activates the assistant.
    /// </summary>
    public string Model { get; set; } = "hey_jarvis_v0.1";

    /// <summary>
    /// Activation threshold for the wake-word model. A detection score at or above this value triggers
    /// the keyword-detected event. Range 0–1.
    /// </summary>
    public float Threshold { get; set; } = 0.7f;

    /// <summary>Number of audio frames processed per inference step.</summary>
    public int StepFrames { get; set; } = 10;
}
