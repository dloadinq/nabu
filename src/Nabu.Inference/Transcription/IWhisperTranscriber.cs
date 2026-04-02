namespace Nabu.Inference.Transcription;

/// <summary>
/// Abstraction over a Whisper speech-to-text engine that supports transcription, translation,
/// and lazy model initialisation.
/// </summary>
public interface IWhisperTranscriber
{
    /// <summary>
    /// Ensures the underlying Whisper model is loaded and ready for inference.
    /// Safe to call multiple times; subsequent calls are no-ops once initialised.
    /// </summary>
    Task EnsureInitializedAsync();

    /// <summary>Returns <c>true</c> if the model has been successfully initialised.</summary>
    bool IsInitialized();

    /// <summary>
    /// Transcribes the audio in <paramref name="audioStream"/> using the specified language hint.
    /// </summary>
    /// <param name="audioStream">A seekable WAV stream containing 16 kHz mono PCM audio.</param>
    /// <param name="language">Whisper language name (e.g., <c>"german"</c>).</param>
    /// <returns>The transcribed text in the source language, or an empty string if nothing was detected.</returns>
    Task<string> TranscribeWithLanguageAsync(Stream audioStream, string language);

    /// <summary>
    /// Transcribes <paramref name="audioStream"/> and translates the result to English in a single pass.
    /// </summary>
    /// <param name="audioStream">A seekable WAV stream containing 16 kHz mono PCM audio.</param>
    /// <param name="language">Source language hint for Whisper.</param>
    /// <returns>The English translation of the spoken content.</returns>
    Task<string> TranslateToEnglishAsync(Stream audioStream, string language);

    /// <summary>
    /// Updates the active language for subsequent transcription calls.
    /// Rebuilds the Whisper processor if the model is already initialised.
    /// </summary>
    /// <param name="language">The new Whisper language name.</param>
    Task SetLanguageAsync(string language);
}
