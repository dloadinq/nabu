using System.Buffers;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nabu.Core.Config;
using Nabu.Core.Transcription;
using Nabu.Core.Vad;
using NanoWakeWord;

namespace Nabu.Core.Audio;

public class AudioProcessingPipeline : IDisposable
{
    private readonly ILogger<AudioProcessingPipeline> _logger;
    private readonly IWhisperTranscriber _whisperTranscriber;

    private readonly WakeWordDetector _wakeWordDetector;
    private readonly SpeechDetector _speechDetector;
    private readonly AudioRecordingSession _recordingSession;

    private readonly Channel<byte[]> _chunkChannel;
    private readonly Task _processLoopTask;
    private readonly CancellationTokenSource _processLoopCts = new();

    private bool _isRecording;
    private bool _keywordDetected;
    private DateTime _wakeWordCooldownUntil = DateTime.MinValue;
    private const int WakeWordCooldownSeconds = 3;
    private readonly SemaphoreSlim _whisperLock = new(1, 1);
    private readonly SemaphoreSlim _finalizeLock = new(1, 1);

    private DateTime _lastPreviewTime = DateTime.MinValue;
    private const int LivePreviewIntervalMs = 1000;
    private const int MaxQueuedChunks = 64;

    public event Action<string>? OnWakeWordDetected;
    public event Action<string>? OnTranscriptionPreview;
    public event Action<string>? OnTranscriptionFinal;
    public event Action<string>? OnStatusChanged;

    private string _preferredLanguage;
    private readonly string _wakeWordModelName;
    private readonly string _wakeWordPhrase;
    private readonly Regex _stripWakeWordRegex;

    public AudioProcessingPipeline(
        ILogger<AudioProcessingPipeline> logger,
        IWhisperTranscriber whisperTranscriber,
        IVadDetector vadDetector,
        WakeWordRuntime wakeWordRuntime,
        IOptions<WhisperLocalOptions> options)
    {
        _logger = logger;
        _whisperTranscriber = whisperTranscriber;
        _preferredLanguage = options.Value.Whisper.Language;
        _wakeWordModelName = options.Value.WakeWord.Model;
        _wakeWordPhrase = DeriveWakeWordPhrase(_wakeWordModelName);
        _stripWakeWordRegex = BuildStripRegex(_wakeWordPhrase);

        _wakeWordDetector = new WakeWordDetector(wakeWordRuntime);
        var vadOpts = options.Value.Vad;
        _speechDetector = new SpeechDetector(vadDetector, vadOpts.Threshold, vadOpts.MinSilenceDurationMs);
        _recordingSession = new AudioRecordingSession();

        _chunkChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaxQueuedChunks)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _processLoopTask = ProcessChunkLoopAsync(_processLoopCts.Token);
    }

    public async Task ProcessAudioChunkAsync(byte[] rawBytes)
    {
        if (!IsValidChunk(rawBytes)) return;
        await _chunkChannel.Writer.WriteAsync(rawBytes);
    }

    private async Task ProcessChunkLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var rawBytes in _chunkChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    ProcessChunk(rawBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing audio chunk");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ProcessChunk(byte[] rawBytes)
    {
        ProcessRecordingChunk(rawBytes);
        ProcessSamples(rawBytes);

        if (TryHandleWakeWord())
            _ = EnsureWhisperInitializedAsync();

        var speechResult = _speechDetector.ProcessBuffer(_isRecording);
        if (TryHandleSpeechResult(speechResult))
            return;

        TryTriggerLivePreview();
    }

    private static bool IsValidChunk(byte[] rawBytes)
    {
        return rawBytes.Length > 0 && (rawBytes.Length % 2) == 0;
    }

    private void ProcessRecordingChunk(byte[] rawBytes)
    {
        if (!_isRecording)
            _recordingSession.ProcessPreRoll(rawBytes);
        else
            _recordingSession.RecordChunk(rawBytes);
    }

    private void ProcessSamples(byte[] rawBytes)
    {
        int sampleCount = rawBytes.Length / 2;
        var shortSamples = ArrayPool<short>.Shared.Rent(sampleCount);
        var floatSamples = ArrayPool<float>.Shared.Rent(sampleCount);
        try
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(rawBytes, i * 2);
                shortSamples[i] = s;
                floatSamples[i] = s / 32768f;
            }
            _speechDetector.ProcessBatch(floatSamples.AsSpan(0, sampleCount));
            _wakeWordDetector.ProcessBatch(shortSamples.AsSpan(0, sampleCount));
        }
        finally
        {
            ArrayPool<short>.Shared.Return(shortSamples);
            ArrayPool<float>.Shared.Return(floatSamples);
        }
    }

    private bool TryHandleWakeWord()
    {
        if (_isRecording) return false;
        if (DateTime.Now < _wakeWordCooldownUntil) return false;

        if (!_wakeWordDetector.ProcessBuffer()) return false;

        _logger.LogInformation("Keyword Spotted: {WakeWord}", _wakeWordModelName);
        _keywordDetected = true;
        _recordingSession.DiscardPreRoll();
        SafeInvoke(OnWakeWordDetected, _wakeWordModelName);
        return true;
    }

    private bool TryHandleSpeechResult(SpeechResult speechResult)
    {
        if (speechResult == SpeechResult.SpeechDetected && _keywordDetected && !_isRecording)
        {
            StartRecording();
            return false;
        }

        if (speechResult == SpeechResult.SilenceTimeout && _isRecording)
        {
            _ = StopRecordingAndFinalizeAsync();
            return true;
        }

        return false;
    }

    private void StartRecording()
    {
        _logger.LogInformation("Starting speech recording...");
        _isRecording = true;
        _keywordDetected = false;
        _lastPreviewTime = DateTime.Now;
        _recordingSession.StartRecording();
        SafeInvoke(OnStatusChanged, "Listening...");
    }

    private void TryTriggerLivePreview()
    {
        if (!_isRecording || !_whisperTranscriber.IsInitialized()) return;
        if ((DateTime.Now - _lastPreviewTime).TotalMilliseconds <= LivePreviewIntervalMs) return;

        _lastPreviewTime = DateTime.Now;
        _ = GenerateLivePreviewAsync();
    }

    private void SafeInvoke(Action<string>? handler, string arg)
    {
        if (handler == null) return;
        try
        {
            handler(arg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event handler threw: {Arg}", arg);
        }
    }

    private async Task EnsureWhisperInitializedAsync()
    {
        if (_whisperTranscriber.IsInitialized()) return;
        try
        {
            _logger.LogInformation("Initializing Whisper model in background...");
            await _whisperTranscriber.EnsureInitializedAsync();
            _logger.LogInformation("Whisper model initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Whisper model.");
            SafeInvoke(OnStatusChanged, "Error: model failed to load.");
        }
    }

    private async Task GenerateLivePreviewAsync()
    {
        if (!await _whisperLock.WaitAsync(0)) return;

        try
        {
            var previewStream = await _recordingSession.CreatePreviewStreamAsync();
            if (previewStream == null) return;

            try
            {
                string preview = await _whisperTranscriber.TranscribeWithLanguageAsync(previewStream, _preferredLanguage);
                preview = StripWakeWord(preview.Trim());
                if (!string.IsNullOrEmpty(preview))
                    SafeInvoke(OnTranscriptionPreview, preview);
            }
            finally
            {
                await previewStream.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live preview generation failed");
        }
        finally
        {
            _whisperLock.Release();
        }
    }

    private async Task StopRecordingAndFinalizeAsync()
    {
        if (!await _finalizeLock.WaitAsync(0)) return;
        try
        {
            _logger.LogInformation("Stopping recording and finalizing...");
            _wakeWordCooldownUntil = DateTime.Now.AddSeconds(WakeWordCooldownSeconds);
            _speechDetector.Reset();
            _wakeWordDetector.Reset();
            _isRecording = false;

            try
            {
                var recordingStream = await _recordingSession.StopAndGetStreamAsync();

                if (recordingStream != null)
                {
                    await _whisperLock.WaitAsync();
                    try
                    {
                        if (_whisperTranscriber.IsInitialized())
                        {
                            var fullText = await _whisperTranscriber.TranscribeWithLanguageAsync(recordingStream, _preferredLanguage);
                            SafeInvoke(OnTranscriptionPreview, fullText.Trim());

                            string final = StripWakeWord(fullText.Trim());
                            if (!string.IsNullOrEmpty(final))
                                SafeInvoke(OnTranscriptionFinal, final);
                        }
                    }
                    finally
                    {
                        _whisperLock.Release();
                    }

                    await recordingStream.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recording finalization");
            }
            finally
            {
                SafeInvoke(OnStatusChanged, "Idle.");
            }
        }
        finally
        {
            _finalizeLock.Release();
        }
    }

    /// <summary>
    /// Derives the spoken wake word phrase from the model filename.
    /// e.g. "hey_jarvis_v0.1" → "hey jarvis", "alexa_v0.1" → "alexa"
    /// </summary>
    private static string DeriveWakeWordPhrase(string modelName)
    {
        var parts = modelName.Split('_');
        var wordParts = parts.TakeWhile(p => !Regex.IsMatch(p, @"^v\d"));
        return string.Join(" ", wordParts);
    }

    /// <summary>
    /// Builds a regex that strips the wake word from a transcription prefix,
    /// tolerating punctuation that Whisper may insert between or after words.
    /// e.g. phrase "hey jarvis" matches "Hey, Jarvis.", "hey jarvis," etc.
    /// </summary>
    private static Regex BuildStripRegex(string phrase)
    {
        var words = phrase.Split(' ').Select(Regex.Escape);
        var pattern = "^" + string.Join(@"[,.]?\s+", words) + @"[,.]?\s*";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private string StripWakeWord(string text)
    {
        var match = _stripWakeWordRegex.Match(text);
        if (match.Success)
            text = text[match.Length..];
        return text.Trim();
    }

    public void SetPreferredLanguage(string language)
    {
        _preferredLanguage = language;
    }

    /// <summary>
    /// Manually stop recording and finalize transcription. Use when user clicks Send button.
    /// </summary>
    public Task ForceStopAndFinalizeAsync()
    {
        if (!_isRecording) return Task.CompletedTask;
        _isRecording = false;
        return StopRecordingAndFinalizeAsync();
    }

    public void Dispose()
    {
        _chunkChannel.Writer.Complete();
        _processLoopCts.Cancel();
        try
        {
            _processLoopTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _processLoopCts.Dispose();
        _wakeWordDetector.Dispose();
    }
}
