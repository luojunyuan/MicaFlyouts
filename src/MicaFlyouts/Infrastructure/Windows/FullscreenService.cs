using MicaFlyouts.Infrastructure.Interop;

namespace MicaFlyouts.Infrastructure.Windows;

public sealed class FullscreenService
{
    public static bool IsFullscreenApplicationRunning()
    {
        if (NativeWindowApi.IsFullscreenPresentationActive())
            return true;

        var foreground = NativeWindowApi.GetForegroundWindow();
        if (foreground == 0 || !NativeWindowApi.TryGetWindowRect(foreground, out var rect))
            return false;

        var monitor = NativeWindowApi.GetMonitorForWindow(foreground);
        return rect.Left <= monitor.Bounds.Left
            && rect.Top <= monitor.Bounds.Top
            && rect.Right >= monitor.Bounds.Right
            && rect.Bottom >= monitor.Bounds.Bottom;
    }
}
