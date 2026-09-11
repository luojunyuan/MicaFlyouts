using MicaFlyouts.App;
using MicaFlyouts.Domain.Onboarding;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.Onboarding;

public sealed class OnboardingWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<OnboardingComponent>());
}

public sealed class OnboardingComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var onboarding = UseExternalStore(services.OnboardingStore.Subscribe, () => services.OnboardingStore.Snapshot);
        var step = onboarding.CurrentStep;
        var image = StepImage(step);
        var title = StepTitle(step, t);

        return Border(VStack(16,
                RenderIntroduction(services, t, step, image, title),
                RenderNavigation(services, t, step)))
            .Padding(28)
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8);
    }

    private static string StepImage(OnboardingStep step)
        => step switch
        {
            OnboardingStep.Media => "ms-appx:///Assets/Onboarding/MediaFlyout.png",
            OnboardingStep.Volume => "ms-appx:///Assets/Onboarding/Taskbar.png",
            _ => "ms-appx:///Assets/Onboarding/LockKeysFlyout.png",
        };

    private static string StepTitle(OnboardingStep step, IntlAccessor t)
        => step switch
        {
            OnboardingStep.Media => t.Message(Loc.App.MediaFlyoutTitle),
            OnboardingStep.Volume => t.Message(Loc.App.VolumeFlyoutTitle),
            _ => t.Message(Loc.App.LockKeysCustomizationTitle),
        };

    private static StackElement RenderIntroduction(
        AppServices services,
        IntlAccessor t,
        OnboardingStep step,
        string image,
        string title)
        => HStack(16,
            ((Image(image) with { Stretch = "UniformToFill" }).Width(420).Height(280))
                .AutomationName(t.Message(Loc.App.OnboardingIllustration, ("feature", title))),
            VStack(12,
                Heading(AppBranding.Name),
                SubHeading(title),
                TextBlock(t.Message(Loc.App.OnboardingDescription)),
                RenderStepToggle(services, t, step)));

    private static ToggleSwitchElement RenderStepToggle(AppServices services, IntlAccessor t, OnboardingStep step)
        => ToggleSwitch(
            step == OnboardingStep.Media
                ? Optional<bool>.Of(services.Settings.Snapshot.MediaFlyoutEnabled)
                : Optional<bool>.Of(step == OnboardingStep.Volume
                    ? services.Settings.Snapshot.VolumeControlEnabled
                    : services.Settings.Snapshot.LockKeysEnabled),
            enabled => ApplyStepSetting(services, step, enabled))
            .AutomationName(t.Message(Loc.App.EnableFeature));

    private static StackElement RenderNavigation(AppServices services, IntlAccessor t, OnboardingStep step)
    {
        bool first = OnboardingRules.Previous(step) is null;
        bool last = OnboardingRules.Next(step) is null;

        return HStack(8,
            Button(t.Message(Loc.App.Back), () => Move(services, OnboardingRules.Previous(step)))
                .AutomationName(t.Message(Loc.App.Back))
                .IsEnabled(!first),
            Button(last ? t.Message(Loc.App.Finish) : t.Message(Loc.App.Next), () => Move(services, OnboardingRules.Next(step)))
                .AutomationName(last ? t.Message(Loc.App.Finish) : t.Message(Loc.App.Next)));
    }

    private static void Move(AppServices services, OnboardingStep? next)
    {
        if (next is { } step)
        {
            services.OnboardingStore.Update(snapshot => snapshot with { CurrentStep = step });
            return;
        }
        services.OnboardingStore.Update(snapshot => snapshot with { IsComplete = true });
        services.Windows.Close(new WindowKey("onboarding"));
    }

    private static void ApplyStepSetting(AppServices services, OnboardingStep step, bool enabled)
        => services.Settings.Update(settings => step switch
        {
            OnboardingStep.Media => settings with { MediaFlyoutEnabled = enabled },
            OnboardingStep.Volume => settings with { VolumeControlEnabled = enabled },
            _ => settings with { LockKeysEnabled = enabled },
        });
}
