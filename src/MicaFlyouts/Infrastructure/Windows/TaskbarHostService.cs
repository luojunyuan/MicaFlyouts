using MicaFlyouts.App;
using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Taskbar;
using MicaFlyouts.Infrastructure.Interop;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using Microsoft.UI.Reactor;
using WinRT.Interop;

namespace MicaFlyouts.Infrastructure.Windows;

public sealed partial class TaskbarHostService(
    ISettingsStore settings,
    MediaStore media,
    TaskbarStore store,
    UiDispatcher dispatcher,
    MonitorService monitors,
    AppLogger logger) : IDisposable
{
    private readonly ISettingsStore _settings = settings;
    private readonly MediaStore _media = media;
    private readonly TaskbarStore _store = store;
    private readonly UiDispatcher _dispatcher = dispatcher;
    private readonly MonitorService _monitors = monitors;
    private readonly AppLogger _logger = logger;
    private readonly object _gate = new();
    private ReactorWindow? _widgetWindow;
    private ReactorWindow? _visualizerWindow;
    private CancellationTokenSource? _cancellation;
    private Action? _mediaUnsubscribe;
    private Action? _settingsUnsubscribe;
    private nint _attachedTaskbar;
    private int _disposed;

    public TaskbarSnapshot Snapshot => _store.Snapshot;

    public void Start()
    {
        if (_cancellation is not null || Volatile.Read(ref _disposed) != 0)
            return;
        _cancellation = new CancellationTokenSource();
        _mediaUnsubscribe = _media.Subscribe(Refresh);
        _settingsUnsubscribe = _settings.Subscribe(Refresh);
        Refresh();
        _ = MonitorExplorerAsync(_cancellation.Token);
    }

    public void AttachWidgetWindow(ReactorWindow window)
    {
        _widgetWindow = window;
        AttachWindows();
    }

    public void AttachVisualizerWindow(ReactorWindow window)
    {
        _visualizerWindow = window;
        AttachWindows();
    }

    public void DetachWindows()
    {
        _widgetWindow = null;
        _visualizerWindow = null;
        _attachedTaskbar = 0;
    }

    public void Refresh()
        => UiDispatcher.EnqueueOrRun(RefreshOnUiThread);

    private void RefreshOnUiThread()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var settings = _settings.Snapshot;
        var monitors = MonitorService.GetMonitors();
        if (monitors.Count == 0)
        {
            _store.Set(new TaskbarSnapshot(null, TaskbarOrientation.Horizontal, PixelRect.Empty, false, false, "-", "-", false, false));
            return;
        }

        int selectedIndex = Math.Clamp(settings.TaskbarWidgetSelectedMonitor, 0, monitors.Count - 1);
        var targetMonitor = monitors[selectedIndex];
        nint taskbar = FindTaskbarForMonitor(targetMonitor);
        PixelRect taskbarRect = PixelRect.Empty;
        bool available = taskbar != 0 && NativeWindowApi.TryGetWindowRect(taskbar, out taskbarRect);

        bool vertical = available && taskbarRect.Height > taskbarRect.Width;
        var media = _media.Snapshot.ActiveSession;
        var snapshot = new TaskbarSnapshot(
            targetMonitor.DeviceId,
            vertical ? TaskbarOrientation.Vertical : TaskbarOrientation.Horizontal,
            taskbarRect,
            _attachedTaskbar != 0,
            available,
            media?.Track.DisplayTitle ?? "-",
            media?.Track.DisplayArtist ?? "-",
            media?.Track.PlaybackStatus == MediaPlaybackStatus.Playing,
            false);
        _store.Set(snapshot);
        _attachedTaskbar = taskbar;
        AttachWindows();
    }

    private void AttachWindows()
    {
        if (_attachedTaskbar == 0)
            return;

        var settings = _settings.Snapshot;
        _ = _media.Snapshot.ActiveSession;
        var monitors = MonitorService.GetMonitors();
        PixelRect? nativeWidgets = TaskbarAutomationService.TryGetRect(_attachedTaskbar, "WidgetsButton", out var widgetRect)
            ? widgetRect
            : null;
        PixelRect? systemTray = TaskbarAutomationService.TryGetRect(_attachedTaskbar, "SystemTrayIcon", out var trayRect)
            ? trayRect
            : null;
        var layout = TaskbarLayoutCalculator.Calculate(new TaskbarLayoutInput(
            _store.Snapshot.TaskbarRect,
            MonitorService.ForWindow(_attachedTaskbar).DpiScale,
            100,
            40,
            84,
            40,
            (TaskbarPosition)Math.Clamp(settings.TaskbarWidgetPosition, 0, 2),
            (TaskbarPosition)Math.Clamp(settings.TaskbarVisualizerPosition, 0, 2),
            settings.TaskbarWidgetEnabled,
            settings.TaskbarVisualizerEnabled,
            settings.TaskbarWidgetPadding,
            settings.TaskbarWidgetManualPadding,
            settings.LegacyTaskbarWidthEnabled,
            monitors.Count > 0 && _store.Snapshot.TargetMonitorId == monitors[0].DeviceId,
            nativeWidgets,
            systemTray));

        AttachWindow(_widgetWindow, layout.WidgetRect, _attachedTaskbar);
        AttachWindow(_visualizerWindow, layout.VisualizerRect, _attachedTaskbar);
    }

    private static void AttachWindow(ReactorWindow? window, PixelRect rect, nint taskbar)
    {
        if (window is null || rect.IsEmpty)
            return;
        nint child = WindowNative.GetWindowHandle(window.NativeWindow);
        if (child == 0)
            return;
        // The host service only changes native parenting and geometry; content remains Reactor-owned.
        NativeWindowApi.ConfigureEmbeddedWindow(child, taskbar);
        var position = new System.Drawing.Point(rect.Left, rect.Top);
        _ = NativeWindowApi.TryScreenToClient(taskbar, ref position);
        _ = NativeWindowApi.SetWindowPosition(child, position.X, position.Y, rect.Width, rect.Height);
        window.SetOpacity(1);
        window.Show();
    }

    private static nint FindTaskbarForMonitor(MicaFlyouts.Domain.Windows.MonitorSnapshot target)
    {
        if (target.IsPrimary)
            return NativeWindowApi.FindWindow("Shell_TrayWnd");

        nint child = 0;
        while (true)
        {
            child = NativeWindowApi.FindChildWindow(0, child, "Shell_SecondaryTrayWnd");
            if (child == 0)
                break;
            var monitor = MonitorService.ForWindow(child);
            if (monitor.Bounds == target.Bounds)
                return child;
        }
        return 0;
    }

    private async Task MonitorExplorerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var taskbar = NativeWindowApi.FindWindow("Shell_TrayWnd");
                if (taskbar != _attachedTaskbar)
                    UiDispatcher.EnqueueOrRun(RefreshOnUiThread);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("TaskbarHostService.MonitorExplorerAsync stopped after OperationCanceledException.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _mediaUnsubscribe?.Invoke();
        _settingsUnsubscribe?.Invoke();
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        DetachWindows();
    }
}
