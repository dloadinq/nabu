using System.Diagnostics;
using Nabu.Core.Config;
using Nabu.Inference.Vad;

namespace Nabu.Core.Vad;

/// <summary>Represents the outcome of a single VAD processing cycle.</summary>
public enum SpeechResult
{
    /// <summary>No speech was detected and no silence timeout occurred during this cycle.</summary>
    None,

    /// <summary>At least one audio window exceeded the speech probability threshold.</summary>
    SpeechDetected,

    /// <summary>
    /// Speech was previously detected but sufficient silence has elapsed to consider the utterance
    /// complete and trigger recording finalisation.
    /// </summary>
    SilenceTimeout
}

/// <summary>
/// Accumulates audio samples in a circular ring buffer, feeds fixed-size windows to a <see cref="IVadDetector"/>,
/// and tracks speech timing to detect utterance boundaries.
/// </summary>
/// <remarks>
/// <see cref="ProcessBatch"/> and <see cref="ProcessBuffer"/> are called from different threads and are
/// synchronised via an internal lock.
/// </remarks>
public class SpeechDetector
{
    private readonly IVadDetector _vadDetector;
    private readonly float[] _ringBuffer;
    private readonly float[] _windowBuffer;
    
    private int _head; 
    private int _tail; 
    private int _count;
    
    private readonly Lock _stateLock = new();
    private readonly float _speechThreshold;
    private readonly int _minSilenceDurationMs;
    private long _lastSpeechTimestamp;

    public SpeechDetector(IVadDetector vadDetector, VadOptions vadOptions)
    {
        _vadDetector = vadDetector;
        _speechThreshold = vadOptions.Threshold;
        _minSilenceDurationMs = vadOptions.MinSilenceDurationMs;

        _ringBuffer = new float[vadDetector.WindowSize * 32];
        _windowBuffer = new float[vadDetector.WindowSize];
    }

    /// <summary>
    /// Appends <paramref name="samples"/> to the internal ring buffer. Thread-safe.
    /// The oldest samples are silently overwritten when the buffer is full.
    /// </summary>
    /// <param name="samples">Normalised float PCM samples at the VAD sampling rate.</param>
    public void ProcessBatch(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;
        lock (_stateLock)
        {
            int remaining = samples.Length;
            int offset = 0;
            while (remaining > 0)
            {
                int spaceToEnd = _ringBuffer.Length - _head;
                int toCopy = Math.Min(remaining, spaceToEnd);
                samples.Slice(offset, toCopy).CopyTo(_ringBuffer.AsSpan(_head));
                
                _head = (_head + toCopy) % _ringBuffer.Length;
                offset += toCopy;
                remaining -= toCopy;
            }
            _count = Math.Min(_ringBuffer.Length, _count + samples.Length);
        }
    }

    /// <summary>
    /// Drains the ring buffer in VAD-window-sized chunks, running inference on each window.
    /// Updates the last-speech timestamp when a speech window is found, and checks for the
    /// silence timeout when recording is active.
    /// </summary>
    /// <param name="isRecording">
    /// <c>true</c> when the pipeline is currently recording, enabling silence timeout detection.
    /// </param>
    /// <returns>The <see cref="SpeechResult"/> for this processing cycle.</returns>
    public SpeechResult ProcessBuffer(bool isRecording)
    {
        bool speechDetectedThisChunk = false;
        int windowSize = _vadDetector.WindowSize;
        
        while (true)
        {
            lock (_stateLock)
            {
                if (_count < windowSize) break;

                int spaceToEnd = _ringBuffer.Length - _tail;
                if (windowSize <= spaceToEnd)
                {
                    _ringBuffer.AsSpan(_tail, windowSize).CopyTo(_windowBuffer);
                }
                else
                {
                    _ringBuffer.AsSpan(_tail, spaceToEnd).CopyTo(_windowBuffer);
                    _ringBuffer.AsSpan(0, windowSize - spaceToEnd).CopyTo(_windowBuffer.AsSpan(spaceToEnd));
                }
                
                _tail = (_tail + windowSize) % _ringBuffer.Length;
                _count -= windowSize;
            }

            float prob = _vadDetector.Process(_windowBuffer);
            if (prob > _speechThreshold)
            {
                Interlocked.Exchange(ref _lastSpeechTimestamp, Stopwatch.GetTimestamp());
                speechDetectedThisChunk = true;
            }
        }
        
        if (speechDetectedThisChunk)
            return SpeechResult.SpeechDetected;
        
        long lastSpeech = Interlocked.Read(ref _lastSpeechTimestamp);

        if (isRecording && lastSpeech != 0)
        {
            if (Stopwatch.GetElapsedTime(lastSpeech).TotalMilliseconds > _minSilenceDurationMs)
            {
                Interlocked.Exchange(ref _lastSpeechTimestamp, 0); 
                return SpeechResult.SilenceTimeout;
            }
        }

        return SpeechResult.None;
    }

    /// <summary>
    /// Clears the ring buffer, resets the last-speech timestamp, and resets the underlying
    /// <see cref="IVadDetector"/> hidden state. Should be called after each utterance is finalised.
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
            Array.Clear(_ringBuffer);
            _lastSpeechTimestamp = 0;
        }

        _vadDetector.Reset();
    }
}
