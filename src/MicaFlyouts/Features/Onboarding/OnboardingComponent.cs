using MicaFlyouts.App;
using MicaFlyouts.Domain.Onboarding;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var step = onboarding.CurrentStep;

        return Border(Grid(
                [GridSize.Star()],
                [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                RenderHeader(t).Grid(row: 0).Margin(bottom: 16),
                RenderStep(services, settings, t, step).Grid(row: 1),
                RenderNavigation(services, t, step).Grid(row: 2).Margin(top: 24)))
            .Padding(36)
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8);
    }

    private static StackElement RenderHeader(IntlAccessor t)
        => HStack(12,
            Icon(AppBranding.TitleBarIcon).Width(36).Height(36),
            TextBlock(t.Message(Loc.App.OnboardingTitle))
                .FontSize(28)
                .SemiBold()
                .VerticalAlignment(VerticalAlignment.Center));

    private static GridElement RenderStep(
        AppServices services,
        SettingsSnapshot settings,
        IntlAccessor t,
        OnboardingStep step)
    {
        string title = StepTitle(step, t);
        var image = RenderIllustration(StepImage(step), t.Message(Loc.App.OnboardingIllustration, ("feature", title)));

        return Grid(
            [GridSize.Star(1.2), GridSize.Star()],
            [GridSize.Star()],
            Border(image)
                .Padding(20)
                .Margin(top: 12, right: 10)
                .Grid(row: 0, column: 0),
            Border(RenderStepDetails(services, settings, t, step, title))
                .Padding(20, 20, 0, 0)
                .Margin(top: 12, left: 10)
                .Grid(row: 0, column: 1));
    }

    private static BorderElement RenderIllustration(string imageUri, string automationName)
        => (Border(Empty()) with
        {
            Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(imageUri, UriKind.Absolute)),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            },
        })
            .Width(390)
            .Height(220)
            .CornerRadius(10)
            .WithBorder(SurfaceStroke, 1)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .AutomationName(automationName);

    private static ScrollViewerElement RenderStepDetails(
        AppServices services,
        SettingsSnapshot settings,
        IntlAccessor t,
        OnboardingStep step,
        string title)
        => ScrollViewer(VStack(16,
                TextBlock(title)
                    .FontSize(24)
                    .SemiBold()
                    .TextWrapping(TextWrapping.Wrap),
                TextBlock(StepDescription(step, t))
                    .FontSize(15)
                    .TextWrapping(TextWrapping.Wrap)
                    .Opacity(0.7)
                    .Margin(bottom: 16),
                RenderStepSettings(services, settings, t, step)))
            .Set(scrollViewer =>
            {
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            });

    private static StackElement RenderStepSettings(
        AppServices services,
        SettingsSnapshot settings,
        IntlAccessor t,
        OnboardingStep step)
        => step switch
        {
            OnboardingStep.Media => RenderSettingToggle(
                t,
                t.Message(Loc.App.TextBlockText1),
                null,
                settings.MediaFlyoutEnabled,
                enabled => services.Settings.Update(current => current with { MediaFlyoutEnabled = enabled })),
            OnboardingStep.Volume => VStack(20,
                RenderSettingToggle(
                    t,
                    t.Message(Loc.App.EnableVolumeFlyoutTitle),
                    t.Message(Loc.App.EnableVolumeFlyoutDescription),
                    settings.VolumeControlEnabled,
                    enabled => services.Settings.Update(current => current with { VolumeControlEnabled = enabled })),
                RenderSettingToggle(
                    t,
                    t.Message(Loc.App.VolumeMixerSectionTitle),
                    t.Message(Loc.App.EnableVolumeMixerDescription),
                    settings.VolumeMixerEnabled,
                    enabled => services.Settings.Update(current => current with { VolumeMixerEnabled = enabled }),
                    isEnabled: settings.VolumeControlEnabled,
                    opacity: settings.VolumeControlEnabled ? 1 : 0.5)),
            OnboardingStep.LockKeys => RenderSettingToggle(
                t,
                t.Message(Loc.App.EnableLockKeysTitle),
                t.Message(Loc.App.EnableLockKeysDescription),
                settings.LockKeysEnabled,
                enabled => services.Settings.Update(current => current with { LockKeysEnabled = enabled })),
            _ => VStack(20,
                RenderSettingToggle(
                    t,
                    t.Message(Loc.App.TaskbarWidgetEnabledTitle),
                    t.Message(Loc.App.TaskbarWidgetEnabledDescription),
                    settings.TaskbarWidgetEnabled,
                    enabled => services.Settings.Update(current => current with { TaskbarWidgetEnabled = enabled })),
                RenderSettingToggle(
                    t,
                    t.Message(Loc.App.TaskbarVisualizerEnabledTitle),
                    t.Message(Loc.App.TaskbarVisualizerEnabledDescription),
                    settings.TaskbarVisualizerEnabled,
                    enabled => services.Settings.Update(current => current with { TaskbarVisualizerEnabled = enabled }))),
        };

    private static StackElement RenderSettingToggle(
        IntlAccessor t,
        string title,
        string? description,
        bool value,
        Action<bool> onChanged,
        bool isEnabled = true,
        double opacity = 1)
        => VStack(6,
                TextBlock(title).FontSize(14).SemiBold(),
                description is null
                    ? null
                    : TextBlock(description)
                        .FontSize(12)
                        .TextWrapping(TextWrapping.Wrap)
                        .Opacity(0.75),
                HStack(8,
                    ToggleSwitch(Optional<bool>.Of(value), onChanged)
                        .AutomationName(title)
                        .IsEnabled(isEnabled),
                    TextBlock(value ? t.Message(Loc.App.Enabled) : t.Message(Loc.App.Disabled))
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Opacity(0.7)))
            .Opacity(opacity);

    private static GridElement RenderNavigation(AppServices services, IntlAccessor t, OnboardingStep step)
    {
        bool first = OnboardingRules.Previous(step) is null;
        bool last = OnboardingRules.Next(step) is null;
        int stepNumber = (int)step + 1;
        int stepCount = Enum.GetValues<OnboardingStep>().Length;
        string progress = string.Format(CultureInfo.CurrentCulture, t.Message(Loc.App.OnboardingStepsCounter), stepNumber, stepCount);

        return Grid(
            [GridSize.Star(), GridSize.Auto],
            [GridSize.Auto],
            TextBlock(progress)
                .VerticalAlignment(VerticalAlignment.Center)
                .Opacity(0.5)
                .Grid(row: 0, column: 0),
            HStack(8,
                first
                    ? null
                    : Button(t.Message(Loc.App.Back), () => Move(services, OnboardingRules.Previous(step)))
                        .SubtleButton()
                        .AutomationName(t.Message(Loc.App.Back)),
                Button(last ? t.Message(Loc.App.Finish) : t.Message(Loc.App.Next),
                        () => Move(services, OnboardingRules.Next(step)))
                    .AccentButton()
                    .Width(112)
                    .Height(44)
                    .AutomationName(last ? t.Message(Loc.App.Finish) : t.Message(Loc.App.Next)))
                .Grid(row: 0, column: 1));
    }

    private static string StepImage(OnboardingStep step)
        => step switch
        {
            OnboardingStep.Media => "ms-appx:///Assets/Onboarding/MediaFlyout.png",
            OnboardingStep.Volume => "ms-appx:///Assets/FluentFlyoutVolumeDemo.png",
            OnboardingStep.LockKeys => "ms-appx:///Assets/Onboarding/LockKeysFlyout.png",
            _ => "ms-appx:///Assets/Onboarding/Taskbar.png",
        };

    private static string StepTitle(OnboardingStep step, IntlAccessor t)
        => step switch
        {
            OnboardingStep.Media => t.Message(Loc.App.MediaFlyoutTitle),
            OnboardingStep.Volume => t.Message(Loc.App.VolumeFlyoutTitle),
            OnboardingStep.LockKeys => t.Message(Loc.App.LockKeysCustomizationTitle),
            _ => t.Message(Loc.App.TaskbarWidgetCustomizationTitle),
        };

    private static string StepDescription(OnboardingStep step, IntlAccessor t)
        => step switch
        {
            OnboardingStep.Media => t.Message(Loc.App.MediaFlyoutDescription),
            OnboardingStep.Volume => t.Message(Loc.App.VolumeFlyoutDescription),
            OnboardingStep.LockKeys => t.Message(Loc.App.LockKeysDescription),
            _ => t.Message(Loc.App.TaskbarWidgetDescription),
        };

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
}
