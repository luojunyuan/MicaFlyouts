using System.Threading.Channels;
using MicaFlyouts.App;
using MicaFlyouts.Domain.Visualizer;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;

namespace MicaFlyouts.Infrastructure.Audio;

public sealed partial class VisualizerService : IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly VisualizerStore _store;
    private readonly UiDispatcher _dispatcher;
    private readonly AppLogger _logger;
    private readonly Func<IAudioLoopbackCapture> _captureFactory;
    private readonly Channel<VisualizerSnapshot> _frames = Channel.CreateBounded<VisualizerSnapshot>(
        new BoundedChannelOptions(2) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });
    private readonly FrameRateLimiter _limiter = new();
    private IAudioLoopbackCapture? _capture;
    private CancellationTokenSource? _cancellation;
    private float[] _previousBars = [];
    private Action? _settingsUnsubscribe;
    private int _disposed;

    public VisualizerService(
        ISettingsStore settings,
        VisualizerStore store,
        UiDispatcher dispatcher,
        AppLogger logger,
        Func<IAudioLoopbackCapture>? captureFactory = null)
    {
        _settings = settings;
        _store = store;
        _dispatcher = dispatcher;
        _logger = logger;
        _captureFactory = captureFactory ?? (static () => new NativeWasapiLoopbackCaptureAdapter());
    }

    public VisualizerSnapshot Snapshot => _store.Snapshot;

    public void Start()
    {
        if (_cancellation is not null || Volatile.Read(ref _disposed) != 0)
            return;
        _cancellation = new CancellationTokenSource();
        _settingsUnsubscribe = _settings.Subscribe(OnSettingsChanged);
        ConfigureCapture();
        _ = ConsumeFramesAsync(_cancellation.Token);
    }

    private void OnSettingsChanged()
        => UiDispatcher.EnqueueOrRun(ConfigureCapture);

    private void ConfigureCapture()
    {
        if (_settings.Snapshot.TaskbarVisualizerEnabled)
        {
            if (_capture is not null)
                return;
            try
            {
                _capture = _captureFactory();
                _capture.SamplesAvailable += Capture_SamplesAvailable;
                _capture.Start();
            }
            catch (Exception exception)
            {
                _logger.Warn($"Visualizer capture is unavailable: {exception.Message}");
                _capture?.Dispose();
                _capture = null;
                PublishEmpty();
            }
        }
        else
        {
            _capture?.StopCapture();
            _capture?.Dispose();
            _capture = null;
            PublishEmpty();
        }
    }

    private void Capture_SamplesAvailable(ReadOnlyMemory<float> samples, int sampleRate)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_limiter.ShouldPublish(DateTimeOffset.UtcNow))
            return;

        var settings = _settings.Snapshot;
        try
        {
            var bars = FftProcessor.ComputeBars(
                samples.Span,
                sampleRate,
                settings.TaskbarVisualizerBarCount,
                settings.TaskbarVisualizerAudioSensitivity,
                settings.TaskbarVisualizerAudioPeakLevel,
                _previousBars);
            _previousBars = bars;
            bool hasContent = bars.Any(static bar => bar > 0.02f);
            bool baseline = settings.TaskbarVisualizerBaseline
                && (!settings.TaskbarVisualizerBaselineAutoHide || hasContent);
            _frames.Writer.TryWrite(new VisualizerSnapshot(bars, baseline, hasContent, DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            _logger.Warn($"Visualizer frame processing failed: {exception.Message}");
        }
    }

    private async Task ConsumeFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                UiDispatcher.EnqueueOrRun(() => _store.Set(frame));
        }
        catch (OperationCanceledException)
        {
            _logger.Info("VisualizerService.ConsumeFramesAsync stopped after OperationCanceledException.");
        }
    }

    private void PublishEmpty()
    {
        _previousBars = [];
        UiDispatcher.EnqueueOrRun(() => _store.Set(VisualizerSnapshot.Empty));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _settingsUnsubscribe?.Invoke();
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _frames.Writer.TryComplete();
        if (_capture is not null)
        {
            _capture.SamplesAvailable -= Capture_SamplesAvailable;
            _capture.Dispose();
            _capture = null;
        }
    }
}
