using MicaFlyouts.Domain.Windows;
using MicaFlyouts.Infrastructure.Interop;

namespace MicaFlyouts.Infrastructure.Windows;

public sealed class MonitorService
{
    public static IReadOnlyList<MonitorSnapshot> GetMonitors()
        => NativeWindowApi.GetMonitors();

    public static MonitorSnapshot Select(int index)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
            return NativeWindowApi.GetMonitorForWindow(0);
        return monitors[Math.Clamp(index, 0, monitors.Count - 1)];
    }

    public static MonitorSnapshot ForWindow(nint hwnd)
        => NativeWindowApi.GetMonitorForWindow(hwnd);

    public static MonitorSnapshot ForCursor()
    {
        if (!NativeWindowApi.TryGetCursorPosition(out var point))
            return Select(0);
        return NativeWindowApi.GetMonitorForPoint(point.X, point.Y);
    }
}
