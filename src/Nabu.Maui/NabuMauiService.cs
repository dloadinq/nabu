using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoWakeWord;
using Nabu.Core.Audio;
using Nabu.Core.Config;
using Nabu.Core.Kws;
using Nabu.Core.Transcription;
using Nabu.Core.Vad;
using Nabu.Inference.Kws;
using Nabu.Inference.Transcription;
using Nabu.Inference.Vad;

namespace Nabu.Maui;

public sealed class NabuMauiService : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly NabuLocalOptions _options;

    private AudioProcessingPipeline? _pipeline;
    private IWhisperTranscriber? _whisper;
    private WindowsAudioCapture? _capture;
    private string? _currentModelPath;

    /// <summary>Raised on the pipeline background thread — marshal to UI thread before touching UI.</summary>
    public event Action<string>? OnWakeWordDetected;
    public event Action<string>? OnTranscriptionPreview;
    public event Action<string, string?>? OnTranscriptionFinal;
    public event Action<string>? OnStatusChanged;

    public bool IsInitialized => _pipeline != null;

    /// <summary>Human-readable wake-word phrase derived from the model name (e.g. "Hey Jarvis").</summary>
    public string WakeWordPhrase => DerivePhrase(_options.WakeWord.Model);

    private static string DerivePhrase(string modelName)
    {
        var parts = modelName.Split('_');
        var words = parts.TakeWhile(p => !System.Text.RegularExpressions.Regex.IsMatch(p, @"^v\d"));
        return string.Join(' ', words.Select(w => char.ToUpper(w[0]) + w[1..]));
    }

    public NabuMauiService(ILoggerFactory loggerFactory, IOptions<NabuLocalOptions> options)
    {
        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    /// <summary>
    /// Creates and warms up the audio pipeline with the given Whisper model.
    /// Safe to call again if the model path changed (reinitializes).
    /// </summary>
    public async Task InitializeAsync(string modelPath, bool forceCpu = false)
    {
        if (_pipeline != null && _currentModelPath == modelPath) return;

        Stop();

        // Dispose off the UI thread: AudioProcessingPipeline.Dispose() calls
        // GetAwaiter().GetResult() on its background task. If that task captured
        // the UI SynchronizationContext (which it does when constructed here),
        // blocking on it from the UI thread would deadlock.
        if (_pipeline != null)
        {
            var old = _pipeline;
            _pipeline = null;
            await Task.Run(() => old.Dispose());
        }

        _whisper = null;

        var vadModelPath = await EnsureVadModelAsync();

        var whisperLogger = _loggerFactory.CreateLogger<WhisperService>();
        _whisper = new WhisperService(_options.Whisper.Language, modelPath, whisperLogger);

        var vad = new SileroVadDetectorAdapter(vadModelPath, _options.Vad.SamplingRate);

        var kwsRuntime = new WakeWordRuntime(new WakeWordRuntimeConfig
        {
            WakeWords =
            [
                new WakeWordConfig
                {
                    Model = _options.WakeWord.Model,
                    Threshold = _options.WakeWord.Threshold
                }
            ],
            StepFrames = _options.WakeWord.StepFrames
        });

        var pipelineLogger = _loggerFactory.CreateLogger<AudioProcessingPipeline>();
        _pipeline = new AudioProcessingPipeline(
            pipelineLogger,
            _whisper,
            vad,
            new WakeWordDetector(kwsRuntime),
            Options.Create(_options));

        _pipeline.OnWakeWordDetected += s => OnWakeWordDetected?.Invoke(s);
        _pipeline.OnTranscriptionPreview += s => OnTranscriptionPreview?.Invoke(s);
        _pipeline.OnTranscriptionFinal += (s, t) => OnTranscriptionFinal?.Invoke(s, t);
        _pipeline.OnStatusChanged += s => OnStatusChanged?.Invoke(s);

        _currentModelPath = modelPath;

        await _whisper.EnsureInitializedAsync();
    }

    /// <summary>Starts WASAPI microphone capture and feeds audio into the pipeline.</summary>
    public void Start()
    {
        if (_pipeline == null)
            throw new InvalidOperationException("Call InitializeAsync() before Start().");

        _capture = new WindowsAudioCapture(async (buf, len) =>
            await _pipeline.ProcessAudioChunkAsync(buf, len));
        _capture.Start();
    }

    /// <summary>Stops microphone capture. Does not destroy the pipeline or Whisper model.</summary>
    public void Stop()
    {
        _capture?.Stop();
        _capture?.Dispose();
        _capture = null;
    }

    public void SetLanguage(string language) => _pipeline?.SetPreferredLanguage(language);

    public Task CancelAsync() => _pipeline?.CancelAndDiscardAsync() ?? Task.CompletedTask;

    /// <summary>Extracts silero_vad.onnx from the MAUI app package to writable storage on first run.</summary>
    private static async Task<string> EnsureVadModelAsync()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "silero_vad.onnx");
        if (!File.Exists(path))
        {
            await using var src = await FileSystem.OpenAppPackageFileAsync("silero_vad.onnx");
            await using var dst = File.Create(path);
            await src.CopyToAsync(dst);
        }
        return path;
    }

    public void Dispose()
    {
        Stop();
        _pipeline?.Dispose();
        _pipeline = null;
    }
}
