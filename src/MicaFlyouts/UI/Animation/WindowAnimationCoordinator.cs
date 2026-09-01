using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Flyouts;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Infrastructure.Interop;
using MicaFlyouts.Infrastructure.Windows;
using Microsoft.UI.Reactor;

namespace MicaFlyouts.UI.Animation;

public static class WindowAnimationCoordinator
{
    public static void Show(ReactorWindow window, SettingsSnapshot settings)
    {
        var monitor = MonitorService.Select(settings.FlyoutSelectedMonitor);
        var target = FlyoutPositionCalculator.Calculate(
            (FlyoutPosition)Math.Clamp(settings.Position, 0, 5),
            new ScreenRect(0, 0, 310, 116),
            monitor.WorkArea,
            settings.VolumeControlEnabled,
            settings.VolumeControlAboveMediaFlyout);
        double scale = monitor.DpiScale;
        window.SetPosition(target.Left / scale, target.Top / scale);
        window.SetOpacity(1);
        window.Show();
        NativeWindowApi.ConfigureOverlayWindow(
            WinRT.Interop.WindowNative.GetWindowHandle(window.NativeWindow),
            topmost: true,
            noActivate: true);
    }

    public static void Hide(ReactorWindow window, SettingsSnapshot settings)
    {
        _ = settings;
        window.SetOpacity(0);
        window.Hide();
    }
}
