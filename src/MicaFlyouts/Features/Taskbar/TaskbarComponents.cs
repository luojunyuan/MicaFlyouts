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

public sealed class TaskbarWidgetWindowComponent : Component
{
    public override Element Render() => Component<TaskbarWidgetComponent>();
}

public sealed class TaskbarWidgetComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var media = UseExternalStore(services.MediaStore.Subscribe, () => services.MediaStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var active = media.ActiveSession;
        if (active is null && settings.TaskbarWidgetHideCompletely)
            return Empty();
        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            HStack(6,
                Component<CoverImage, CoverImageProps>(new CoverImageProps(active?.Track.Thumbnail, 28)),
                VStack(0,
                    MarqueeText(active?.Track.DisplayTitle ?? "-").FontSize(11),
                    MarqueeText(active?.Track.DisplayArtist ?? "-").FontSize(10).Opacity(0.58))),
            6,
            4));
    }
}

public sealed class TaskbarVisualizerWindowComponent : Component
{
    public override Element Render() => Component<TaskbarVisualizerComponent>();
}

public sealed class TaskbarVisualizerComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var snapshot = UseExternalStore(services.VisualizerStore.Subscribe, () => services.VisualizerStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var bars = snapshot.Bars;
        if (bars.Count == 0)
            bars = new[] { 0.02f };
        var elements = bars
            .Select((bar, index) => Border(Empty())
                .Background(Accent)
                .Width(3)
                .Height(Math.Max(2, 34 * bar))
                .VAlign(VerticalAlignment.Bottom)
                .WithKey(index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        Element body = HStack(2, elements);
        if (settings.TaskbarVisualizerCenteredBars)
            body = Grid([GridSize.Star()], [GridSize.Star()], body);
        var surface = Border(body)
            .Width(84)
            .Height(40)
            .Padding(4, 2)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(4);
        return settings.TaskbarWidgetBackgroundBlur ? surface.Background(LayerFill) : surface;
    }
}
