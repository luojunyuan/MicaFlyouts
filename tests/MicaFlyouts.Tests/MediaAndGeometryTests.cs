using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Flyouts;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Taskbar;
using MicaFlyouts.Domain.Visualizer;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed class MediaAndGeometryTests
{
    [Fact]
    public void MediaFilter_MatchesNamesExactlyButIdsBySubstring()
    {
        Assert.True(MediaFilter.MatchesEntry("Spotify", "Spotify", "Package.Spotify!App"));
        Assert.False(MediaFilter.MatchesEntry("Spotify", "Spotify music bridge", "Package.Other!App"));
        Assert.True(MediaFilter.MatchesEntry("spotify", "Other", "Package.Spotify!App"));
        Assert.False(MediaFilter.MatchesEntry("", "Anything", "Anything"));
        Assert.False(MediaFilter.IsAllowed(true, 0, ["Spotify"], "Spotify", "Spotify"));
        Assert.True(MediaFilter.IsAllowed(true, 1, ["Spotify"], "Spotify", "Spotify"));
    }

    [Fact]
    public void MediaSelection_PrefersFocusedAndSkipsClosed()
    {
        var closed = Session("closed", MediaPlaybackStatus.Closed, false);
        var first = Session("first", MediaPlaybackStatus.Paused, false);
        var focused = Session("focused", MediaPlaybackStatus.Playing, true);
        Assert.Equal("focused", MediaSelection.SelectFocused([closed, first, focused], null)?.Id);
        Assert.Equal("focused", MediaSelection.SelectFocused([closed, first, focused], "missing")?.Id);
        Assert.Null(MediaSelection.SelectFocused([closed], null));
    }

    [Fact]
    public void NextUpRules_SuppressesDuplicateTitles()
    {
        var current = Track("Song", "Artist");
        Assert.False(NextUpRules.ShouldShow(true, current, Track("song", "Different")));
        Assert.True(NextUpRules.ShouldShow(true, current, Track("Other", "Artist")));
        Assert.False(NextUpRules.ShouldShow(false, current, Track("Other", "Artist")));
    }

    [Fact]
    public void TimelineFormatter_UsesHoursOnlyWhenNeeded()
    {
        Assert.Equal("00:07", TimelineFormatter.Format(TimeSpan.FromSeconds(7)));
        Assert.Equal("01:02:03", TimelineFormatter.Format(new TimeSpan(1, 2, 3)));
        Assert.Equal("00:00", TimelineFormatter.Format(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void FlyoutPositions_CoverAllSixPositionsAndOsdReservation()
    {
        var work = new ScreenRect(100, 50, 1000, 800);
        var window = new ScreenRect(0, 0, 300, 100);
        Assert.Equal(new WindowPosition(116, 734), FlyoutPositionCalculator.Calculate(FlyoutPosition.BottomLeft, window, work));
        Assert.Equal(new WindowPosition(450, 734), FlyoutPositionCalculator.Calculate(FlyoutPosition.BottomCenter, window, work));
        Assert.Equal(new WindowPosition(784, 734), FlyoutPositionCalculator.Calculate(FlyoutPosition.BottomRight, window, work));
        Assert.Equal(new WindowPosition(450, 66), FlyoutPositionCalculator.Calculate(FlyoutPosition.TopCenter, window, work));
        Assert.Equal(80, FlyoutPositionCalculator.GetBottomCenterMargin(true, false));
        Assert.Equal(16, FlyoutPositionCalculator.GetBottomCenterMargin(true, true));
    }

    [Fact]
    public void TaskbarLayout_SupportsHorizontalVerticalPaddingAndFixedPositions()
    {
        var horizontal = TaskbarLayoutCalculator.Calculate(new TaskbarLayoutInput(
            new PixelRect(0, 1040, 1920, 40), 1, 100, 40, 84, 40,
            TaskbarPosition.End, TaskbarPosition.Start, true, true, true, 5, false, false, null, new PixelRect(1750, 1040, 170, 40)));
        Assert.Equal(TaskbarOrientation.Horizontal, horizontal.Orientation);
        Assert.False(horizontal.WidgetRect.IsEmpty);
        Assert.False(horizontal.VisualizerRect.IsEmpty);

        var vertical = TaskbarLayoutCalculator.Calculate(new TaskbarLayoutInput(
            new PixelRect(0, 0, 48, 1080), 1.5, 100, 40, 84, 40,
            TaskbarPosition.Center, TaskbarPosition.End, true, true, false, 0, true, true));
        Assert.Equal(TaskbarOrientation.Vertical, vertical.Orientation);
        Assert.False(vertical.WidgetRect.IsEmpty);
        Assert.False(vertical.VisualizerRect.IsEmpty);
    }

    [Fact]
    public void LockKeyLayout_UsesMinimumWidthAndOnOffVisualStates()
    {
        Assert.Equal(160, LockKeyLayout.EstimateWidth(""));
        Assert.True(LockKeyLayout.EstimateWidth("Caps Lock") >= 160);
        Assert.Equal(1, LockKeyLayout.VisualState(true).Opacity);
        Assert.Equal(0.2, LockKeyLayout.VisualState(false).Opacity);
        Assert.Contains("On", LockKeyLayout.StatusText(LockKeyKind.CapsLock, true));
        Assert.Equal("Insert pressed", LockKeyLayout.StatusText(LockKeyKind.Insert, false));
    }

    [Fact]
    public void FftProcessor_ProducesBarsAndFrameLimiterCapsRate()
    {
        var samples = new float[FftProcessor.FftLength];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * i * 440 / 48000);
        var bars = FftProcessor.ComputeBars(samples, 48000, 12, 2, 3);
        Assert.Equal(12, bars.Length);
        Assert.Contains(bars, value => value > 0);
        var limiter = new FrameRateLimiter(30);
        var now = DateTimeOffset.UtcNow;
        Assert.True(limiter.ShouldPublish(now));
        Assert.False(limiter.ShouldPublish(now.AddMilliseconds(10)));
        Assert.True(limiter.ShouldPublish(now.AddMilliseconds(34)));
    }

    private static MediaSessionSnapshot Session(string id, MediaPlaybackStatus status, bool focused)
        => new(id, id, id, Track(id, "Artist") with { PlaybackStatus = status }, focused);

    private static MediaTrackSnapshot Track(string title, string artist)
        => new(title, artist, "Album", "media", "player", null, MediaPlaybackStatus.Playing,
            new MediaPlaybackCapabilities(CanPlay: true, CanPause: true, CanSeek: true),
            new MediaTimelineSnapshot(TimeSpan.Zero, TimeSpan.FromMinutes(4), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(4), DateTimeOffset.UtcNow), false, 0);
}
