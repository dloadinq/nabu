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

    private readonly Lock _stateLock = new();

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int PreRollSeconds = 2;
    private const int MaxPreRollBytes = SampleRate * 2 * Channels * PreRollSeconds;

    private readonly byte[] _preRollBuffer = new byte[MaxPreRollBytes];
    private int _preRollHead = 0;
    private int _preRollCount = 0;

    /// <summary>
    /// Appends a PCM chunk to the circular pre-roll ring. Oldest chunks are evicted automatically
    /// when the pre-roll window (<see cref="PreRollSeconds"/> seconds) is exceeded.
    /// The <paramref name="buffer"/> is copied internally; the caller retains ownership of the original.
    /// </summary>
    /// <param name="buffer">Byte array containing raw PCM-16 audio.</param>
    /// <param name="length">Number of valid bytes to copy from <paramref name="buffer"/>.</param>
    public void ProcessPreRoll(byte[] buffer, int length)
    {
        lock (_stateLock)
        {
            if (length >= MaxPreRollBytes)
            {
                int startOffset = length - MaxPreRollBytes;
                Buffer.BlockCopy(buffer, startOffset, _preRollBuffer, 0, MaxPreRollBytes);
                _preRollHead = 0;
                _preRollCount = MaxPreRollBytes;
                return;
            }

            int tail = (_preRollHead + _preRollCount) % MaxPreRollBytes;
            int spaceToEnd = MaxPreRollBytes - tail;

            if (length <= spaceToEnd)
            {
                Buffer.BlockCopy(buffer, 0, _preRollBuffer, tail, length);
            }
            else
            {
                Buffer.BlockCopy(buffer, 0, _preRollBuffer, tail, spaceToEnd);
                Buffer.BlockCopy(buffer, spaceToEnd, _preRollBuffer, 0, length - spaceToEnd);
            }

            if (_preRollCount + length > MaxPreRollBytes)
            {
                _preRollCount = MaxPreRollBytes;
                _preRollHead = (_preRollHead + length) % MaxPreRollBytes;
            }
            else
            {
                _preRollCount += length;
            }
        }
    }

    /// <summary>
    /// Clears the pre-roll ring buffer immediately.
    /// Called when the wake word is detected but the subsequent speech should not include the pre-roll.
    /// </summary>
    public void DiscardPreRoll()
    {
        lock (_stateLock)
        {
            _preRollHead = 0;
            _preRollCount = 0;
        }
    }

    /// <summary>
    /// Begins a new recording session by initialising the WAV recording stream,
    /// then prepends all buffered pre-roll chunks. The pre-roll ring is cleared after the flush.
    /// </summary>
    public void StartRecording()
    {
        lock (_stateLock)
        {
            _recordingStream = new MemoryStream();
            _waveWriter = new WavFileBuilder(_recordingStream);

            if (_preRollCount > 0)
            {
                if (_preRollHead + _preRollCount <= MaxPreRollBytes)
                {
                    _waveWriter.Write(_preRollBuffer, _preRollHead, _preRollCount);
                }
                else
                {
                    int firstPart = MaxPreRollBytes - _preRollHead;
                    _waveWriter.Write(_preRollBuffer, _preRollHead, firstPart);
                    _waveWriter.Write(_preRollBuffer, 0, _preRollCount - firstPart);
                }
            }
            
            _preRollHead = 0;
            _preRollCount = 0;
        }
    }

    /// <summary>
    /// Appends a PCM chunk to the full recording stream.
    /// Should only be called while a session is active (after <see cref="StartRecording"/>).
    /// </summary>
    /// <param name="buffer">Byte array containing raw PCM-16 audio.</param>
    /// <param name="length">Number of valid bytes to write.</param>
    public void RecordChunk(byte[] buffer, int length)
    {
        lock (_stateLock)
        {
            _waveWriter?.Write(buffer, 0, length);
        }
    }

    /// <summary>
    /// Returns a read-only snapshot copy of the current recording data as a WAV <see cref="MemoryStream"/>
    /// suitable for passing to Whisper for live preview transcription.
    /// Returns <c>null</c> if no recording is in progress.
    /// </summary>
    public Task<MemoryStream?> CreatePreviewStreamAsync()
    {
        lock (_stateLock)
        {
            if (_waveWriter == null || _recordingStream == null)
                return Task.FromResult<MemoryStream?>(null);

            _waveWriter.Flush();
            
            var length = (int)_recordingStream.Length;
            var bufferCopy = new byte[length];
            Buffer.BlockCopy(_recordingStream.GetBuffer(), 0, bufferCopy, 0, length);
            
            return Task.FromResult<MemoryStream?>(new MemoryStream(bufferCopy, 0, length, false));
        }
    }

    /// <summary>
    /// Stops the active recording session, finalises the WAV header, and returns the complete recording
    /// as a read-only <see cref="MemoryStream"/>.
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
        }

        await writer.FlushAsync();

        var buf = recordingStream.GetBuffer();
        var len = (int)recordingStream.Length;

        await writer.DisposeAsync();
        recordingStream.Dispose();

        var result = new MemoryStream(len);
        result.Write(buf, 0, len);
        result.Position = 0;
        return result;
    }
    
    public void Dispose()
    {
        DiscardPreRoll();
        lock (_stateLock)
        {
            _waveWriter?.Dispose();
            _recordingStream?.Dispose();
        }
    }
}
