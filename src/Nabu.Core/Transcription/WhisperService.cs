using System.Text;
using Microsoft.Extensions.Logging;
using Nabu.Inference.Transcription;
using Whisper.net;

namespace Nabu.Core.Transcription;

/// <summary>
/// Implements <see cref="IWhisperTranscriber"/> using the Whisper.net library with a GGML model file.
/// Maintains separate <c>WhisperProcessor</c> instances for transcription and translation to avoid
/// reconfiguring the processor between calls. Lazy initialisation defers the (potentially slow) model
/// load until the first audio chunk is processed.
/// </summary>
/// <remarks>
/// All public members are thread-safe. Concurrent transcription and translation calls are serialised
/// via an internal semaphore to comply with Whisper.net's single-threaded processor requirement.
/// </remarks>
public class WhisperService(string language, string modelPath, ILogger<WhisperService> logger)
    : IWhisperTranscriber, IAsyncDisposable
{
    private readonly SemaphoreSlim _transcribeLock = new(1, 1);
    private readonly SemaphoreSlim _translateLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private WhisperFactory? _factory;
    private WhisperProcessor? _transcribeProcessor;
    private WhisperProcessor? _translateProcessor;
    
    private string _transcribeLanguage = language;
    private string _translateLanguage = language;
    private bool _initialized;

    /// <inheritdoc/>
    public bool IsInitialized()
    {
        return _initialized;
    }

    /// <inheritdoc/>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            Initialize();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void Initialize()
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Whisper model not found: {Path.GetFullPath(modelPath)}");

        _factory = WhisperFactory.FromPath(modelPath);
        BuildProcessors();
        _initialized = true;
    }

    private void BuildProcessors()
    {
        _transcribeProcessor?.Dispose();
        _translateProcessor?.Dispose();
        _transcribeProcessor = _factory!.CreateBuilder().WithLanguage(_transcribeLanguage).Build();
        _translateProcessor = _factory!.CreateBuilder().WithLanguage(_translateLanguage).WithTranslate().Build();
    }

    /// <inheritdoc/>
    public async Task SetLanguageAsync(string language)
    {
        await _transcribeLock.WaitAsync();
        await _translateLock.WaitAsync();
        try
        {
            if (string.Equals(_transcribeLanguage, language, StringComparison.OrdinalIgnoreCase) && 
                string.Equals(_translateLanguage, language, StringComparison.OrdinalIgnoreCase)) return;
                
            logger.LogInformation("Language changed to {New}", language);
            _transcribeLanguage = language;
            _translateLanguage = language;

            if (_initialized && _factory != null)
            {
                BuildProcessors();
            }
        }
        finally
        {
            _translateLock.Release();
            _transcribeLock.Release();
        }
    }

    /// <summary>
    /// Transcribes a WAV audio stream using the currently configured language.
    /// </summary>
    /// <param name="audioStream">A seekable WAV stream containing 16 kHz mono PCM audio.</param>
    /// <returns>The transcribed text, or an empty string if no speech was detected.</returns>
    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        return await TranscribeWithLanguageAsync(audioStream, _transcribeLanguage);
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeWithLanguageAsync(Stream audioStream, string language)
    {
        await _transcribeLock.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            RebuildTranscribeIfLanguageChanged(language);
            var result = await RunProcessorAsync(_transcribeProcessor!, audioStream);
            return result;
        }
        finally
        {
            _transcribeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> TranslateToEnglishAsync(Stream audioStream, string language)
    {
        await _translateLock.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            RebuildTranslateIfLanguageChanged(language);
            return await RunProcessorAsync(_translateProcessor!, audioStream);
        }
        finally
        {
            _translateLock.Release();
        }
    }

    private void RebuildTranscribeIfLanguageChanged(string language)
    {
        if (string.Equals(_transcribeLanguage, language, StringComparison.OrdinalIgnoreCase) || _factory == null)
            return;

        _transcribeLanguage = language;
        _transcribeProcessor?.Dispose();
        _transcribeProcessor = _factory!.CreateBuilder().WithLanguage(language).Build();
    }
    
    private void RebuildTranslateIfLanguageChanged(string language)
    {
        if (string.Equals(_translateLanguage, language, StringComparison.OrdinalIgnoreCase) || _factory == null)
            return;

        _translateLanguage = language;
        _translateProcessor?.Dispose();
        _translateProcessor = _factory!.CreateBuilder().WithLanguage(language).WithTranslate().Build();
    }

    private static async Task<string> RunProcessorAsync(WhisperProcessor processor, Stream audioStream)
    {
        var sb = new StringBuilder(256);
        await foreach (var result in processor.ProcessAsync(audioStream))
            sb.Append(result.Text);
        return sb.ToString().Trim();
    }
    
    public async ValueTask DisposeAsync()
    {
        await _transcribeLock.WaitAsync();
        await _translateLock.WaitAsync();
        try
        {
            if (_transcribeProcessor != null) await _transcribeProcessor.DisposeAsync();
            if (_translateProcessor != null) await _translateProcessor.DisposeAsync();
            _factory?.Dispose();
        }
        finally
        {
            _translateLock.Release();
            _transcribeLock.Release();
        }

        _translateLock.Dispose();
        _transcribeLock.Dispose();
        _initLock.Dispose();
    }

}