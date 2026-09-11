using MicaFlyouts.App;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.UI.Components;

public abstract class LocalizedWindowComponent : Component
{
    protected Element UseLocalized(Element child)
    {
        var services = AppRuntime.Services;
        var localization = UseExternalStore(
            services.LocalizationStore.Subscribe,
            () => services.LocalizationStore.Snapshot);

        return LocaleProvider(
            localization.Language,
            child,
            services.Localization.ResourceProvider);
    }
}
