using MicaFlyouts.App;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using MicaFlyouts.UI.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using static MicaFlyouts.UI.Components.IconButton;
using static MicaFlyouts.UI.Components.TextComponents;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.Media;

public sealed class MediaFlyoutWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<MediaFlyoutComponent>());
}

public sealed class MediaFlyoutComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();

        var media = UseExternalStore(services.MediaStore.Subscribe, () => services.MediaStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var active = media.ActiveSession;
        if (active is null)
            return Empty();

        var track = active.Track;
        var position = track.Timeline.CurrentPosition(DateTimeOffset.UtcNow);
        double min = Math.Max(track.Timeline.MinSeek.TotalSeconds, 0);
        double max = Math.Max(track.Timeline.MaxSeek.TotalSeconds, min + 1);

        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            Child: RenderMediaLayout(active, settings, position, min, max, services, t),
            Radius: 8,
            Padding: 12));
    }

    private static GridElement RenderMediaLayout(
        MediaSessionSnapshot session,
        SettingsSnapshot settings,
        TimeSpan position,
        double min,
        double max,
        AppServices services,
        IntlAccessor t)
    {
        var track = session.Track;
        bool showTimeline = settings.SeekbarEnabled
            && track.Capabilities.CanSeek
            && track.Timeline.MaxSeek > track.Timeline.MinSeek;
        double coverSize = settings.CompactLayout ? 36 : 78;
        double coverCellSize = settings.CompactLayout ? coverSize : coverSize + 12;
        Element cover = RenderCover(track, coverSize);
        if (!settings.CompactLayout)
            cover = cover.Margin(6);
        Element details = settings.CompactLayout
            ? RenderCompactTrackInfo(track, settings, services, t)
            : RenderTrackInfo(session, settings, services, t);

        return Grid(
            [GridSize.Px(coverCellSize), GridSize.Star()],
            [GridSize.Px(coverCellSize), GridSize.Auto],
            cover.Grid(row: 0, column: 0),
            details.Grid(row: 0, column: 1).Padding(left: 12),
            showTimeline
                ? RenderTimeline(track, position, min, max, services)
                    .Grid(row: 1, column: 0, columnSpan: 2)
                    .Padding(top: 8)
                : Empty());
    }

    private static ComponentElement<CoverImageProps> RenderCover(MediaTrackSnapshot track, double size)
        => Component<CoverImage, CoverImageProps>(new CoverImageProps(track.Thumbnail, size));

    private static StackElement RenderTrackInfo(
        MediaSessionSnapshot session,
        SettingsSnapshot settings,
        AppServices services,
        IntlAccessor t)
    {
        var track = session.Track;
        var title = string.IsNullOrWhiteSpace(track.Title)
            ? t.Message(Loc.App.UnknownTitle)
            : track.Title;
        var artist = track.Artist;
        var alignment = settings.CenterTitleArtist ? TextAlignment.Center : TextAlignment.Left;

        return VStack(4,
            MarqueeText(title).FontSize(14).SemiBold().TextAlignment(alignment),
            MarqueeText(artist).FontSize(14).Opacity(0.55).TextAlignment(alignment),
            HStack(8,
                RenderMediaControls(track, settings, services, t, compact: false),
                settings.PlayerInfoEnabled ? RenderPlayerInfo(session, services) : Empty())
                .HorizontalAlignment(
                    settings.CenterTitleArtist && !settings.PlayerInfoEnabled
                        ? HorizontalAlignment.Center
                        : HorizontalAlignment.Stretch)
                .VAlign(VerticalAlignment.Center));
    }

    private static StackElement RenderCompactTrackInfo(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        AppServices services,
        IntlAccessor t)
    {
        var title = string.IsNullOrWhiteSpace(track.Title)
            ? t.Message(Loc.App.UnknownTitle)
            : track.Title;
        var artist = track.Artist;

        return HStack(8,
            VStack(2,
                MarqueeText(title).FontSize(13).SemiBold(),
                MarqueeText(artist).FontSize(12).Opacity(0.55))
                .Width(216)
                .MinWidth(0),
            RenderMediaControls(track, settings, services, t, compact: true))
            .VAlign(VerticalAlignment.Center);
    }

    private static ButtonElement RenderPlayerInfo(MediaSessionSnapshot session, AppServices services)
        => Button(HStack(4,
                Component<CoverImage, CoverImageProps>(new CoverImageProps(session.AppIcon, 14)),
                MarqueeText(session.AppDisplayName).FontSize(10).Opacity(0.25).Width(48)),
                () => services.Media.ActivateFocusedPlayer())
            .Width(70)
            .Height(28)
            .AutomationName(session.AppDisplayName)
            .Background(TransparentBackground)
            .BorderThickness(new Thickness(0))
            .Padding(new Thickness(0))
            .CornerRadius(new CornerRadius(4));

    private static StackElement RenderMediaControls(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        AppServices services,
        IntlAccessor t,
        bool compact)
    {
        bool canTogglePlayback = track.PlaybackStatus == MediaPlaybackStatus.Playing
            ? track.Capabilities.CanPause
            : track.Capabilities.CanPlay;
        var controls = new List<Element>
        {
            Create(
                FluentSystemIcon.Previous20,
                t.Message(Loc.App.MediaPrevious),
                () => _ = services.Media.SkipPreviousAsync(),
                isEnabled: track.Capabilities.CanPrevious,
                opacity: track.Capabilities.CanPrevious ? 1 : 0.35),
            Create(
                track.PlaybackStatus == MediaPlaybackStatus.Playing ? FluentSystemIcon.Pause20 : FluentSystemIcon.Play20,
                track.PlaybackStatus == MediaPlaybackStatus.Playing
                    ? t.Message(Loc.App.MediaPause)
                    : t.Message(Loc.App.MediaPlay),
                () => _ = services.Media.TogglePlayPauseAsync(),
                size: 32,
                accented: true,
                isEnabled: canTogglePlayback,
                opacity: canTogglePlayback ? 1 : 0.35),
            Create(
                FluentSystemIcon.Next20,
                t.Message(Loc.App.MediaNext),
                () => _ = services.Media.SkipNextAsync(),
                isEnabled: track.Capabilities.CanNext,
                opacity: track.Capabilities.CanNext ? 1 : 0.35),
        };
        if (!compact && settings.RepeatEnabled)
        {
            int nextRepeatMode = track.RepeatMode switch
            {
                0 => 2,
                2 => 1,
                _ => 0,
            };
            controls.Add(Create(
                FluentSystemIcon.ArrowRepeatAll24,
                t.Message(Loc.App.MediaRepeat),
                () => _ = services.Media.SetRepeatAsync(nextRepeatMode),
                isEnabled: track.Capabilities.CanRepeat,
                opacity: !track.Capabilities.CanRepeat ? 0.35 : track.RepeatMode == 0 ? 0.5 : 1));
        }
        if (!compact && settings.ShuffleEnabled)
            controls.Add(Create(
                FluentSystemIcon.ArrowShuffle24,
                t.Message(Loc.App.MediaShuffle),
                () => _ = services.Media.SetShuffleAsync(!track.IsShuffleActive),
                isEnabled: track.Capabilities.CanShuffle,
                opacity: !track.Capabilities.CanShuffle ? 0.35 : track.IsShuffleActive ? 1 : 0.5));

        return HStack(8, [.. controls]).VAlign(VerticalAlignment.Center);
    }

    private static StackElement RenderTimeline(
        MediaTrackSnapshot track,
        TimeSpan position,
        double min,
        double max,
        AppServices services)
        => HStack(4,
            TextBlock(TimelineFormatter.Format(position)).FontSize(11).Opacity(0.55).Width(42),
            RenderSeekBar(track, position, min, max, services),
            TextBlock(TimelineFormatter.Format(TimeSpan.FromSeconds(max))).FontSize(11).Opacity(0.55).Width(42));

    private static SliderElement RenderSeekBar(
        MediaTrackSnapshot track,
        TimeSpan position,
        double min,
        double max,
        AppServices services)
    {
        double value = Math.Clamp(position.TotalSeconds, min, max);
        return Slider(
            Optional<double>.Of(value),
            min: min,
            max: max,
            onValueChanged: next => _ = services.Media.SeekAsync(TimeSpan.FromSeconds(next)))
            .Height(18)
            .WithKey("seekbar");
    }
}
