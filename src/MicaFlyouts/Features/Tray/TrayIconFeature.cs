using Microsoft.Win32;
using MicaFlyouts.App;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Windows.UI.ViewManagement;

namespace MicaFlyouts.Features.Tray;

/// <summary>
/// Owns the tray surface and its non-window lifetime.
/// </summary>
public sealed partial class TrayIconFeature : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly AppLogger _logger;
    private Action? _settingsUnsubscribe;
    private UISettings? _uiSettings;
    private TrayIconHost? _host;
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
        try
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnTrayColorsChanged;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray theme watcher unavailable: {exception.Message}");
        }

        Synchronize();
    }

    private void OnSettingsChanged() =>
        UiDispatcher.EnqueueOrRun(Synchronize);

    private void OnTrayColorsChanged(UISettings sender, object args) =>
        UiDispatcher.TryEnqueue(() =>
        {
            CloseHost();
            Synchronize();
        });

    private void Synchronize()
    {
        if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _started) == 0)
            return;
        if (_settings.Snapshot.NIconHide)
        {
            CloseHost();
            return;
        }

        try
        {
            if (_host is null)
                _host = TrayIconHost.Create(SystemUsesLightTheme);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray icon unavailable: {exception.Message}");
            CloseHost();
        }
    }

    private bool SystemUsesLightTheme()
    {
        using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        if (personalize?.GetValue("SystemUsesLightTheme") is int lightTheme)
            return lightTheme != 0;
        var foreground = _uiSettings?.GetColorValue(UIColorType.Foreground);
        return foreground is { } color && color.R + color.G + color.B < 384;
    }

    private void CloseHost()
    {
        var host = _host;
        _host = null;
        host?.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _settingsUnsubscribe?.Invoke();
        _settingsUnsubscribe = null;
        if (_uiSettings is not null)
        {
            _uiSettings.ColorValuesChanged -= OnTrayColorsChanged;
            _uiSettings = null;
        }
        CloseHost();
    }
}

internal sealed partial class TrayIconHost : IDisposable
{
    private readonly ReactorHostControl _host = new();
    private int _disposed;

    private TrayIconHost()
    {
    }

    public static TrayIconHost Create(Func<bool> systemUsesLightTheme)
    {
        ArgumentNullException.ThrowIfNull(systemUsesLightTheme);

        var host = new TrayIconHost();
        try
        {
            host._host.Mount(new TrayIconComponent(systemUsesLightTheme));
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _host.Dispose();
    }
}
