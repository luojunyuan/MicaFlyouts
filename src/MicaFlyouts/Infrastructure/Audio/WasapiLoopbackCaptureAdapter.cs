using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MicaFlyouts.Infrastructure.Audio;

public interface IAudioLoopbackCapture : IDisposable
{
    event Action<ReadOnlyMemory<float>, int>? SamplesAvailable;
    void Start();
    void StopCapture();
}

/// <summary>
/// NAudio is deliberately kept behind this adapter so no NAudio type crosses the
/// infrastructure boundary used by features and Reactor components.
/// </summary>
public sealed partial class WasapiLoopbackCaptureAdapter : IAudioLoopbackCapture
{
    private readonly WasapiRecorder _capture;
    private int _disposed;

    public WasapiLoopbackCaptureAdapter()
    {
        _capture = new WasapiRecorderBuilder()
            .WithLoopbackCapture()
            .Build();
        _capture.DataAvailable += Capture_DataAvailable;
    }

    public event Action<ReadOnlyMemory<float>, int>? SamplesAvailable;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _capture.StartRecording();
    }

    public void StopCapture()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _capture.StopRecording();
    }

    private void Capture_DataAvailable(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        long devicePosition,
        long qpcPosition)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var format = _capture.WaveFormat;
        int channels = Math.Max(1, format.Channels);
        int bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        int frameCount = buffer.Length / Math.Max(1, bytesPerSample * channels);
        var samples = new float[frameCount];
        if ((flags & AudioClientBufferFlags.Silent) == 0)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                double total = 0;
                for (int channel = 0; channel < channels; channel++)
                {
                    int offset = (frame * channels + channel) * bytesPerSample;
                    total += ReadSample(buffer, offset, format);
                }
                samples[frame] = (float)(total / channels);
            }
        }
        SamplesAvailable?.Invoke(samples, format.SampleRate);
    }

    private static double ReadSample(ReadOnlySpan<byte> buffer, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            return BitConverter.ToSingle(buffer.Slice(offset, sizeof(float)));
        if (format.BitsPerSample == 16)
            return BitConverter.ToInt16(buffer.Slice(offset, sizeof(short))) / 32768d;
        if (format.BitsPerSample == 24)
        {
            int value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16;
            if ((value & 0x00800000) != 0)
                value |= unchecked((int)0xFF000000);
            return value / 8388608d;
        }
        if (format.BitsPerSample == 32)
            return BitConverter.ToInt32(buffer.Slice(offset, sizeof(int))) / 2147483648d;
        return 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try { _capture.StopRecording(); } catch { }
        _capture.DataAvailable -= Capture_DataAvailable;
        _capture.Dispose();
    }
}
