using System.Buffers;
using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Nabu.Maui;

/// <summary>
/// Captures microphone audio via WASAPI, converts it to 16 kHz mono PCM-16,
/// and forwards pooled buffers to the pipeline. The pipeline takes ownership
/// of each rented buffer and returns it to <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
internal sealed class WindowsAudioCapture : IDisposable
{
    private readonly Func<byte[], int, Task> _onAudioData;
    private WasapiCapture? _capture;
    private WaveFormat? _sourceFormat;

    private const int TargetSampleRate = 16000;

    public WindowsAudioCapture(Func<byte[], int, Task> onAudioData)
    {
        _onAudioData = onAudioData;
    }

    public void Start()
    {
        _capture = new WasapiCapture();
        _sourceFormat = _capture.WaveFormat;
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
    }

    private async void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _sourceFormat == null) return;
        try
        {
            var floatMono = ToFloatMono(e.Buffer, e.BytesRecorded, _sourceFormat);
            var resampled = Resample(floatMono, _sourceFormat.SampleRate, TargetSampleRate);

            // Rent from pool — AudioProcessingPipeline returns it after processing
            int byteCount = resampled.Length * 2;
            var pooled = ArrayPool<byte>.Shared.Rent(byteCount);
            WritePcm16(resampled, pooled);
            await _onAudioData(pooled, byteCount);
        }
        catch
        {
            // Must not propagate from async void event handler
        }
    }

    /// <summary>Converts any PCM/float WASAPI buffer to float mono samples.</summary>
    private static float[] ToFloatMono(byte[] buffer, int length, WaveFormat fmt)
    {
        bool isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat;
        int bytesPerSample = fmt.BitsPerSample / 8;
        int channels = fmt.Channels;
        int frameBytes = bytesPerSample * channels;
        int frameCount = length / frameBytes;
        var result = new float[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                int offset = i * frameBytes + ch * bytesPerSample;
                sum += isFloat
                    ? BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(offset))
                    : BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset)) / 32768f;
            }
            result[i] = sum / channels;
        }

        return result;
    }

    /// <summary>Linear interpolation resample — sufficient quality for speech recognition.</summary>
    private static float[] Resample(float[] input, int inputRate, int outputRate)
    {
        if (inputRate == outputRate) return input;

        int outputLen = (int)((long)input.Length * outputRate / inputRate);
        var output = new float[outputLen];
        double ratio = (double)inputRate / outputRate;

        for (int i = 0; i < outputLen; i++)
        {
            double pos = i * ratio;
            int idx = (int)pos;
            double frac = pos - idx;
            float s0 = input[Math.Min(idx, input.Length - 1)];
            float s1 = input[Math.Min(idx + 1, input.Length - 1)];
            output[i] = (float)(s0 * (1.0 - frac) + s1 * frac);
        }

        return output;
    }

    private static void WritePcm16(float[] samples, byte[] dest)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)(Math.Clamp(samples[i], -1f, 1f) * 32767f);
            BinaryPrimitives.WriteInt16LittleEndian(dest.AsSpan(i * 2), s);
        }
    }

    public void Stop() => _capture?.StopRecording();

    public void Dispose()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
    }
}
