namespace Nabu;

/// <summary>
/// Implement this interface to receive final transcriptions from the Nabu widget.
/// Classes implementing this interface must be registered in the DI container.
/// </summary>
public interface INabuHandler
{
    /// <summary>
    /// Triggered when a transcription is finalized.
    /// </summary>
    /// <param name="text">The transcribed text. Guaranteed to be non-empty.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnTranscriptionReadyAsync(string text);
}
