namespace Nabu.Inference.Vad;

/// <summary>
/// Abstraction over a Voice Activity Detection (VAD) model that classifies fixed-size audio windows
/// as speech or non-speech and returns a probability score.
/// </summary>
public interface IVadDetector
{
    /// <summary>
    /// The number of samples expected per inference window. Implementations must only be called with
    /// spans of exactly this length.
    /// </summary>
    int WindowSize { get; }

    /// <summary>
    /// Runs one VAD inference pass on a fixed-size window of normalised float samples.
    /// </summary>
    /// <param name="buffer">A span of exactly <see cref="WindowSize"/> normalised float samples (−1 to +1).</param>
    /// <returns>Speech probability for the window, in the range [0, 1].</returns>
    float Process(ReadOnlySpan<float> buffer);

    /// <summary>Resets the model's internal recurrent state, discarding any accumulated context.</summary>
    void Reset();
}
