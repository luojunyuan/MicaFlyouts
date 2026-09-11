using MicaFlyouts.Features.Media;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.Settings;

internal sealed class RootComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var localization = UseExternalStore(services.LocalizationStore.Subscribe, () => services.LocalizationStore.Snapshot);
        return LocaleProvider(
            localization.Language,
            Component<MediaFlyoutWindowComponent>(),
            services.Localization.ResourceProvider);
    }
}
