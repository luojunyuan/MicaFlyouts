namespace MicaFlyouts.Domain.Media;

public enum MediaPlaybackStatus
{
    Closed,
    Changing,
    Opened,
    Playing,
    Paused,
    Stopped,
}

public readonly record struct MediaPlaybackCapabilities(
    bool CanPlay = false,
    bool CanPause = false,
    bool CanPrevious = false,
    bool CanNext = false,
    bool CanSeek = false,
    bool CanRepeat = false,
    bool CanShuffle = false);

public sealed record MediaTimelineSnapshot(
    TimeSpan Start,
    TimeSpan End,
    TimeSpan Position,
    TimeSpan MinSeek,
    TimeSpan MaxSeek,
    DateTimeOffset LastUpdated)
{
    public TimeSpan CurrentPosition(DateTimeOffset now)
    {
        if (Position < TimeSpan.Zero || LastUpdated == default)
            return Position < TimeSpan.Zero ? TimeSpan.Zero : Position;

        var adjusted = Position + (now - LastUpdated);
        return adjusted < Start ? Start : adjusted > End ? End : adjusted;
    }
}

public sealed record MediaTrackSnapshot(
    string Title,
    string Artist,
    string Album,
    string MediaId,
    string PlayerId,
    byte[]? Thumbnail,
    MediaPlaybackStatus PlaybackStatus,
    MediaPlaybackCapabilities Capabilities,
    MediaTimelineSnapshot Timeline,
    bool IsShuffleActive,
    int RepeatMode)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Unknown title" : Title;
    public string DisplayArtist => string.IsNullOrWhiteSpace(Artist) ? "Unknown artist" : Artist;
}

public sealed record MediaSessionSnapshot(
    string Id,
    string AppDisplayName,
    string AppUserModelId,
    MediaTrackSnapshot Track,
    bool IsFocused = false);

public sealed record MediaSnapshot(
    IReadOnlyList<MediaSessionSnapshot> Sessions,
    string? FocusedSessionId,
    MediaSessionSnapshot? ActiveSession,
    MediaSessionSnapshot? NextTrack)
{
    public static MediaSnapshot Empty { get; } = new([], null, null, null);
}
