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
public sealed class WasapiLoopbackCaptureAdapter : IAudioLoopbackCapture
{
    private readonly WasapiLoopbackCapture _capture;
    private int _disposed;

    public WasapiLoopbackCaptureAdapter()
    {
        _capture = new WasapiLoopbackCapture();
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

    private void Capture_DataAvailable(object? sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var format = _capture.WaveFormat;
        int channels = Math.Max(1, format.Channels);
        int bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        int frameCount = args.BytesRecorded / Math.Max(1, bytesPerSample * channels);
        var samples = new float[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            double total = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                int offset = (frame * channels + channel) * bytesPerSample;
                total += ReadSample(args.Buffer, offset, format);
            }
            samples[frame] = (float)(total / channels);
        }
        SamplesAvailable?.Invoke(samples, format.SampleRate);
    }

    private static double ReadSample(byte[] buffer, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            return BitConverter.ToSingle(buffer, offset);
        if (format.BitsPerSample == 16)
            return BitConverter.ToInt16(buffer, offset) / 32768d;
        if (format.BitsPerSample == 24)
        {
            int value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16;
            if ((value & 0x00800000) != 0)
                value |= unchecked((int)0xFF000000);
            return value / 8388608d;
        }
        if (format.BitsPerSample == 32)
            return BitConverter.ToInt32(buffer, offset) / 2147483648d;
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
