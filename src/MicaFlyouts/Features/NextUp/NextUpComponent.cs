using MicaFlyouts.Domain.Media;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static MicaFlyouts.UI.Components.TextComponents;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.NextUp;

public sealed class NextUpWindowComponent(MediaTrackSnapshot track) : LocalizedWindowComponent
{
    private readonly MediaTrackSnapshot _track = track;

    public override Element Render()
        => UseLocalized(Component<NextUpComponent, MediaTrackSnapshot>(_track));
}

public sealed class NextUpComponent : Component<MediaTrackSnapshot>
{
    public override Element Render()
    {
        var t = UseIntl();
        var title = string.IsNullOrWhiteSpace(Props.Title)
            ? t.Message(Loc.App.UnknownTitle)
            : Props.Title;
        var artist = string.IsNullOrWhiteSpace(Props.Artist)
            ? t.Message(Loc.App.UnknownArtist)
            : Props.Artist;
        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            HStack(10,
                Component<CoverImage, CoverImageProps>(new CoverImageProps(Props.Thumbnail, 38)),
                VStack(1,
                    Caption(t.Message(Loc.App.NextUpWindow_UpNextText)).Opacity(0.6),
                    MarqueeText(title).SemiBold(),
                    MarqueeText(artist).Opacity(0.6))),
            8,
            6));
    }
}
