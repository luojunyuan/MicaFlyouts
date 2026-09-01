using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.UI.Components;

public sealed record FlyoutSurfaceProps(Element Child, double Radius = 8, double Padding = 12);

public sealed class FlyoutSurface : Component<FlyoutSurfaceProps>
{
    public override Element Render()
        => Border(Props.Child)
            .Padding(Props.Padding)
            .Background(LayerFill)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(Props.Radius);
}

public sealed record SettingRowProps(string Title, string Description, Element? Control = null, Element? Icon = null);

public sealed class SettingRow : Component<SettingRowProps>
{
    public override Element Render()
        => Grid(
            [GridSize.Star(), GridSize.Auto],
            [GridSize.Auto],
            HStack(10,
                Props.Icon ?? Empty(),
                VStack(2,
                    TextBlock(Props.Title).SemiBold(),
                    TextBlock(Props.Description).Opacity(0.68).FontSize(12)))
                .Grid(row: 0, column: 0),
            (Props.Control ?? Empty()).Grid(row: 0, column: 1));
}
