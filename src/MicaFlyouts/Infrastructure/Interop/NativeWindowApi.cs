using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Windows;
using Win32 = Windows.Win32;
using Win32Foundation = Windows.Win32.Foundation;
using Win32Dwm = Windows.Win32.Graphics.Dwm;
using Win32Gdi = Windows.Win32.Graphics.Gdi;
using Win32HiDpi = Windows.Win32.UI.HiDpi;
using Win32Keyboard = Windows.Win32.UI.Input.KeyboardAndMouse;
using Win32Messaging = Windows.Win32.UI.WindowsAndMessaging;
using Win32Com = Windows.Win32.System.Com;

namespace MicaFlyouts.Infrastructure.Interop;

/// <summary>
/// The only managed boundary around the generated CsWin32 surface.
/// </summary>
public static unsafe class NativeWindowApi
{
    private const int MonitorInfoPrimary = 1;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);

    public static nint FindWindow(string className, string? title = null)
        => (nint)Win32.PInvoke.FindWindow(className, title);

    public static nint FindChildWindow(nint parent, string className, string? title = null)
        => FindChildWindow(parent, 0, className, title);

    public static nint FindChildWindow(nint parent, nint childAfter, string className, string? title = null)
        => (nint)Win32.PInvoke.FindWindowEx(
            (Win32Foundation.HWND)parent,
            (Win32Foundation.HWND)childAfter,
            className,
            title);

    public static string GetClassName(nint hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        int length = Win32.PInvoke.GetClassName((Win32Foundation.HWND)hwnd, buffer);
        return length <= 0 ? string.Empty : new string(buffer[..length]);
    }

    public static bool TryGetWindowRect(nint hwnd, out PixelRect rect)
    {
        rect = PixelRect.Empty;
        if (hwnd == 0 || !Win32.PInvoke.GetWindowRect((Win32Foundation.HWND)hwnd, out var native))
            return false;

        rect = new PixelRect(native.left, native.top, native.right - native.left, native.bottom - native.top);
        return !rect.IsEmpty;
    }

    public static bool TryGetExtendedFrameRect(nint hwnd, out PixelRect rect)
    {
        rect = PixelRect.Empty;
        Span<byte> bytes = stackalloc byte[sizeof(Win32Foundation.RECT)];
        var result = Win32.PInvoke.DwmGetWindowAttribute(
            (Win32Foundation.HWND)hwnd,
            Win32Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            bytes);
        if (result.Value != 0)
            return false;

        var native = MemoryMarshal.Read<Win32Foundation.RECT>(bytes);
        rect = new PixelRect(native.left, native.top, native.right - native.left, native.bottom - native.top);
        return !rect.IsEmpty;
    }

    public static nint SetParent(nint child, nint parent)
        => (nint)Win32.PInvoke.SetParent((Win32Foundation.HWND)child, (Win32Foundation.HWND)parent);

    public static void ConfigureEmbeddedWindow(nint child, nint parent)
    {
        if (child == 0 || parent == 0)
            return;
        var style = GetWindowLongPtr(child, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE).ToInt64();
        style = (style & ~WsPopup) | WsChild;
        SetWindowLongPtr(child, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, new nint(style));
        SetParent(child, parent);
        _ = SetWindowPosition(child, 0, 0, 1, 1);
    }

    public static bool SetWindowPosition(
        nint hwnd,
        int left,
        int top,
        int width,
        int height,
        bool noActivate = true,
        bool noZOrder = true)
    {
        var flags = Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
        if (noActivate)
            flags |= Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (noZOrder)
            flags |= Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
        return Win32.PInvoke.SetWindowPos(
            (Win32Foundation.HWND)hwnd,
            default,
            left,
            top,
            width,
            height,
            flags);
    }

    public static bool SetVisible(nint hwnd, bool visible, bool activate = false)
        => Win32.PInvoke.ShowWindow(
            (Win32Foundation.HWND)hwnd,
            visible
                ? activate ? Win32Messaging.SHOW_WINDOW_CMD.SW_SHOW : Win32Messaging.SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE
                : Win32Messaging.SHOW_WINDOW_CMD.SW_HIDE);

    public static bool SetForeground(nint hwnd)
        => Win32.PInvoke.SetForegroundWindow((Win32Foundation.HWND)hwnd);

    public static bool IsMinimized(nint hwnd)
        => Win32.PInvoke.IsIconic((Win32Foundation.HWND)hwnd);

    public static uint GetDpi(nint hwnd)
        => hwnd == 0 ? 96u : Math.Max(96u, Win32.PInvoke.GetDpiForWindow((Win32Foundation.HWND)hwnd));

    public static nint GetParent(nint hwnd)
        => (nint)Win32.PInvoke.GetParent((Win32Foundation.HWND)hwnd);

    public static nint GetForegroundWindow()
        => (nint)Win32.PInvoke.GetForegroundWindow();

    public static bool TryGetCursorPosition(out System.Drawing.Point point)
        => Win32.PInvoke.GetCursorPos(out point);

    public static bool TryScreenToClient(nint hwnd, ref System.Drawing.Point point)
        => Win32.PInvoke.ScreenToClient((Win32Foundation.HWND)hwnd, ref point);

    public static bool TryCreateUiAutomation<T>(Guid classId, out T? automation)
        where T : class
    {
        var result = Win32.PInvoke.CoCreateInstance(
            in classId,
            null,
            Win32Com.CLSCTX.CLSCTX_INPROC_SERVER,
            out automation);
        return result.Value >= 0 && automation is not null;
    }

    public static uint GetWindowThreadProcessId(nint hwnd, out uint processId)
        => Win32.PInvoke.GetWindowThreadProcessId((Win32Foundation.HWND)hwnd, out processId);

    private static nint GetWindowLongPtr(nint hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX index)
        => Win32.PInvoke.GetWindowLongPtr((Win32Foundation.HWND)hwnd, index);

    private static nint SetWindowLongPtr(nint hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX index, nint value)
        => Win32.PInvoke.SetWindowLongPtr((Win32Foundation.HWND)hwnd, index, value);

    public static void ConfigureOverlayWindow(nint hwnd, bool topmost, bool noActivate)
    {
        if (hwnd == 0)
            return;

        var style = GetWindowLongPtr(hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE).ToInt64();
        style |= WsExToolWindow;
        style &= ~WsExAppWindow;
        if (noActivate)
            style |= WsExNoActivate;
        SetWindowLongPtr(hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, new nint(style));

        var insertAfter = topmost ? (Win32Foundation.HWND)new nint(-1) : default;
        var flags = Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE
            | Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | Win32Messaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        _ = Win32.PInvoke.SetWindowPos((Win32Foundation.HWND)hwnd, insertAfter, 0, 0, 0, 0, flags);
    }

    public static bool SetOpacity(nint hwnd, byte opacity)
    {
        if (hwnd == 0)
            return false;

        var style = GetWindowLongPtr(hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, Win32Messaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, new nint(style | WsExLayered));
        return Win32.PInvoke.SetLayeredWindowAttributes(
            (Win32Foundation.HWND)hwnd,
            default,
            opacity,
            Win32Messaging.LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
    }

    public static bool SetRoundedRegion(nint hwnd, int width, int height)
    {
        if (hwnd == 0 || width <= 0 || height <= 0)
            return false;

        var region = Win32.PInvoke.CreateRectRgn(0, 0, width, height);
        if (region.IsNull)
            return false;

        // Reactor owns the window; a successful SetWindowRgn takes ownership of the region.
        if (Win32.PInvoke.SetWindowRgn((Win32Foundation.HWND)hwnd, region, true) != 0)
            return true;

        _ = Win32.PInvoke.DeleteObject((Win32Gdi.HGDIOBJ)region);
        return false;
    }

    public static IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        List<MonitorSnapshot> monitors = [];
        var handle = GCHandle.Alloc(monitors);
        try
        {
            _ = Win32.PInvoke.EnumDisplayMonitors(
                default,
                null,
                &MonitorCallback,
                (Win32Foundation.LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < monitors.Count; i++)
            monitors[i] = monitors[i] with { Index = i };

        return [..
            monitors
                .OrderByDescending(static monitor => monitor.IsPrimary)
                .ThenBy(static monitor => monitor.Bounds.Left)];
    }

    public static MonitorSnapshot GetMonitorForWindow(nint hwnd)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
            return new MonitorSnapshot(0, "DISPLAY0", "Primary display", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true, 96, 96);

        if (hwnd == 0)
            return monitors.FirstOrDefault(static monitor => monitor.IsPrimary) ?? monitors[0];

        var nativeMonitor = Win32.PInvoke.MonitorFromWindow(
            (Win32Foundation.HWND)hwnd,
            Win32Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (nativeMonitor.IsNull)
            return monitors[0];

        if (TryGetMonitorRect(nativeMonitor, out var bounds, out var work, out var primary, out var dpi))
            return new MonitorSnapshot(0, "DISPLAY0", "Display", bounds, work, primary, dpi, dpi);

        return monitors[0];
    }

    public static MonitorSnapshot GetMonitorForPoint(int x, int y)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
            return new MonitorSnapshot(0, "DISPLAY0", "Primary display", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true, 96, 96);

        var nativeMonitor = Win32.PInvoke.MonitorFromPoint(
            new System.Drawing.Point(x, y),
            Win32Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (nativeMonitor.IsNull)
            return monitors[0];

        if (TryGetMonitorRect(nativeMonitor, out var bounds, out var work, out var primary, out var dpi))
            return new MonitorSnapshot(0, "DISPLAY0", "Display", bounds, work, primary, dpi, dpi);

        return monitors[0];
    }

    public static bool IsFullscreenPresentationActive()
    {
        var result = Win32.PInvoke.SHQueryUserNotificationState(out var state);
        return result.Value == 0 && state is
            Win32.UI.Shell.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN or
            Win32.UI.Shell.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY;
    }

    public static uint RegisterWindowMessage(string name)
        => Win32.PInvoke.RegisterWindowMessage(name);

    public static bool RegisterShellHookWindow(nint hwnd)
        => Win32.PInvoke.RegisterShellHookWindow((Win32Foundation.HWND)hwnd);

    public static bool DeregisterShellHookWindow(nint hwnd)
        => Win32.PInvoke.DeregisterShellHookWindow((Win32Foundation.HWND)hwnd);

    public static void SendKeyEvent(byte virtualKey, bool keyUp)
        => Win32.PInvoke.keybd_event(
            virtualKey,
            0,
            keyUp ? Win32Keyboard.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0,
            0);

    private static bool TryGetMonitorRect(
        Win32Gdi.HMONITOR monitor,
        out ScreenRect bounds,
        out ScreenRect work,
        out bool primary,
        out uint dpi)
    {
        bounds = ScreenRect.Empty;
        work = ScreenRect.Empty;
        primary = false;
        dpi = 96;
        var info = new Win32Gdi.MONITORINFO { cbSize = (uint)sizeof(Win32Gdi.MONITORINFO) };
        if (!Win32.PInvoke.GetMonitorInfo(monitor, ref info))
            return false;

        bounds = ToScreenRect(info.rcMonitor);
        work = ToScreenRect(info.rcWork);
        primary = (info.dwFlags & MonitorInfoPrimary) != 0;
        if (Win32.PInvoke.GetDpiForMonitor(
            monitor,
            Win32HiDpi.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
            out var dpiX,
            out _).Value == 0)
        {
            dpi = dpiX;
        }
        return true;
    }

    private static ScreenRect ToScreenRect(Win32Foundation.RECT rect)
        => new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static Win32Foundation.BOOL MonitorCallback(
        Win32Gdi.HMONITOR monitor,
        Win32Gdi.HDC _,
        Win32Foundation.RECT* __,
        Win32Foundation.LPARAM data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        if (handle.Target is List<MonitorSnapshot> monitors
            && TryGetMonitorRect(monitor, out var bounds, out var work, out var primary, out var dpi))
        {
            monitors.Add(new MonitorSnapshot(monitors.Count, $"DISPLAY{monitors.Count}", "Display", bounds, work, primary, dpi, dpi));
        }
        return true;
    }
}
