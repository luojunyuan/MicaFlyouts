using MicaFlyouts.App;
using MicaFlyouts.Domain.Volume;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;

namespace MicaFlyouts.Infrastructure.Audio;

public sealed partial class AudioService : IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly VolumeStore _store;
    private readonly UiDispatcher _dispatcher;
    private readonly AppLogger _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _pollCancellation;
    private CoreAudioDeviceEnumerator? _enumerator;
    private CoreAudioDevice? _endpoint;
    private DateTime _nextEnumeratorRetryUtc;
    private int _disposed;

    public AudioService(ISettingsStore settings, VolumeStore store, UiDispatcher dispatcher, AppLogger logger)
    {
        _settings = settings;
        _store = store;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public VolumeSnapshot Snapshot => _store.Snapshot;

    public void Start()
    {
        if (_pollCancellation is not null || Volatile.Read(ref _disposed) != 0)
            return;
        _pollCancellation = new CancellationTokenSource();
        _ = PollAsync(_pollCancellation.Token);
    }

    public void SetMasterVolume(float volume)
    {
        volume = Math.Clamp(volume, 0, 1);
        lock (_gate)
        {
            try
            {
                if (EnsureEndpoint())
                    _ = _endpoint?.TrySetMasterVolume(volume);
            }
            catch (Exception exception) { _logger.Warn($"Could not set master volume: {exception.Message}"); }
        }
        Refresh();
    }

    public void ToggleMasterMute()
    {
        lock (_gate)
        {
            try
            {
                if (EnsureEndpoint())
                    _ = _endpoint?.TryToggleMasterMute();
            }
            catch (Exception exception) { _logger.Warn($"Could not toggle master mute: {exception.Message}"); }
        }
        Refresh();
    }

    public void SetApplicationVolume(string sessionId, float volume)
    {
        volume = Math.Clamp(volume, 0, 1);
        lock (_gate)
        {
            try
            {
                if (EnsureEndpoint())
                    _ = _endpoint?.TrySetApplicationVolume(sessionId, volume);
            }
            catch (Exception exception) { _logger.Warn($"Could not update audio session: {exception.Message}"); }
        }
        Refresh();
    }

    public void ToggleApplicationMute(string sessionId)
    {
        lock (_gate)
        {
            try
            {
                if (EnsureEndpoint())
                    _ = _endpoint?.TryToggleApplicationMute(sessionId);
            }
            catch (Exception exception) { _logger.Warn($"Could not update audio session: {exception.Message}"); }
        }
        Refresh();
    }

    public void SetNativeOsdSuppressed(bool suppressed)
        => UiDispatcher.EnqueueOrRun(() => _store.Update(snapshot => snapshot with { IsNativeOsdSuppressed = suppressed }));

    public void Refresh()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _ = RefreshAsync();
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(750));
        Refresh();
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                Refresh();
        }
        catch (OperationCanceledException)
        {
            _logger.Info("AudioService.PollAsync stopped after OperationCanceledException.");
        }
    }

    private async Task RefreshAsync()
    {
        await Task.Yield();
        var snapshot = ReadSnapshot();
        UiDispatcher.EnqueueOrRun(() => _store.Set(snapshot));
    }

    private VolumeSnapshot ReadSnapshot()
    {
        lock (_gate)
        {
            try
            {
                if (!EnsureEndpoint() || _endpoint is null)
                    return VolumeSnapshot.Empty;

                if (_endpoint.TryReadSnapshot(_store.Snapshot.IsNativeOsdSuppressed, out var snapshot))
                    return snapshot;

                _endpoint.Dispose();
                _endpoint = null;
                _nextEnumeratorRetryUtc = DateTime.UtcNow.AddSeconds(5);
                return VolumeSnapshot.Empty;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Audio device refresh failed: {exception.Message}");
                return VolumeSnapshot.Empty;
            }
        }
    }

    private bool EnsureEndpoint()
    {
        if (_endpoint is not null)
            return true;

        if (DateTime.UtcNow < _nextEnumeratorRetryUtc)
            return false;

        try
        {
            // Keep COM activation out of construction and app startup. On some
            // NativeAOT machines the audio endpoint may not be available yet.
            if (_enumerator is null)
            {
                if (!CoreAudioDeviceEnumerator.TryCreate(out var created) || created is null)
                    throw new InvalidOperationException("Core Audio is unavailable.");
                _enumerator = created;
            }

            if (!_enumerator.TryGetDefaultRenderDevice(out _endpoint) || _endpoint is null)
                throw new InvalidOperationException("No default render endpoint is available.");

            _nextEnumeratorRetryUtc = default;
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Audio endpoint initialization failed: {exception.Message}");
            _endpoint?.Dispose();
            _endpoint = null;
            _nextEnumeratorRetryUtc = DateTime.UtcNow.AddSeconds(5);
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _pollCancellation?.Cancel();
        _pollCancellation?.Dispose();
        lock (_gate)
        {
            _endpoint?.Dispose();
            _endpoint = null;
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }
}
