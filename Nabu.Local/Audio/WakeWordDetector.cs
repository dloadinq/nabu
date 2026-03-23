using NanoWakeWord;

namespace Nabu.Local.Audio;

public class WakeWordDetector : IDisposable
{
    private readonly WakeWordRuntime _wakeWordRuntime;
    private readonly List<short> _wakeWordBuffer = new();
    private readonly object _stateLock = new();

    public WakeWordDetector(WakeWordRuntime wakeWordRuntime)
    {
        _wakeWordRuntime = wakeWordRuntime;
    }

    public void Process(short sample)
    {
        lock (_stateLock)
        {
            _wakeWordBuffer.Add(sample);
        }
    }

    public void ProcessBatch(ReadOnlySpan<short> samples)
    {
        if (samples.Length == 0) return;
        lock (_stateLock)
        {
            foreach (var s in samples)
                _wakeWordBuffer.Add(s);
        }
    }

    public bool ProcessBuffer()
    {
        short[]? samples = null;
        lock (_stateLock)
        {
            if (_wakeWordBuffer.Count > 0)
            {
                samples = _wakeWordBuffer.ToArray();
                _wakeWordBuffer.Clear();
            }
        }

        if (samples != null && samples.Length > 0)
        {
            int detectedIndex = _wakeWordRuntime.Process(samples);
            if (detectedIndex >= 0)
                return true;
        }

        return false;
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _wakeWordBuffer.Clear();
            _wakeWordBuffer.TrimExcess();
        }
    }

    public void Dispose()
    {
    }
}