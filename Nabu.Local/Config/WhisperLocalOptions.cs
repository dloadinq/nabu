namespace Nabu.Local.Config;

public class WhisperLocalOptions
{
    public const string SectionName = "WhisperLocal";
    public string Url { get; set; } = "http://localhost:50000";
    public string StatusHtmlPath { get; set; } = "Status.html";
    public WhisperOptions Whisper { get; set; } = new();
    public VadOptions Vad { get; set; } = new();
    public WakeWordOptions WakeWord { get; set; } = new();
}

public class WhisperOptions
{
    public string GpuModelPath { get; set; } = "models/ggml-medium.bin";
    public string CpuModelPath { get; set; } = "models/ggml-medium_q4.bin";
    public string Language { get; set; } = "english";
}

public class VadOptions
{
    public string ModelPath { get; set; } = "resources/silero_vad.onnx";
    public float Threshold { get; set; } = 0.65f;
    public int SamplingRate { get; set; } = 16000;
    public int MinSpeechDurationMs { get; set; } = 250;
    public float MaxSpeechDurationSeconds { get; set; } = float.PositiveInfinity;
    public int MinSilenceDurationMs { get; set; } = 1000;
    public int SpeechPadMs { get; set; } = 30;
}

public class WakeWordOptions
{
    public string Model { get; set; } = "hey_jarvis_v0.1";
    public float Threshold { get; set; } = 0.9f;
    public int StepFrames { get; set; } = 4;
}
