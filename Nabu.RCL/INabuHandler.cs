namespace Nabu.RCL;

/// <summary>Implement this interface to receive final transcriptions from the Nabu widget.</summary>
public interface INabuHandler
{
    /// <summary>Called when a transcription is ready. Register via DI (IEnumerable&lt;INabuHandler&gt;).</summary>
    Task OnTranscriptionReadyAsync(string text);
}
