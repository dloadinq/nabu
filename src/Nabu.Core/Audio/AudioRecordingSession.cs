using System.Buffers;

namespace Nabu.Core.Audio;

/// <summary>
/// Manages the audio buffers for a single voice recording session, including a circular pre-roll
/// buffer that captures audio before the wake word is detected.
/// </summary>
/// <remarks>
/// All public methods are thread-safe via an internal lock.
/// Pre-roll audio is prepended to the recording when <see cref="StartRecording"/> is called,
/// ensuring that speech immediately following the wake word is not lost.
/// </remarks>
public class AudioRecordingSession : IDisposable
{
    private MemoryStream? _recordingStream;
    private WavFileBuilder? _waveWriter;

    private MemoryStream? _previewStream;
    private WavFileBuilder? _previewWriter;

    private readonly Lock _stateLock = new();

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int PreRollSeconds = 2;
    private const int MaxPreRollBytes = SampleRate * 2 * Channels * PreRollSeconds;

    private const int PreRollSlots = 128;
    private readonly (byte[] Buffer, int Length)[] _preRoll = new (byte[], int)[PreRollSlots];
    private int _preRollHead;
    private int _preRollCount;
    private int _currentPreRollBytes;
    
    private const int MaxRecordingSeconds = 60;
    private const int MaxRecordingBytes = SampleRate * 2 * Channels * MaxRecordingSeconds; 

    /// <summary>
    /// Appends a PCM chunk to the circular pre-roll ring. Oldest chunks are evicted automatically
    /// when the pre-roll window (<see cref="PreRollSeconds"/> seconds) is exceeded.
    /// The <paramref name="buffer"/> is copied internally; the caller retains ownership of the original.
    /// </summary>
    /// <param name="buffer">Byte array containing raw PCM-16 audio.</param>
    /// <param name="length">Number of valid bytes to copy from <paramref name="buffer"/>.</param>
    public void ProcessPreRoll(byte[] buffer, int length)
    {
        var owned = ArrayPool<byte>.Shared.Rent(length);
        Buffer.BlockCopy(buffer, 0, owned, 0, length);

        lock (_stateLock)
        {
            while (_preRollCount > 0 && _currentPreRollBytes + length > MaxPreRollBytes)
            {
                ref var old = ref _preRoll[_preRollHead];
                ArrayPool<byte>.Shared.Return(old.Buffer);
                _currentPreRollBytes -= old.Length;
                old = default;
                _preRollHead = (_preRollHead + 1) % PreRollSlots;
                _preRollCount--;
            }
            
            if (_preRollCount >= PreRollSlots)
            {
                ArrayPool<byte>.Shared.Return(_preRoll[_preRollHead].Buffer);
                _currentPreRollBytes -= _preRoll[_preRollHead].Length;
                _preRoll[_preRollHead] = default;
                _preRollHead = (_preRollHead + 1) % PreRollSlots;
                _preRollCount--;
            }

            int tail = (_preRollHead + _preRollCount) % PreRollSlots;
            _preRoll[tail] = (owned, length);
            _preRollCount++;
            _currentPreRollBytes += length;
        }
    }

    /// <summary>
    /// Clears the pre-roll ring buffer and returns all pooled buffers to <see cref="System.Buffers.ArrayPool{T}.Shared"/>.
    /// Called when the wake word is detected but the subsequent speech should not include the pre-roll
    /// (e.g., the wake word utterance itself).
    /// </summary>
    public void DiscardPreRoll()
    {
        lock (_stateLock)
        {
            for (int i = 0; i < _preRollCount; i++)
            {
                ref var chunk = ref _preRoll[(_preRollHead + i) % PreRollSlots];
                ArrayPool<byte>.Shared.Return(chunk.Buffer);
                chunk = default;
            }
            _preRollHead = 0;
            _preRollCount = 0;
            _currentPreRollBytes = 0;
        }
    }

    /// <summary>
    /// Begins a new recording session by initialising the WAV recording and preview streams,
    /// then prepends all buffered pre-roll chunks. The pre-roll ring is cleared after the flush.
    /// </summary>
    public void StartRecording()
    {
        lock (_stateLock)
        {
            _recordingStream = new MemoryStream(MaxRecordingBytes);
            _waveWriter = new WavFileBuilder(_recordingStream);

            _previewStream = new MemoryStream(MaxRecordingBytes);
            _previewWriter = new WavFileBuilder(_previewStream);

            for (int i = 0; i < _preRollCount; i++)
            {
                ref var chunk = ref _preRoll[(_preRollHead + i) % PreRollSlots];
                _waveWriter.Write(chunk.Buffer, 0, chunk.Length);
                _previewWriter.Write(chunk.Buffer, 0, chunk.Length);
                ArrayPool<byte>.Shared.Return(chunk.Buffer);
                chunk = default;
            }
            _preRollHead = 0;
            _preRollCount = 0;
            _currentPreRollBytes = 0;
        }
    }

    /// <summary>
    /// Appends a PCM chunk to both the full recording stream and the live preview stream.
    /// Should only be called while a session is active (after <see cref="StartRecording"/>).
    /// </summary>
    /// <param name="buffer">Byte array containing raw PCM-16 audio.</param>
    /// <param name="length">Number of valid bytes to write.</param>
    public void RecordChunk(byte[] buffer, int length)
    {
        lock (_stateLock)
        {
            _waveWriter?.Write(buffer, 0, length);
            _previewWriter?.Write(buffer, 0, length);
        }
    }

    /// <summary>
    /// Returns a read-only snapshot of the current recording data as a WAV <see cref="MemoryStream"/>
    /// suitable for passing to Whisper for live preview transcription.
    /// Returns <c>null</c> if no recording is in progress.
    /// </summary>
    /// <remarks>The returned stream shares the underlying buffer; it must not be written to.</remarks>
    public Task<MemoryStream?> CreatePreviewStreamAsync()
    {
        lock (_stateLock)
        {
            if (_previewWriter == null || _previewStream == null)
                return Task.FromResult<MemoryStream?>(null);

            _previewWriter.Flush();
            
            var buffer = _previewStream.GetBuffer();
            var length = (int)_previewStream.Length;
            return Task.FromResult<MemoryStream?>(new MemoryStream(buffer, 0, length, false));
        }
    }

    /// <summary>
    /// Stops the active recording session, finalises the WAV header, and returns the complete recording
    /// as a read-only <see cref="MemoryStream"/>. The preview stream is disposed.
    /// Returns <c>null</c> if no recording was active.
    /// </summary>
    /// <remarks>The caller is responsible for disposing the returned stream.</remarks>
    public async Task<MemoryStream?> StopAndGetStreamAsync()
    {
        WavFileBuilder? writer;
        MemoryStream? recordingStream;

        lock (_stateLock)
        {
            if (_waveWriter == null || _recordingStream == null) return null;

            writer = _waveWriter;
            recordingStream = _recordingStream;
            _waveWriter = null;
            _recordingStream = null;

            _previewWriter?.Dispose();
            _previewWriter = null;
            _previewStream?.Dispose();
            _previewStream = null;
        }

        await writer.FlushAsync();

        var buf = recordingStream.GetBuffer();
        var len = (int)recordingStream.Length;

        await writer.DisposeAsync();
        recordingStream.Dispose();
        return new MemoryStream(buf, 0, len, false);
    }
    
    public void Dispose()
    {
        DiscardPreRoll();
        lock (_stateLock)
        {
            _waveWriter?.Dispose();
            _previewWriter?.Dispose();
            _recordingStream?.Dispose();
            _previewStream?.Dispose();
        }
    }
}
