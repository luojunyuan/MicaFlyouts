using MicaFlyouts.Domain.Media;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static MicaFlyouts.UI.Components.TextComponents;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.NextUp;

public sealed class NextUpWindowComponent : Component
{
    private readonly MediaTrackSnapshot _track;

    public NextUpWindowComponent(MediaTrackSnapshot track)
    {
        _track = track;
    }

    public override Element Render()
        => Component<NextUpComponent, MediaTrackSnapshot>(_track);
}

public sealed class NextUpComponent : Component<MediaTrackSnapshot>
{
    public override Element Render()
        => Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            HStack(10,
                Component<CoverImage, CoverImageProps>(new CoverImageProps(Props.Thumbnail, 38)),
                VStack(1,
                    Caption("Next up").Opacity(0.6),
                    MarqueeText(Props.DisplayTitle).SemiBold(),
                    MarqueeText(Props.DisplayArtist).Opacity(0.6))),
            8,
            6));
}
