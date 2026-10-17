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
