using System.Text;
using Whisper.net;

namespace Nabu.Local.Transcription;

public class WhisperService : IWhisperTranscriber, IDisposable
{
    private string _whisperLanguage;
    private readonly string _gpuModelPath;
    private readonly string _cpuModelPath;
    private string _activeModelPath = "";
    private readonly ILogger<WhisperService> _logger;
    private readonly SemaphoreSlim _transcribeLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public WhisperProcessor? Processor { get; private set; }

    private WhisperFactory? _whisperFactory;
    private bool _whisperInitialized;

    public WhisperService(string whisperLanguage, string gpuModelPath, string cpuModelPath, ILogger<WhisperService> logger)
    {
        _whisperLanguage = whisperLanguage;
        _gpuModelPath = gpuModelPath;
        _cpuModelPath = cpuModelPath;
        _logger = logger;
    }
    
    public async Task<WhisperProcessor> InitializeWhisperAsync()
    {
        await EnsureInitializedAsync();
        return Processor!;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_whisperInitialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_whisperInitialized) return;
            await InitializeCoreAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeCoreAsync()
    {
        if (File.Exists(_gpuModelPath))
        {
            try
            {
                _logger.LogDebug("Trying GPU model: {Path}", _gpuModelPath);
                _whisperFactory = WhisperFactory.FromPath(_gpuModelPath);
                Processor = _whisperFactory.CreateBuilder()
                    .WithLanguage(_whisperLanguage)
                    .Build();
                _activeModelPath = _gpuModelPath;
                _whisperInitialized = true;
                _logger.LogDebug("Whisper initialized with GPU model: {Path}", _gpuModelPath);
                return;
            }
            catch (WhisperModelLoadException)
            {
                _logger.LogWarning("GPU model failed to load, falling back to CPU model");
                _whisperFactory?.Dispose();
                _whisperFactory = null;
            }
        }

        if (!File.Exists(_cpuModelPath))
            throw new FileNotFoundException($"Whisper CPU model not found at: {Path.GetFullPath(_cpuModelPath)}");

        _whisperFactory = WhisperFactory.FromPath(_cpuModelPath);
        Processor = _whisperFactory.CreateBuilder()
            .WithLanguage(_whisperLanguage)
            .Build();
        _activeModelPath = _cpuModelPath;
        _whisperInitialized = true;
        _logger.LogDebug("Whisper initialized with CPU model: {Path}", _cpuModelPath);
    }

    public async Task SetLanguageAsync(string language)
    {
        if (string.Equals(_whisperLanguage, language, StringComparison.OrdinalIgnoreCase))
            return;

        await _transcribeLock.WaitAsync();
        try
        {
            if (string.Equals(_whisperLanguage, language, StringComparison.OrdinalIgnoreCase))
                return;

            _whisperLanguage = language;
            _logger.LogInformation("Language changed to: {Language}", language);

            if (_whisperInitialized && _whisperFactory != null)
            {
                Processor?.Dispose();
                Processor = _whisperFactory.CreateBuilder()
                    .WithLanguage(_whisperLanguage)
                    .Build();
                _logger.LogInformation("Whisper processor rebuilt with language: {Language}", language);
            }
        }
        finally
        {
            _transcribeLock.Release();
        }
    }

    public void Dispose()
    {
        Processor?.Dispose();
        _whisperFactory?.Dispose();
        _whisperInitialized = false;
    }

    public bool IsInitialized() => _whisperInitialized;

    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        return await TranscribeWithLanguageAsync(audioStream, _whisperLanguage);
    }

    public async Task<string> TranscribeWithLanguageAsync(Stream audioStream, string language)
    {
        await _transcribeLock.WaitAsync();
        try
        {
            if (!_whisperInitialized || Processor == null)
                await EnsureInitializedAsync();

            if (!string.Equals(_whisperLanguage, language, StringComparison.OrdinalIgnoreCase) && _whisperFactory != null)
            {
                _whisperLanguage = language;
                Processor?.Dispose();
                Processor = _whisperFactory.CreateBuilder().WithLanguage(_whisperLanguage).Build();
            }

            var transcriptionBuilder = new StringBuilder();
            await foreach (var result in Processor!.ProcessAsync(audioStream))
                transcriptionBuilder.Append(result.Text);
            return transcriptionBuilder.ToString().Trim();
        }
        finally
        {
            _transcribeLock.Release();
        }
    }
}
