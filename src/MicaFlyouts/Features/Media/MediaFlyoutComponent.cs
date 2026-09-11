using MicaFlyouts.App;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
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
        double max = Math.Max(track.Timeline.End.TotalSeconds, 1);

        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            Child: RenderMediaLayout(track, settings, position, max, services, t),
            Radius: 8,
            Padding: 12));
    }

    private static GridElement RenderMediaLayout(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        TimeSpan position,
        double max,
        AppServices services,
        IntlAccessor t)
        => Grid(
            [GridSize.Px(78), GridSize.Star()],
            [GridSize.Px(78), GridSize.Auto],
            RenderCover(track).Grid(row: 0, column: 0),
            RenderTrackInfo(track, settings, services, t)
                .Grid(row: 0, column: 1)
                .Padding(left: 12),
            RenderTimeline(track, settings, position, max, services)
                .Grid(row: 1, column: 0, columnSpan: 2)
                .Padding(top: 8));

    private static ComponentElement<CoverImageProps> RenderCover(MediaTrackSnapshot track)
        => Component<CoverImage, CoverImageProps>(new CoverImageProps(track.Thumbnail, 78));

    private static StackElement RenderTrackInfo(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        AppServices services,
        IntlAccessor t)
    {
        var title = string.IsNullOrWhiteSpace(track.Title)
            ? t.Message(Loc.App.UnknownTitle)
            : track.Title;
        var artist = string.IsNullOrWhiteSpace(track.Artist)
            ? t.Message(Loc.App.UnknownArtist)
            : track.Artist;

        return VStack(4,
            MarqueeText(title).FontSize(14).SemiBold(),
            MarqueeText(artist).FontSize(14).Opacity(0.55),
            RenderMediaControls(track, settings, services, t));
    }

    private static StackElement RenderMediaControls(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        AppServices services,
        IntlAccessor t)
        => HStack(4,
            Create("\uE100", t.Message(Loc.App.MediaPrevious), () => _ = services.Media.SkipPreviousAsync()),
            Create(
                track.PlaybackStatus == MediaPlaybackStatus.Playing ? "\uE769" : "\uE768",
                track.PlaybackStatus == MediaPlaybackStatus.Playing
                    ? t.Message(Loc.App.MediaPause)
                    : t.Message(Loc.App.MediaPlay),
                () => _ = services.Media.TogglePlayPauseAsync()),
            Create("\uE101", t.Message(Loc.App.MediaNext), () => _ = services.Media.SkipNextAsync()),
            settings.RepeatEnabled
                ? Create("\uE8EE", t.Message(Loc.App.MediaRepeat), () => _ = services.Media.SetRepeatAsync(track.RepeatMode == 0 ? 2 : 0))
                : Empty(),
            settings.ShuffleEnabled
                ? Create("\uE8B1", t.Message(Loc.App.MediaShuffle), () => _ = services.Media.SetShuffleAsync(!track.IsShuffleActive))
                : Empty())
            .VAlign(VerticalAlignment.Center);

    private static StackElement RenderTimeline(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        TimeSpan position,
        double max,
        AppServices services)
        => HStack(4,
            TextBlock(TimelineFormatter.Format(position)).FontSize(11).Opacity(0.55).Width(42),
            RenderSeekBar(track, settings, position, max, services),
            TextBlock(TimelineFormatter.Format(track.Timeline.End)).FontSize(11).Opacity(0.55).Width(42));

    private static Element RenderSeekBar(
        MediaTrackSnapshot track,
        SettingsSnapshot settings,
        TimeSpan position,
        double max,
        AppServices services)
    {
        if (!settings.SeekbarEnabled || !track.Capabilities.CanSeek)
            return Empty();

        double value = Math.Clamp(position.TotalSeconds, 0, max);
        return Slider(
            Optional<double>.Of(value),
            min: 0,
            max: max,
            onValueChanged: next => _ = services.Media.SeekAsync(TimeSpan.FromSeconds(next)))
            .Height(18)
            .WithKey("seekbar");
    }
}
