using NAudio.Wave;

namespace Nabu.Local.Audio;

public class AudioRecordingSession
{
    private MemoryStream? _recordingStream;
    private WaveFileWriter? _waveWriter;

    private readonly LinkedList<byte[]> _preRollBuffer = new();
    private readonly List<byte[]> _rawChunks = new();
    private readonly object _stateLock = new();

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int PreRollSeconds = 2;
    private readonly int _maxPreRollBytes = SampleRate * 2 * Channels * PreRollSeconds;
    private int _currentPreRollBytes;

    public void ProcessPreRoll(byte[] rawBytes)
    {
        lock (_stateLock)
        {
            _preRollBuffer.AddLast(rawBytes);
            _currentPreRollBytes += rawBytes.Length;
            while (_currentPreRollBytes > _maxPreRollBytes)
            {
                var removed = _preRollBuffer.First!.Value;
                _preRollBuffer.RemoveFirst();
                _currentPreRollBytes -= removed.Length;
            }
        }
    }

    public void DiscardPreRoll()
    {
        lock (_stateLock)
        {
            _preRollBuffer.Clear();
            _currentPreRollBytes = 0;
        }
    }

    public void StartRecording()
    {
        _recordingStream = new MemoryStream();
        _waveWriter = new WaveFileWriter(_recordingStream, new WaveFormat(SampleRate, 16, 1));

        lock (_stateLock)
        {
            _rawChunks.Clear();
            foreach (var chunk in _preRollBuffer)
            {
                _waveWriter.Write(chunk, 0, chunk.Length);
                _rawChunks.Add(chunk);
            }

            _preRollBuffer.Clear();
            _currentPreRollBytes = 0;
        }
    }

    public void RecordChunk(byte[] chunk)
    {
        _waveWriter?.Write(chunk, 0, chunk.Length);
        lock (_stateLock)
        {
            _rawChunks.Add(chunk);
        }
    }

    public async Task<MemoryStream?> CreatePreviewStreamAsync()
    {
        List<byte[]> snapshot;
        lock (_stateLock)
        {
            if (_rawChunks.Count == 0) return null;
            snapshot = new List<byte[]>(_rawChunks);
        }

        var stream = new MemoryStream();
        var writer = new WaveFileWriter(stream, new WaveFormat(SampleRate, 16, 1));
        foreach (var chunk in snapshot)
        {
            writer.Write(chunk, 0, chunk.Length);
        }

        await writer.FlushAsync();
        await writer.DisposeAsync();

        var wavData = stream.ToArray();
        return new MemoryStream(wavData, false);
    }

    public async Task<MemoryStream?> StopAndGetStreamAsync()
    {
        if (_waveWriter == null || _recordingStream == null)
            return null;

        await _waveWriter.FlushAsync();
        await _waveWriter.DisposeAsync();
        _waveWriter = null;

        var wavData = _recordingStream.ToArray();
        _recordingStream = null;

        lock (_stateLock)
        {
            _rawChunks.Clear();
        }

        return new MemoryStream(wavData, false);
    }
}
