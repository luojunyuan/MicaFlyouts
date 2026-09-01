using MicaFlyouts.App;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using MicaFlyouts.Infrastructure.Windows;
using System.Runtime.CompilerServices;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MicaFlyouts.Infrastructure.Media;

using DomainMediaStatus = MicaFlyouts.Domain.Media.MediaPlaybackStatus;

public sealed class MediaSessionService : IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly MediaStore _store;
    private readonly UiDispatcher _dispatcher;
    private readonly FullscreenService _fullscreen;
    private readonly MediaPlayerResolver _resolver;
    private readonly AppLogger _logger;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _subscribedSessions = new(StringComparer.Ordinal);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Action? _settingsUnsubscribe;
    private int _started;
    private int _disposed;

    public MediaSessionService(
        ISettingsStore settings,
        MediaStore store,
        UiDispatcher dispatcher,
        FullscreenService fullscreen,
        MediaPlayerResolver resolver,
        AppLogger logger)
    {
        _settings = settings;
        _store = store;
        _dispatcher = dispatcher;
        _fullscreen = fullscreen;
        _resolver = resolver;
        _logger = logger;
    }

    public MediaSnapshot Snapshot => _store.Snapshot;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0 || Volatile.Read(ref _disposed) != 0)
            return;
        _settingsUnsubscribe = _settings.Subscribe(RefreshFromSettings);
        _ = InitializeAsync();
    }

    public async Task TogglePlayPauseAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = Snapshot.ActiveSession;
        if (snapshot is null)
            return;
        try
        {
            var session = GetNativeSession(snapshot.Id);
            var status = session.GetPlaybackInfo()?.PlaybackStatus;
            if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await session.TryPauseAsync().AsTask(cancellationToken).ConfigureAwait(false);
            else
                await session.TryPlayAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Play/pause failed: {exception.Message}");
        }
    }

    public Task SkipPreviousAsync(CancellationToken cancellationToken = default)
        => InvokeAsync(static session => session.TrySkipPreviousAsync().AsTask(), cancellationToken);

    public Task SkipNextAsync(CancellationToken cancellationToken = default)
        => InvokeAsync(static session => session.TrySkipNextAsync().AsTask(), cancellationToken);

    public Task SetRepeatAsync(int repeatMode, CancellationToken cancellationToken = default)
    {
        var mode = repeatMode switch
        {
            1 => MediaPlaybackAutoRepeatMode.Track,
            2 => MediaPlaybackAutoRepeatMode.List,
            _ => MediaPlaybackAutoRepeatMode.None,
        };
        return InvokeAsync(session => session.TryChangeAutoRepeatModeAsync(mode).AsTask(), cancellationToken);
    }

    public Task SetShuffleAsync(bool enabled, CancellationToken cancellationToken = default)
        => InvokeAsync(session => session.TryChangeShuffleActiveAsync(enabled).AsTask(), cancellationToken);

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        return InvokeAsync(session => session.TryChangePlaybackPositionAsync(position.Ticks).AsTask(), cancellationToken);
    }

    public bool ActivateFocusedPlayer()
    {
        var session = Snapshot.ActiveSession;
        return session is not null && _resolver.TryActivate(session.AppUserModelId, session.Track.Title);
    }

    public void Refresh()
        => _ = RefreshAsync();

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += Manager_SessionsChanged;
            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Media control is unavailable: {exception.Message}");
            Publish(MediaSnapshot.Empty);
        }
    }

    private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => _ = RefreshAsync();

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => _ = RefreshAsync();

    private void SessionChanged(GlobalSystemMediaTransportControlsSession sender, object _)
        => _ = RefreshAsync(sender);

    private async Task RefreshAsync(GlobalSystemMediaTransportControlsSession? changedSession = null)
    {
        var manager = _manager;
        if (manager is null || Volatile.Read(ref _disposed) != 0)
            return;

        try
        {
            var sessions = manager.GetSessions();
            foreach (var session in sessions)
                SubscribeToSession(session);

            var snapshots = new List<MediaSessionSnapshot>(sessions.Count);
            foreach (var session in sessions)
            {
                if (changedSession is not null && SessionKey(session) != SessionKey(changedSession) && snapshots.Count > 0)
                {
                    // Keep the loop responsive when a single session changes; the next manager event refreshes the rest.
                }
                var snapshot = await ReadSnapshotAsync(session).ConfigureAwait(false);
                if (snapshot is not null && IsAllowed(snapshot))
                    snapshots.Add(snapshot);
            }

            string? currentId = manager.GetCurrentSession() is { } current
                ? SessionKey(current)
                : null;
            var focused = MediaSelection.SelectFocused(snapshots, currentId);
            var next = FindNextTrack(snapshots, focused);
            Publish(new MediaSnapshot(snapshots, currentId, focused, next));
        }
        catch (Exception exception)
        {
            _logger.Warn($"Media session refresh failed: {exception.Message}");
        }
    }

    private void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
    {
        var key = SessionKey(session);
        if (_subscribedSessions.ContainsKey(key))
            return;
        _subscribedSessions[key] = session;
        session.MediaPropertiesChanged += SessionChanged;
        session.PlaybackInfoChanged += SessionChanged;
        session.TimelinePropertiesChanged += SessionChanged;
    }

    private async Task<MediaSessionSnapshot?> ReadSnapshotAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var player = _resolver.Resolve(session.SourceAppUserModelId);
            var status = MapStatus(playback?.PlaybackStatus);
            var controls = new MediaPlaybackCapabilities(
                playback?.Controls?.IsPlayEnabled == true,
                playback?.Controls?.IsPauseEnabled == true,
                playback?.Controls?.IsPreviousEnabled == true,
                playback?.Controls?.IsNextEnabled == true,
                playback?.Controls?.IsPlaybackPositionEnabled == true,
                playback?.Controls?.IsChannelDownEnabled == true || playback?.Controls?.IsChannelUpEnabled == true,
                playback?.Controls?.IsFastForwardEnabled == true || playback?.Controls?.IsRewindEnabled == true);
            var track = new MediaTrackSnapshot(
                properties?.Title ?? string.Empty,
                properties?.Artist ?? string.Empty,
                properties?.AlbumTitle ?? string.Empty,
                properties?.AlbumArtist ?? string.Empty,
                session.SourceAppUserModelId ?? string.Empty,
                await ReadThumbnailAsync(properties?.Thumbnail).ConfigureAwait(false),
                status,
                controls,
                ToTimeline(timeline),
                playback?.IsShuffleActive == true,
                MapRepeat(playback?.AutoRepeatMode));
            return new MediaSessionSnapshot(
                SessionKey(session),
                player.DisplayName,
                session.SourceAppUserModelId ?? string.Empty,
                track,
                string.Equals(
                    _manager?.GetCurrentSession() is { } current ? SessionKey(current) : null,
                    SessionKey(session),
                    StringComparison.Ordinal));
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not read media session: {exception.Message}");
            return null;
        }
    }

    private bool IsAllowed(MediaSessionSnapshot session)
    {
        var settings = _settings.Snapshot;
        if (!settings.MediaFlyoutEnabled && !settings.TaskbarWidgetEnabled)
            return false;
        var entries = settings.AppFilteringMode == 0 ? settings.BlockedApps : settings.AllowedApps;
        return MediaFilter.IsAllowed(settings.AppFilteringEnabled, settings.AppFilteringMode, entries, session.AppDisplayName, session.Id);
    }

    private MediaSessionSnapshot? GetActiveSession()
        => Snapshot.ActiveSession;

    private void RefreshFromSettings()
        => UiDispatcher.EnqueueOrRun(Refresh);

    private void Publish(MediaSnapshot snapshot)
        => UiDispatcher.EnqueueOrRun(() => _store.Set(snapshot));

    private async Task InvokeAsync(
        Func<GlobalSystemMediaTransportControlsSession, Task> command,
        CancellationToken cancellationToken)
    {
        var session = GetActiveSession();
        if (session is null)
            return;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await command(GetNativeSession(session.Id)).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Media command failed: {exception.Message}");
        }
    }

    private GlobalSystemMediaTransportControlsSession GetNativeSession(string id)
        => _subscribedSessions.TryGetValue(id, out var session)
            ? session
            : throw new InvalidOperationException("The media session is no longer available.");

    private static string SessionKey(GlobalSystemMediaTransportControlsSession session)
        => $"{session.SourceAppUserModelId ?? "Media player"}:{RuntimeHelpers.GetHashCode(session):X8}";

    private static MediaSessionSnapshot? FindNextTrack(IReadOnlyList<MediaSessionSnapshot> sessions, MediaSessionSnapshot? current)
        => sessions.FirstOrDefault(session => current is not null
            && !string.Equals(session.Id, current.Id, StringComparison.Ordinal)
            && !string.Equals(session.Track.Title, current.Track.Title, StringComparison.OrdinalIgnoreCase));

    private static DomainMediaStatus MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus? status)
        => status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => DomainMediaStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => DomainMediaStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => DomainMediaStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => DomainMediaStatus.Changing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => DomainMediaStatus.Opened,
            _ => DomainMediaStatus.Closed,
        };

    private static int MapRepeat(MediaPlaybackAutoRepeatMode? mode)
        => mode switch
        {
            MediaPlaybackAutoRepeatMode.Track => 1,
            MediaPlaybackAutoRepeatMode.List => 2,
            _ => 0,
        };

    private static MediaTimelineSnapshot ToTimeline(GlobalSystemMediaTransportControlsSessionTimelineProperties timeline)
        => new(
            timeline.StartTime,
            timeline.EndTime,
            timeline.Position,
            timeline.MinSeekTime,
            timeline.MaxSeekTime,
            timeline.LastUpdatedTime);

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
            return null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            using var managed = stream.AsStreamForRead();
            using var memory = new MemoryStream();
            await managed.CopyToAsync(memory).ConfigureAwait(false);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _settingsUnsubscribe?.Invoke();
        if (_manager is not null)
        {
            _manager.SessionsChanged -= Manager_SessionsChanged;
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
        }
        _subscribedSessions.Clear();
    }
}
