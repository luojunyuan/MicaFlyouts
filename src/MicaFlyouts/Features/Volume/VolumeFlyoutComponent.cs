using MicaFlyouts.App;
using MicaFlyouts.Domain.Volume;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using static MicaFlyouts.UI.Components.IconButton;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.Volume;

public sealed class VolumeFlyoutWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<VolumeFlyoutComponent>());
}

public sealed class VolumeFlyoutComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();

        var volume = UseExternalStore(services.VolumeStore.Subscribe, () => services.VolumeStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var master = Slider(
            Optional<double>.Of(volume.MasterVolume * 100),
            min: 0,
            max: 100,
            onValueChanged: value => services.Audio.SetMasterVolume((float)(value / 100)))
            .Height(24)
            .Flex(grow: 1, basis: 0);
        var children = new List<Element>
        {
            Create(volume.IsMasterMuted ? "\uE74F" : "\uE767", t.Message(Loc.App.MediaMute), services.Audio.ToggleMasterMute),
            master,
            TextBlock($"{Math.Round(volume.MasterVolume * 100)}").Width(38).TextAlignment(TextAlignment.Right),
        };
        if (settings.VolumeMixerEnabled)
        {
            children.Add(Create("\uE995", t.Message(Loc.App.OpenVolumeMixer), () => services.Windows.OpenOrActivate(
                new WindowKey("volume-mixer"),
                new WindowSpec
                {
                    Title = t.Message(Loc.App.VolumeMixerSectionTitle),
                    Width = 420,
                    Height = 420,
                    MinWidth = 320,
                    MinHeight = 240,
                    Icon = AppBranding.WindowIcon,
                    StartPosition = WindowStartPosition.CenterOnCurrent,
                    CornerStyle = WindowCornerStyle.Rounded,
                    Backdrop = BackdropChoice.Of(BackdropKind.Mica),
                },
                static () => new VolumeMixerWindowComponent())));
        }
        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            HStack(8, children.ToArray()), 8, 8));
    }
}

public sealed class VolumeMixerWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<VolumeMixerComponent>());
}

public sealed class VolumeMixerComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var volume = UseExternalStore(services.VolumeStore.Subscribe, () => services.VolumeStore.Snapshot);
        var rows = volume.Applications
            .Select(application => ApplicationRow(t, application, services))
            .ToArray();
        return Border(
            VStack(12,
                HStack(8, TextBlock(t.Message(Loc.App.VolumeMixerSectionTitle)).FontSize(20).SemiBold(), Empty()),
                rows.Length == 0 ? TextBlock(t.Message(Loc.App.NoApplicationSessions)).Opacity(0.65) : VStack(8, rows)))
            .Padding(20)
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8);
    }

    private static StackElement ApplicationRow(IntlAccessor t, ApplicationVolumeSnapshot application, AppServices services)
        => HStack(8,
            TextBlock(application.DisplayName).Width(150).TextTrimming(TextTrimming.CharacterEllipsis),
            Slider(
                Optional<double>.Of(application.Volume * 100),
                0,
                100,
                value => services.Audio.SetApplicationVolume(application.SessionId, (float)(value / 100)))
                .Flex(grow: 1, basis: 0),
            Create(application.IsMuted ? "\uE74F" : "\uE767", t.Message(Loc.App.MediaMute), () => services.Audio.ToggleApplicationMute(application.SessionId)))
            .WithKey(application.SessionId);
}
