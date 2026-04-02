using System.Runtime.InteropServices;
using Nabu.Inference.Kws;
using NanoWakeWord;

namespace Nabu.Core.Kws;

/// <summary>
/// Implements <see cref="IWakeWordDetector"/> using the NanoWakeWord <see cref="WakeWordRuntime"/>.
/// Audio samples are accumulated in an internal list and evaluated in bulk when
/// <see cref="ProcessBuffer"/> is called.
/// </summary>
public class WakeWordDetector(WakeWordRuntime wakeWordRuntime) : IWakeWordDetector, IDisposable
{
    private readonly List<short> _wakeWordBuffer = new(16000);
    private readonly Lock _stateLock = new();
    private short[] _processBuffer = Array.Empty<short>();

    /// <inheritdoc/>
    public void ProcessBatch(ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty) return;
        
        lock (_stateLock)
        {
            foreach (var s in samples)
                _wakeWordBuffer.Add(s);
        }
    }

    /// <inheritdoc/>
    public bool ProcessBuffer()
    {
        int count;
        lock (_stateLock)
        {
            count = _wakeWordBuffer.Count;
            if (count == 0) return false;

            if (count != _processBuffer.Length)
                _processBuffer = new short[count];

            CollectionsMarshal.AsSpan(_wakeWordBuffer).CopyTo(_processBuffer);
            _wakeWordBuffer.Clear();
        }

        return wakeWordRuntime.Process(_processBuffer) >= 0;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_stateLock)
            _wakeWordBuffer.Clear();
    }

    /// <summary>Disposes the underlying <see cref="WakeWordRuntime"/>.</summary>
    public void Dispose() => wakeWordRuntime.Dispose();
}