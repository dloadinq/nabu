namespace Nabu.Inference.Kws;

/// <summary>
/// Abstraction over a keyword-spotting (KWS) engine that detects a configured wake word in a stream
/// of raw 16-bit PCM audio samples.
/// </summary>
public interface IWakeWordDetector
{
    /// <summary>
    /// Appends a batch of raw PCM-16 audio samples to the detector's internal accumulation buffer.
    /// This method is intended to be called on every incoming audio chunk without blocking.
    /// </summary>
    /// <param name="samples">Read-only span of 16-bit signed PCM samples at 16 kHz mono.</param>
    void ProcessBatch(ReadOnlySpan<short> samples);

    /// <summary>
    /// Evaluates all buffered samples and returns <c>true</c> if a wake word was detected.
    /// The internal buffer is cleared after processing regardless of the result.
    /// </summary>
    /// <returns><c>true</c> if the wake word was detected; otherwise <c>false</c>.</returns>
    bool ProcessBuffer();

    /// <summary>Discards all buffered samples and resets any internal model state.</summary>
    void Reset();
}