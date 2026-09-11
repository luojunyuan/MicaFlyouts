using MicaFlyouts.App;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static MicaFlyouts.UI.Components.IconButton;
using static MicaFlyouts.UI.Components.TextComponents;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.Media;

public sealed class MediaFlyoutWindowComponent : Component
{
    public override Element Render() => Component<MediaFlyoutComponent>();
}

public sealed class MediaFlyoutComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;

        var media = UseExternalStore(services.MediaStore.Subscribe, () => services.MediaStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var active = media.ActiveSession;
        if (active is null)
            return Empty();

        var track = active.Track;
        var position = track.Timeline.CurrentPosition(DateTimeOffset.UtcNow);
        double max = Math.Max(track.Timeline.End.TotalSeconds, 1);
        double value = Math.Clamp(position.TotalSeconds, 0, max);
        var controls = HStack(4,
            Create("\uE100", "Previous", () => _ = services.Media.SkipPreviousAsync()),
            Create(
                track.PlaybackStatus == MediaPlaybackStatus.Playing ? "\uE769" : "\uE768",
                track.PlaybackStatus == MediaPlaybackStatus.Playing ? "Pause" : "Play",
                () => _ = services.Media.TogglePlayPauseAsync()),
            Create("\uE101", "Next", () => _ = services.Media.SkipNextAsync()),
            settings.RepeatEnabled
                ? Create("\uE8EE", "Repeat", () => _ = services.Media.SetRepeatAsync(track.RepeatMode == 0 ? 2 : 0))
                : Empty(),
            settings.ShuffleEnabled
                ? Create("\uE8B1", "Shuffle", () => _ = services.Media.SetShuffleAsync(!track.IsShuffleActive))
                : Empty())
            .VAlign(VerticalAlignment.Center);

        Element seek = Empty();
        if (settings.SeekbarEnabled && track.Capabilities.CanSeek)
        {
            seek = Slider(
                Optional<double>.Of(value),
                min: 0,
                max: max,
                onValueChanged: next => _ = services.Media.SeekAsync(TimeSpan.FromSeconds(next)))
                .Height(18)
                .WithKey("seekbar");
        }

        var content = Grid(
            [GridSize.Px(78), GridSize.Star()],
            [GridSize.Px(78), GridSize.Auto],
            Component<CoverImage, CoverImageProps>(new CoverImageProps(track.Thumbnail, 78)).Grid(row: 0, column: 0),
            VStack(4,
                MarqueeText(track.DisplayTitle).FontSize(14).SemiBold(),
                MarqueeText(track.DisplayArtist).FontSize(14).Opacity(0.55),
                controls).Grid(row: 0, column: 1).Padding(left: 12),
            HStack(4,
                TextBlock(TimelineFormatter.Format(position)).FontSize(11).Opacity(0.55).Width(42),
                seek,
                TextBlock(TimelineFormatter.Format(track.Timeline.End)).FontSize(11).Opacity(0.55).Width(42))
                .Grid(row: 1, column: 0, columnSpan: 2)
                .Padding(top: 8));

        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(content, 8, 12));
    }
}
