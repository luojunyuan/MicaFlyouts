using MicaFlyouts.App;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using Microsoft.UI.Reactor;

namespace MicaFlyouts.Features.Tray;

/// <summary>
/// Owns the tray unit: a hidden host window whose <see cref="TrayIconComponent"/>
/// keeps the tray icon alive through <c>UseTrayIcon</c>. The window is never
/// shown (<c>ActivateOnOpen = false</c>) and is independent from every
/// application window.
/// </summary>
public sealed partial class TrayIconFeature : IDisposable
{
    private static readonly WindowKey TrayHostKey = WindowKey.Of("tray-host");

    private readonly SettingsStore _settings;
    private readonly AppLogger _logger;
    private Action? _settingsUnsubscribe;
    private ReactorWindow? _hostWindow;
    private int _started;
    private int _disposed;

    public TrayIconFeature(SettingsStore settings, AppLogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0
            || Interlocked.Exchange(ref _started, 1) != 0)
            return;

        _settingsUnsubscribe = _settings.Subscribe(OnSettingsChanged);
        Synchronize();
    }

    private void OnSettingsChanged() => UiDispatcher.EnqueueOrRun(Synchronize);

    private void Synchronize()
    {
        if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _started) == 0)
            return;
        if (_settings.Snapshot.NIconHide)
            CloseHostWindow();
        else
            OpenHostWindow();
    }

    private void OpenHostWindow()
    {
        if (_hostWindow is not null)
            return;

        try
        {
            var window = ReactorApp.OpenWindow(
                new WindowSpec
                {
                    Title = "Mica Flyouts Tray Host",
                    Key = TrayHostKey,
                    Width = 100,
                    Height = 40,
                    ResizeMode = WindowResizeMode.NoResize,
                    ShowInTaskbar = false,
                    ShowInSwitcher = false,
                    NoActivate = true,
                    ActivateOnOpen = false,
                },
                static () => new TrayIconComponent());
            window.Closed += OnHostWindowClosed;
            _hostWindow = window;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray icon unavailable: {exception.Message}");
            CloseHostWindow();
        }
    }

    private void OnHostWindowClosed(object? sender, EventArgs e) => _hostWindow = null;

    private void CloseHostWindow()
    {
        var window = _hostWindow;
        _hostWindow = null;
        if (window is null)
            return;

        window.Closed -= OnHostWindowClosed;
        try
        {
            window.Close();
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray icon release failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _settingsUnsubscribe?.Invoke();
        _settingsUnsubscribe = null;
        CloseHostWindow();
    }
}
