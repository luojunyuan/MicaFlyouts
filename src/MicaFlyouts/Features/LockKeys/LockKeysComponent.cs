using MicaFlyouts.App;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.LockKeys;

public sealed class LockKeysWindowComponent : Component
{
    public override Element Render() => Component<LockKeysComponent>();
}

public sealed class LockKeysComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var snapshot = UseExternalStore(services.LockKeyStore.Subscribe, () => services.LockKeyStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var keys = new[]
        {
            (LockKeyKind.CapsLock, "Caps", snapshot.CapsLock, settings.LockKeysCapsEnabled),
            (LockKeyKind.NumLock, "Num", snapshot.NumLock, settings.LockKeysNumEnabled),
            (LockKeyKind.ScrollLock, "Scroll", snapshot.ScrollLock, settings.LockKeysScrollEnabled),
            (LockKeyKind.Insert, "Insert", snapshot.Insert, settings.LockKeysInsertEnabled),
        };
        var items = keys
            .Where(static key => key.Item4)
            .Select(key => KeyItem(key.Item2, key.Item3))
            .ToArray();
        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            HStack(10, items),
            8,
            8));
    }

    private static StackElement KeyItem(string label, bool isOn)
    {
        var visual = LockKeyLayout.VisualState(isOn);
        return VStack(2,
                TextBlock(label).FontSize(12).SemiBold().TextAlignment(TextAlignment.Center),
                Border(Empty()).Background(isOn ? Accent : SecondaryText).Width(visual.IndicatorWidth / 3).Height(2))
            .Opacity(visual.Opacity)
            .VAlign(VerticalAlignment.Center)
            .WithKey(label);
    }
}
