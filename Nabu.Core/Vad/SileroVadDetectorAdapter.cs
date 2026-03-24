using VADdotnet;

namespace Nabu.Core.Vad;

/// <summary>
/// Adapter that uses VadDotNet's SileroVadOnnxModel from the silero-vad project
/// for streaming VAD via <see cref="IVadDetector"/>. The original SileroVadDetector
/// only supports file-based GetSpeechSegmentList; streaming (Process/WindowSize) is
/// implemented here using the ONNX model directly.
/// </summary>
public class SileroVadDetectorAdapter : IVadDetector, IDisposable
{
    private readonly SileroVadOnnxModel _model;
    private readonly int _samplingRate;

    private const int WindowSize8K = 256;
    private const int WindowSize16K = 512;

    /// <param name="onnxModelPath">Path to the Silero VAD ONNX model.</param>
    /// <param name="samplingRate">Audio sampling rate (8000 or 16000 Hz).</param>
    /// <remarks>
    /// The remaining parameters (threshold, minSpeechDurationMs, etc.) are accepted for API compatibility
    /// but are unused here — they are only relevant for SileroVadDetector.GetSpeechSegmentList,
    /// not for the streaming Process() path used by this adapter.
    /// </remarks>
    public SileroVadDetectorAdapter(string onnxModelPath, float threshold, int samplingRate,
        int minSpeechDurationMs, float maxSpeechDurationSeconds,
        int minSilenceDurationMs, int speechPadMs)
    {
        _model = new SileroVadOnnxModel(onnxModelPath);
        _samplingRate = samplingRate;
    }

    public int WindowSize => _samplingRate == 16000 ? WindowSize16K : WindowSize8K;

    public float Process(float[] buffer)
    {
        var output = _model.Call([buffer], _samplingRate);
        return output[0];
    }

    public void Reset() => _model.ResetStates();

    public void Dispose() => _model?.Dispose();
}
