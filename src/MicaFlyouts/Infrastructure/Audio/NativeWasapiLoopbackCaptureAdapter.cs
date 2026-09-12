using System.Runtime.CompilerServices;
using MicaFlyouts.Infrastructure.Logging;

namespace MicaFlyouts.Infrastructure.Audio;

/// <summary>
/// WASAPI loopback capture implemented through the source-generated Core Audio projection.
/// </summary>
internal sealed unsafe partial class NativeWasapiLoopbackCaptureAdapter(AppLogger? logger = null) : IAudioLoopbackCapture
{
    private const int SharedAudioStream = 0;
    private const uint StreamLoopback = 0x00020000;
    private const uint SilentBuffer = 0x00000002;
    private const long BufferDuration = 10_000_000;
    private static readonly Guid AudioClientId = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientId = new("C8ADBD64-E71E-48A0-A4DE-185C3950CDEB");

    private readonly AppLogger? _logger = logger;
    private CoreAudioDeviceEnumerator? _enumerator;
    private CoreAudioDevice? _device;
    private ICoreAudioClient? _client;
    private ICoreAudioCaptureClient? _capture;
    private CancellationTokenSource? _cancellation;
    private Task? _captureTask;
    private int _channels;
    private int _sampleRate;
    private int _bytesPerSample;
    private bool _isFloat;
    private int _disposed;

    public event Action<ReadOnlyMemory<float>, int>? SamplesAvailable;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (_captureTask is not null)
            return;

        if (!CoreAudioDeviceEnumerator.TryCreate(out _enumerator)
            || _enumerator is null
            || !_enumerator.TryGetDefaultRenderDevice(out _device)
            || _device is null
            || !_device.TryActivate(AudioClientId, out _client)
            || _client is null)
            throw new InvalidOperationException("WASAPI loopback is unavailable.");

        nint formatPointer = 0;
        try
        {
            if (_client.GetMixFormat(out formatPointer) < 0 || formatPointer == 0)
                throw new InvalidOperationException("The default audio format is unavailable.");

            var format = Unsafe.ReadUnaligned<CoreAudioWaveFormat>((void*)formatPointer);
            _channels = Math.Max(1, (int)format.Channels);
            _sampleRate = checked((int)format.SamplesPerSecond);
            _bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
            _isFloat = format.FormatTag == 3 || format.BitsPerSample == 32;

            Guid sessionId = Guid.Empty;
            CoreAudioNative.ThrowIfFailed(_client.Initialize(
                SharedAudioStream,
                StreamLoopback,
                BufferDuration,
                0,
                formatPointer,
                in sessionId));
            CoreAudioNative.ThrowIfFailed(_client.GetService(in CaptureClientId, out nint capturePointer));
            _capture = CoreAudioNative.Wrap<ICoreAudioCaptureClient>(capturePointer)
                ?? throw new InvalidOperationException("The WASAPI capture service is unavailable.");
            CoreAudioNative.ThrowIfFailed(_client.Start());
        }
        finally
        {
            if (formatPointer != 0)
                System.Runtime.InteropServices.Marshal.FreeCoTaskMem(formatPointer);
        }

        _cancellation = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoop(_cancellation.Token));
    }

    private void CaptureLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_capture is null)
                    return;

                if (_capture.GetNextPacketSize(out uint packetFrames) < 0)
                    return;
                if (packetFrames == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (_capture.GetBuffer(
                        out nint data,
                        out uint frames,
                        out uint flags,
                        out _,
                        out _) < 0)
                    return;

                try
                {
                    var samples = new float[checked((int)frames)];
                    if ((flags & SilentBuffer) == 0 && data != 0)
                        CopySamples(data, frames, samples);
                    SamplesAvailable?.Invoke(samples, _sampleRate);
                }
                finally
                {
                    _ = _capture.ReleaseBuffer(frames);
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Warn($"WASAPI loopback stopped: {exception.Message}");
        }
    }

    private void CopySamples(nint data, uint frames, float[] destination)
    {
        int frameCount = checked((int)frames);
        int frameSize = checked(_channels * _bytesPerSample);
        var source = new ReadOnlySpan<byte>((void*)data, checked(frameCount * frameSize));
        for (int frame = 0; frame < frameCount; frame++)
        {
            double total = 0;
            int frameOffset = frame * frameSize;
            for (int channel = 0; channel < _channels; channel++)
                total += ReadSample(source, frameOffset + channel * _bytesPerSample);
            destination[frame] = (float)(total / _channels);
        }
    }

    private double ReadSample(ReadOnlySpan<byte> source, int offset)
    {
        if (_isFloat && _bytesPerSample == 4)
            return BitConverter.ToSingle(source.Slice(offset, 4));
        if (_bytesPerSample == 2)
            return (short)(source[offset] | source[offset + 1] << 8) / 32768d;
        if (_bytesPerSample == 3)
        {
            int value = source[offset] | source[offset + 1] << 8 | source[offset + 2] << 16;
            if ((value & 0x00800000) != 0)
                value |= unchecked((int)0xFF000000);
            return value / 8388608d;
        }
        if (_bytesPerSample == 4)
            return BitConverter.ToInt32(source.Slice(offset, 4)) / 2147483648d;
        return 0;
    }

    public void StopCapture()
    {
        _cancellation?.Cancel();
        if (_client is not null)
            _ = _client.Stop();
        try { _captureTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _captureTask = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        StopCapture();
        CoreAudioNative.Release(_capture);
        _capture = null;
        CoreAudioNative.Release(_client);
        _client = null;
        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
    }
}
