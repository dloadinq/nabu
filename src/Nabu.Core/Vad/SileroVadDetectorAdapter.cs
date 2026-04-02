using Nabu.Inference.Vad;
using VADdotnet;

namespace Nabu.Core.Vad;

/// <summary>
/// Adapts the <c>VADdotnet</c> <see cref="SileroVadOnnxModel"/> to the <see cref="IVadDetector"/> interface.
/// Processes fixed-size audio windows and returns a speech probability score for each window.
/// Supports both 8 kHz (256-sample window) and 16 kHz (512-sample window) sampling rates.
/// </summary>
public class SileroVadDetectorAdapter : IVadDetector, IDisposable
{
    private readonly SileroVadOnnxModel _model;
    private readonly int _samplingRate;

    private readonly float[] _persistentBuffer;
    private readonly float[][] _callWrapper;

    private const int WindowSize8K = 256;
    private const int WindowSize16K = 512;

    /// <summary>
    /// Loads the Silero VAD ONNX model from <paramref name="onnxModelPath"/> and configures internal
    /// buffers for the given sampling rate.
    /// </summary>
    /// <param name="onnxModelPath">Path to the <c>silero_vad.onnx</c> model file.</param>
    /// <param name="samplingRate">Audio sampling rate in Hz (8000 or 16000).</param>
    public SileroVadDetectorAdapter(string onnxModelPath, int samplingRate)
    {
        _model = new SileroVadOnnxModel(onnxModelPath);
        _samplingRate = samplingRate;

        _persistentBuffer = new float[WindowSize];
        _callWrapper = [_persistentBuffer];
    }

    /// <inheritdoc/>
    public int WindowSize => _samplingRate == 16000 ? WindowSize16K : WindowSize8K;

    /// <summary>
    /// Copies <paramref name="input"/> into the internal window buffer and runs one VAD inference pass.
    /// </summary>
    /// <param name="input">A span of exactly <see cref="WindowSize"/> normalised float samples.</param>
    /// <returns>Speech probability for the window (0–1). Returns <c>0</c> for empty input.</returns>
    public float Process(ReadOnlySpan<float> input)
    {
        if (input.IsEmpty) return 0f;

        input.CopyTo(_persistentBuffer);

        var output = _model.Call(_callWrapper, _samplingRate);

        return output[0];
    }

    /// <summary>Resets the ONNX model's internal RNN hidden states, clearing accumulated context.</summary>
    public void Reset()
    {
        _model.ResetStates();
    }

    /// <summary>Disposes the underlying <see cref="SileroVadOnnxModel"/>.</summary>
    public void Dispose()
    {
        _model.Dispose();
    }
}