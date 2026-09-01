namespace MicaFlyouts.Domain.Media;

public static class MediaFilter
{
    public static bool IsAllowed(
        bool filteringEnabled,
        int mode,
        IReadOnlyList<string>? entries,
        string? appDisplayName,
        string? sessionId)
    {
        if (!filteringEnabled)
            return true;

        bool matched = entries is not null && entries.Any(entry =>
            MatchesEntry(entry, appDisplayName, sessionId));

        return mode == 0 ? !matched : matched;
    }

    public static bool MatchesEntry(string? entry, string? appDisplayName, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        var normalized = entry.Trim();
        return string.Equals(appDisplayName, normalized, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(sessionId)
                && sessionId.Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public static class MediaSelection
{
    public static MediaSessionSnapshot? SelectFocused(
        IEnumerable<MediaSessionSnapshot> sessions,
        string? focusedSessionId)
    {
        var candidates = sessions.Where(static session => session.Track.PlaybackStatus != MediaPlaybackStatus.Closed).ToArray();
        if (candidates.Length == 0)
            return null;

        if (!string.IsNullOrEmpty(focusedSessionId))
        {
            var focused = candidates.FirstOrDefault(session =>
                string.Equals(session.Id, focusedSessionId, StringComparison.Ordinal));
            if (focused is not null)
                return focused;
        }

        return candidates.FirstOrDefault(static session => session.IsFocused) ?? candidates[0];
    }
}

public static class NextUpRules
{
    public static bool ShouldShow(
        bool enabled,
        MediaTrackSnapshot? current,
        MediaTrackSnapshot? next)
    {
        if (!enabled || current is null || next is null)
            return false;

        return !string.Equals(current.Title, next.Title, StringComparison.OrdinalIgnoreCase);
    }
}

public static class TimelineFormatter
{
    public static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
    }
}
