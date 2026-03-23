namespace Nabu.RCL;

/// <summary>Implement this interface to receive final transcriptions from the Whisper widget.</summary>
public interface IWhisperHandler
{
    /// <summary>Called when a transcription is ready. Register via DI (IEnumerable&lt;IWhisperHandler&gt;).</summary>
    Task OnTranscriptionReadyAsync(string text);
}