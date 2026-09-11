using MicaFlyouts.App;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.LockKeys;

public sealed class LockKeysWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<LockKeysComponent>());
}

public sealed class LockKeysComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var snapshot = UseExternalStore(services.LockKeyStore.Subscribe, () => services.LockKeyStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);

        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            Child: RenderKeyItems(snapshot, settings, t),
            Radius: 8,
            Padding: 8));
    }

    private static StackElement RenderKeyItems(
        LockKeySnapshot snapshot,
        SettingsSnapshot settings,
        IntlAccessor t)
    {
        var keys = new[]
        {
            (LockKeyKind.CapsLock, t.Message(Loc.App.LockWindow_CapsLock), snapshot.CapsLock, settings.LockKeysCapsEnabled),
            (LockKeyKind.NumLock, t.Message(Loc.App.LockWindow_NumLock), snapshot.NumLock, settings.LockKeysNumEnabled),
            (LockKeyKind.ScrollLock, t.Message(Loc.App.LockWindow_ScrollLock), snapshot.ScrollLock, settings.LockKeysScrollEnabled),
            (LockKeyKind.Insert, t.Message(Loc.App.LockWindow_InsertPressed), snapshot.Insert, settings.LockKeysInsertEnabled),
        };
        var items = keys
            .Where(static key => key.Item4)
            .Select(key => KeyItem(key.Item1, key.Item2, key.Item3))
            .ToArray();
        return HStack(10, items);
    }

    private static StackElement KeyItem(LockKeyKind key, string label, bool isOn)
    {
        var visual = LockKeyLayout.VisualState(isOn);
        return VStack(2,
                TextBlock(label).FontSize(12).SemiBold().TextAlignment(TextAlignment.Center),
                Border(Empty()).Background(isOn ? Accent : SecondaryText).Width(visual.IndicatorWidth / 3).Height(2))
            .Opacity(visual.Opacity)
            .VAlign(VerticalAlignment.Center)
            .WithKey(key.ToString());
    }
}
