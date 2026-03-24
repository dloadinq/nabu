namespace Nabu.Core.Transcription;

public interface IWhisperTranscriber
{
    Task EnsureInitializedAsync();
    bool IsInitialized();
    Task<string> TranscribeAsync(Stream audioStream);
    Task<string> TranscribeWithLanguageAsync(Stream audioStream, string language);
    Task SetLanguageAsync(string language);
}
