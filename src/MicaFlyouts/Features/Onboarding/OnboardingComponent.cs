using MicaFlyouts.App;
using MicaFlyouts.Domain.Onboarding;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.Onboarding;

public sealed class OnboardingWindowComponent : Component
{
    public override Element Render() => Component<OnboardingComponent>();
}

public sealed class OnboardingComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var onboarding = UseExternalStore(services.OnboardingStore.Subscribe, () => services.OnboardingStore.Snapshot);
        var step = onboarding.CurrentStep;
        var image = step switch
        {
            OnboardingStep.Media => "ms-appx:///Assets/Onboarding/MediaFlyout.png",
            OnboardingStep.Volume => "ms-appx:///Assets/Onboarding/Taskbar.png",
            _ => "ms-appx:///Assets/Onboarding/LockKeysFlyout.png",
        };
        var title = step switch
        {
            OnboardingStep.Media => "Media flyout",
            OnboardingStep.Volume => "Volume controls",
            _ => "Lock keys",
        };
        bool first = OnboardingRules.Previous(step) is null;
        bool last = OnboardingRules.Next(step) is null;
        var settingToggle = ToggleSwitch(
            step == OnboardingStep.Media
                ? Optional<bool>.Of(services.Settings.Snapshot.MediaFlyoutEnabled)
                : Optional<bool>.Of(step == OnboardingStep.Volume
                    ? services.Settings.Snapshot.VolumeControlEnabled
                    : services.Settings.Snapshot.LockKeysEnabled),
            enabled => ApplyStepSetting(services, step, enabled))
            .AutomationName("Enable this feature");
        var introduction = HStack(16,
            ((Image(image) with { Stretch = "UniformToFill" }).Width(420).Height(280))
                .AutomationName($"{title} illustration"),
            VStack(12,
                Heading("Mica Flyouts"),
                SubHeading(title),
                TextBlock("A native Windows overlay for media, volume, and keyboard status."),
                settingToggle));
        var navigation = HStack(8,
            Button("Back", () => Move(services, OnboardingRules.Previous(step)))
                .AutomationName("Back")
                .IsEnabled(!first),
            Button(last ? "Finish" : "Next", () => Move(services, OnboardingRules.Next(step)))
                .AutomationName(last ? "Finish" : "Next"));
        return Border(VStack(16, introduction, navigation))
            .Padding(28)
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8);
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
