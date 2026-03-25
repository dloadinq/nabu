using Nabu.Core.Vad;

namespace Nabu.Core.Audio;

public enum SpeechResult
{
    None,
    SpeechDetected,
    SilenceTimeout
}

public class SpeechDetector
{
    private readonly IVadDetector _vadDetector;
    private readonly List<float> _vadBuffer = new();
    private readonly object _stateLock = new();
    private readonly float _speechThreshold;
    private readonly int _minSilenceDurationMs;

    private DateTime _lastSpeechTime = DateTime.MinValue;

    public event Action? SpeechDetected;
    public event Action? SilenceTimeoutDetected;

    public SpeechDetector(IVadDetector vadDetector, float speechThreshold = 0.65f, int minSilenceDurationMs = 1000)
    {
        _vadDetector = vadDetector;
        _speechThreshold = speechThreshold;
        _minSilenceDurationMs = minSilenceDurationMs;
    }

    public void Process(float sample)
    {
        lock (_stateLock)
        {
            _vadBuffer.Add(sample);
        }
    }

    /// <summary>
    /// Processes multiple samples in one lock. Prefer over Process() for bulk data.
    /// </summary>
    public void ProcessBatch(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return;
        lock (_stateLock)
        {
            foreach (var sample in samples)
                _vadBuffer.Add(sample);
        }
    }

    public SpeechResult ProcessBuffer(bool isRecording)
    {
        bool speechDetectedThisChunk = false;

        while (true)
        {
            float[] window;
            lock (_stateLock)
            {
                if (_vadBuffer.Count < _vadDetector.WindowSize)
                {
                    break;
                }

                window = _vadBuffer.GetRange(0, _vadDetector.WindowSize).ToArray();
                _vadBuffer.RemoveRange(0, _vadDetector.WindowSize);
            }

            float speechProbability = _vadDetector.Process(window);
            if (speechProbability > _speechThreshold)
            {
                _lastSpeechTime = DateTime.Now;
                speechDetectedThisChunk = true;
            }
        }

        if (speechDetectedThisChunk)
        {
            SpeechDetected?.Invoke();
            return SpeechResult.SpeechDetected;
        }

        if (isRecording && _lastSpeechTime != DateTime.MinValue)
        {
            var silenceDuration = DateTime.Now - _lastSpeechTime;
            if (silenceDuration.TotalMilliseconds > _minSilenceDurationMs)
            {
                SilenceTimeoutDetected?.Invoke();
                return SpeechResult.SilenceTimeout;
            }
        }

        return SpeechResult.None;
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _vadBuffer.Clear();
            _vadBuffer.TrimExcess();
        }

        _vadDetector.Reset();
        _lastSpeechTime = DateTime.MinValue;
    }
}
