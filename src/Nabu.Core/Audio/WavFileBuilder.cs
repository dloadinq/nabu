namespace Nabu.Core.Audio;

/// <summary>
/// Writes a valid RIFF/WAV file into a <see cref="MemoryStream"/>, targeting 16 kHz mono 16-bit PCM:
/// The format expected by Whisper.
/// The WAV header is written at construction time with a zero data length; call <see cref="Flush"/> or
/// <see cref="FlushAsync"/> to patch the header with the actual byte count before reading the stream.
/// </summary>
internal sealed class WavFileBuilder : IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _stream;
    private bool _disposed;

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int ByteRate = SampleRate * Channels * BitsPerSample / 8;
    private const int BlockAlign = Channels * BitsPerSample / 8;
    private const int HeaderSize = 44;

    /// <summary>
    /// Initialises the builder and writes a placeholder WAV header to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="MemoryStream"/> to write into. Must be writable and positioned at the start.</param>
    public WavFileBuilder(MemoryStream stream)
    {
        _stream = stream;
        WriteHeader(0);
    }

    /// <summary>Appends raw PCM bytes to the WAV data section.</summary>
    /// <param name="buffer">Source byte array.</param>
    /// <param name="offset">Zero-based byte offset into <paramref name="buffer"/>.</param>
    /// <param name="count">Number of bytes to write.</param>
    public void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    /// <summary>
    /// Updates the RIFF chunk size and data sub-chunk size fields in the WAV header to reflect the
    /// number of PCM bytes written so far. Must be called before the stream is consumed by Whisper.
    /// </summary>
    public void Flush()
    {
        var dataBytes = (int)_stream.Length - HeaderSize;
        var savedPos = _stream.Position;

        _stream.Position = 4;
        WriteInt32(36 + dataBytes);
        _stream.Position = 40;
        WriteInt32(dataBytes);

        _stream.Position = savedPos;
    }

    public Task FlushAsync()
    {
        Flush();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Flush();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
    
    private void WriteHeader(int dataBytes)
    {
        WriteAscii("RIFF");
        WriteInt32(36 + dataBytes);
        WriteAscii("WAVE");
        WriteAscii("fmt ");
        WriteInt32(16);
        WriteInt16(1);
        WriteInt16(Channels);
        WriteInt32(SampleRate);
        WriteInt32(ByteRate);
        WriteInt16(BlockAlign);
        WriteInt16(BitsPerSample);
        WriteAscii("data");
        WriteInt32(dataBytes);
    }

    private void WriteAscii(string text)
    {
        foreach (var c in text)
            _stream.WriteByte((byte)c);
    }

    private void WriteInt16(int value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private void WriteInt32(int value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)((value >> 8) & 0xFF));
        _stream.WriteByte((byte)((value >> 16) & 0xFF));
        _stream.WriteByte((byte)((value >> 24) & 0xFF));
    }
}