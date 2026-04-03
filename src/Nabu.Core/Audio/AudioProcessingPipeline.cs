using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nabu.Core.Config;
using Nabu.Core.Transcription;
using Nabu.Core.Vad;
using Nabu.Inference.Kws;
using Nabu.Inference.Transcription;
using Nabu.Inference.Vad;

namespace Nabu.Core.Audio;

/// <summary>
/// Central audio processing pipeline for the local Nabu server.
/// Receives raw PCM-16 audio chunks, runs them through a wake-word detector and a voice-activity
/// detector (VAD), records speech segments, and produces transcriptions via Whisper.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline uses a bounded <see cref="System.Threading.Channels.Channel{T}"/> to decouple audio
/// ingestion from processing, preventing buffer overflow under load.
/// </para>
/// <para>
/// Events raised by this class (<see cref="OnWakeWordDetected"/>, <see cref="OnTranscriptionPreview"/>,
/// <see cref="OnTranscriptionFinal"/>, <see cref="OnStatusChanged"/>) are invoked on the processing
/// background thread. Subscribers must marshal to the UI thread if needed.
/// </para>
/// </remarks>
public partial class AudioProcessingPipeline : IDisposable
{
    private readonly ILogger<AudioProcessingPipeline> _logger;
    private readonly IWhisperTranscriber _whisperTranscriber;

    private readonly IWakeWordDetector _wakeWordDetector;
    private readonly SpeechDetector _speechDetector;
    private readonly AudioRecordingSession _recordingSession;

    private readonly record struct AudioChunk(byte[] Buffer, int Length);

    private readonly Channel<AudioChunk> _chunkChannel;
    private readonly Task _processLoopTask;
    private readonly CancellationTokenSource _processLoopCts = new();

    private volatile bool _isRecording;
    private volatile bool _discardNextTranscription;
    private volatile bool _keywordDetected;
    private volatile bool _inWakeWordDelay;

    private long _wakeWordCooldownTicks = DateTime.MinValue.Ticks;
    private const int WakeWordCooldownSeconds = 2;
    private readonly SemaphoreSlim _whisperLock = new(1, 1);
    private readonly SemaphoreSlim _finalizeLock = new(1, 1);
    private readonly int _wakeWordReadyDelayMs = 1200;
    private CancellationTokenSource? _wakeWordReadyCts;

    private long _lastPreviewTimestamp;
    private const int LivePreviewIntervalMs = 1000;
    private const int MaxQueuedChunks = 64;

    private volatile string _preferredLanguage = "english";
    private readonly string _wakeWordModelName;
    private readonly bool _translateCommands = true;
    private readonly Regex _stripWakeWordRegex;

    /// <summary>Raised with the wake-word model name when the wake word is detected.</summary>
    public event Action<string>? OnWakeWordDetected;

    /// <summary>Raised periodically during active recording with an intermediate Whisper transcription.</summary>
    public event Action<string>? OnTranscriptionPreview;

    /// <summary>
    /// Raised when a recording segment is finalised. The first argument is the transcription in the
    /// user's language; the second is the English translation (or <c>null</c> if translation was skipped).
    /// </summary>
    public event Action<string, string?>? OnTranscriptionFinal;

    /// <summary>Raised with a human-readable status string at key lifecycle transitions (e.g., "Listening...", "Idle.").</summary>
    public event Action<string>? OnStatusChanged;

    public AudioProcessingPipeline(
        ILogger<AudioProcessingPipeline> logger,
        IWhisperTranscriber whisperTranscriber,
        IVadDetector vadDetector,
        IWakeWordDetector wakeWordDetector,
        IOptions<NabuLocalOptions> options)
    {
        _logger = logger;
        _whisperTranscriber = whisperTranscriber;

        var nabuOptions = options.Value;
        _wakeWordModelName = nabuOptions.WakeWord.Model;

        var wakeWordPhrase = DeriveWakeWordPhrase(_wakeWordModelName);
        _stripWakeWordRegex = BuildStripRegex(wakeWordPhrase);

        _logger.LogInformation("Wake Word Phrase: {WakeWordPhrase}", wakeWordPhrase);

        _wakeWordDetector = wakeWordDetector;
        _speechDetector = new SpeechDetector(vadDetector, nabuOptions.Vad);
        _recordingSession = new AudioRecordingSession();

        _chunkChannel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(MaxQueuedChunks)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _processLoopTask = ProcessChunkLoopAsync(_processLoopCts.Token);
    }

    /// <summary>
    /// Queues a PCM-16 audio chunk for processing. The buffer must have been rented from
    /// <see cref="System.Buffers.ArrayPool{T}.Shared"/>. Ownership is transferred to the pipeline,
    /// which will return it after processing.
    /// </summary>
    /// <param name="buffer">Pooled byte array containing raw 16-bit little-endian PCM samples.</param>
    /// <param name="length">Number of valid bytes in <paramref name="buffer"/>. Must be even and non-zero.</param>
    public async Task ProcessAudioChunkAsync(byte[] buffer, int length)
    {
        if (length == 0 || length % 2 != 0)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            return;
        }
        try
        {
            await _chunkChannel.Writer.WriteAsync(new AudioChunk(buffer, length));
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    private async Task ProcessChunkLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _chunkChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    ProcessChunk(chunk.Buffer, chunk.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing audio chunk");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk.Buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Audio processing loop faulted unexpectedly");
        }
    }

    private void ProcessChunk(byte[] buffer, int length)
    {
        var shorts = MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, length));

        ProcessRecordingChunk(buffer, length);
        FeedDetectors(shorts);

        if (TryHandleWakeWord())
            _ = EnsureWhisperInitializedAsync();

        var speechResult = _speechDetector.ProcessBuffer(_isRecording);
        if (TryHandleSpeechResult(speechResult))
            return;

        TryTriggerLivePreview();
    }

    private void ProcessRecordingChunk(byte[] buffer, int length)
    {
        if (_isRecording)
            _recordingSession.RecordChunk(buffer, length);
        else if (!_inWakeWordDelay)
            _recordingSession.ProcessPreRoll(buffer, length);
    }

    private void FeedDetectors(ReadOnlySpan<short> shorts)
    {
        int sampleCount = shorts.Length;
        var floatSamples = ArrayPool<float>.Shared.Rent(sampleCount);
        try
        {
            ConvertPcm16ToFloat(shorts, floatSamples.AsSpan(0, sampleCount));
            _speechDetector.ProcessBatch(floatSamples.AsSpan(0, sampleCount));
            if (!_isRecording && !_keywordDetected) _wakeWordDetector.ProcessBatch(shorts);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(floatSamples);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertPcm16ToFloat(ReadOnlySpan<short> input, Span<float> output)
    {
        const float invScale = 1f / 32768f;
        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] * invScale;
    }

    private bool TryHandleWakeWord()
    {
        if (_isRecording) return false;
        if (_keywordDetected) return false;
        if (IsInCooldown()) return false;

        var detected = _wakeWordDetector.ProcessBuffer();
        if (!detected) return false;

        _recordingSession.DiscardPreRoll();
        _inWakeWordDelay = true;
        SafeInvoke(OnWakeWordDetected, _wakeWordModelName);

        _wakeWordReadyCts?.Cancel();
        _wakeWordReadyCts?.Dispose();
        _wakeWordReadyCts = new CancellationTokenSource();
        _ = ActivateKeywordAfterDelayAsync(_wakeWordReadyCts.Token);

        return true;
    }

    private async Task ActivateKeywordAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_wakeWordReadyDelayMs, ct);
        }
        catch (TaskCanceledException)
        {
            _inWakeWordDelay = false;
            return;
        }
        _inWakeWordDelay = false;
        _keywordDetected = true;
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
        _lastPreviewTimestamp = Stopwatch.GetTimestamp();
        _recordingSession.StartRecording();
        SafeInvoke(OnStatusChanged, "Listening...");
    }

    private void TryTriggerLivePreview()
    {
        if (!_isRecording || !_whisperTranscriber.IsInitialized()) return;
        if (Stopwatch.GetElapsedTime(_lastPreviewTimestamp).TotalMilliseconds <= LivePreviewIntervalMs) return;

        _lastPreviewTimestamp = Stopwatch.GetTimestamp();
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

    private void SafeInvoke(Action<string, string?>? handler, string arg1, string? arg2)
    {
        if (handler == null) return;
        try
        {
            handler(arg1, arg2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event handler threw: {Arg1} / {Arg2}", arg1, arg2);
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
                string preview =
                    await _whisperTranscriber.TranscribeWithLanguageAsync(previewStream, _preferredLanguage);
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
            long startTime = Stopwatch.GetTimestamp();
            _logger.LogInformation("Stopping recording and finalizing...");
            SetCooldown();
            _speechDetector.Reset();
            _wakeWordDetector.Reset();
            _isRecording = false;

            try
            {
                var recordingStream = await _recordingSession.StopAndGetStreamAsync();

                if (recordingStream != null)
                {
                    SafeInvoke(OnStatusChanged, "Initializing...");
                    await _whisperLock.WaitAsync();
                    try
                    {
                        if (_whisperTranscriber.IsInitialized())
                        {
                            var fullText =
                                await _whisperTranscriber.TranscribeWithLanguageAsync(recordingStream,
                                    _preferredLanguage);
                            SafeInvoke(OnTranscriptionPreview, fullText.Trim());

                            string final = StripWakeWord(fullText.Trim());

                            string? translatedFinal = null;
                            if (_translateCommands && !string.IsNullOrEmpty(final))
                            {
                                recordingStream.Position = 0;
                                var translatedText = await _whisperTranscriber.TranslateToEnglishAsync(recordingStream, _preferredLanguage);
                                translatedFinal = StripWakeWord(translatedText.Trim());
                            }

                            if (!string.IsNullOrEmpty(final) && !_discardNextTranscription)
                                SafeInvoke(OnTranscriptionFinal, final, translatedFinal);
                            
                            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
                            _logger.LogInformation("Transcription done. Duration: {ElapsedMs:F2}ms", elapsed.TotalMilliseconds);
                            _discardNextTranscription = false;
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

    private bool IsInCooldown()
        => DateTime.Now.Ticks < Interlocked.Read(ref _wakeWordCooldownTicks);

    private void SetCooldown()
        => Interlocked.Exchange(ref _wakeWordCooldownTicks,
            DateTime.Now.AddSeconds(WakeWordCooldownSeconds).Ticks);

    private static string DeriveWakeWordPhrase(string modelName)
    {
        var parts = modelName.Split('_');
        var wordParts = parts.TakeWhile(p => !VersionPartRegex().IsMatch(p));
        return string.Join(' ', wordParts);
    }

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

    [GeneratedRegex(@"^v\d")]
    private static partial Regex VersionPartRegex();

    /// <summary>
    /// Updates the preferred transcription language used for all subsequent Whisper calls.
    /// Thread-safe via a volatile write.
    /// </summary>
    /// <param name="language">Whisper language name (e.g., <c>"german"</c>).</param>
    public void SetPreferredLanguage(string language)
    {
        _preferredLanguage = language;
    }

    /// <summary>
    /// Immediately stops recording and triggers finalisation, producing a transcription for the audio
    /// captured so far. Does nothing if not currently recording.
    /// </summary>
    public Task ForceStopAndFinalizeAsync()
    {
        if (!_isRecording) return Task.CompletedTask;
        _isRecording = false;
        return StopRecordingAndFinalizeAsync();
    }

    /// <summary>
    /// Cancels the current recording session and discards all buffered audio without producing a
    /// transcription. Resets wake-word and VAD state.
    /// </summary>
    public async Task CancelAndDiscardAsync()
    {
        _discardNextTranscription = true;
        _isRecording = false;
        _keywordDetected = false;
        _inWakeWordDelay = false;
        _wakeWordReadyCts?.Cancel();
        _speechDetector.Reset();
        _wakeWordDetector.Reset();

        var stream = await _recordingSession.StopAndGetStreamAsync();
        if (stream != null)
            await stream.DisposeAsync();

        SafeInvoke(OnStatusChanged, "Idle.");
    }

    public void Dispose()
    {
        _chunkChannel.Writer.TryComplete();
        _processLoopCts.Cancel();

        try
        {
            _processLoopTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during audio pipeline shutdown");
        }

        while (_chunkChannel.Reader.TryRead(out var remaining))
        {
            ArrayPool<byte>.Shared.Return(remaining.Buffer);
        }

        _processLoopCts.Dispose();
        _wakeWordReadyCts?.Cancel();
        _wakeWordReadyCts?.Dispose();
        _whisperLock.Dispose();
        _finalizeLock.Dispose();
        _recordingSession.Dispose();
        
        (_wakeWordDetector as IDisposable)?.Dispose();
    }
}
