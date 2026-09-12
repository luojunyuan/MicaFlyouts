using MicaFlyouts.App;
using MicaFlyouts.Domain.Visualizer;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static MicaFlyouts.UI.Components.TextComponents;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.Taskbar;

public sealed class TaskbarWidgetWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<TaskbarWidgetComponent>());
}

public sealed class TaskbarWidgetComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var media = UseExternalStore(services.MediaStore.Subscribe, () => services.MediaStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var active = media.ActiveSession;
        if (active is null && settings.TaskbarWidgetHideCompletely)
            return Empty();
        var title = TrackLabel(active?.Track.Title, active is not null, t.Message(Loc.App.UnknownTitle));
        var artist = TrackLabel(active?.Track.Artist, active is not null, t.Message(Loc.App.UnknownArtist));

        return RenderTaskbarWidget(active?.Track.Thumbnail, title, artist);
    }

    private static string TrackLabel(string? value, bool hasTrack, string unknown)
        => !hasTrack ? "-" : string.IsNullOrWhiteSpace(value) ? unknown : value;

    private static ComponentElement<FlyoutSurfaceProps> RenderTaskbarWidget(byte[]? thumbnail, string title, string artist)
        => Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            Child: HStack(6,
                Component<CoverImage, CoverImageProps>(new CoverImageProps(thumbnail, 28)),
                VStack(0,
                    MarqueeText(title).FontSize(11),
                    MarqueeText(artist).FontSize(10).Opacity(0.58))),
            Radius: 6,
            Padding: 4));
}

public sealed class TaskbarVisualizerWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<TaskbarVisualizerComponent>());
}

public sealed class TaskbarVisualizerComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var snapshot = UseExternalStore(services.VisualizerStore.Subscribe, () => services.VisualizerStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        IReadOnlyList<float> bars = snapshot.Bars.Count == 0
            ? [0.02f]
            : snapshot.Bars;

        return RenderVisualizerSurface(
            bars,
            settings.TaskbarVisualizerCenteredBars,
            settings.TaskbarWidgetBackgroundBlur);
    }

    private static BorderElement RenderVisualizerSurface(
        IReadOnlyList<float> bars,
        bool centerBars,
        bool blurBackground)
    {
        Element body = RenderVisualizerBars(bars);
        if (centerBars)
            body = Grid([GridSize.Star()], [GridSize.Star()], body);

        var surface = Border(body)
            .Width(84)
            .Height(40)
            .Padding(4, 2)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(4);
        return blurBackground ? surface.Background(LayerFill) : surface;
    }

    private static StackElement RenderVisualizerBars(IReadOnlyList<float> bars)
        => HStack(2,
            [.. bars
                .Select((value, band) => new VisualizerBar(band, value))
                .Select(bar => Border(Empty())
                    .Background(Accent)
                    .Width(3)
                    .Height(Math.Max(2, 34 * bar.Value))
                    .VAlign(VerticalAlignment.Bottom)
                    .WithKey(bar.Band.ToString(System.Globalization.CultureInfo.InvariantCulture)))]);

    private readonly record struct VisualizerBar(int Band, float Value);
}
