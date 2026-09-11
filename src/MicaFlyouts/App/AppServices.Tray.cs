using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace MicaFlyouts.App;

public sealed partial class AppServices
{
    private UISettings? _trayUiSettings;
    private TrayIconHost? _trayHost;

    private void StartTray()
    {
        try
        {
            _trayUiSettings = new UISettings();
            _trayUiSettings.ColorValuesChanged += OnTrayColorsChanged;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray theme watcher unavailable: {exception.Message}");
        }
        SyncTray();
    }

    private void OnTrayColorsChanged(UISettings sender, object args) =>
        UiDispatcher.TryEnqueue(() =>
        {
            CloseTray();
            SyncTray();
        });

    private void SyncTray()
    {
        if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _exitRequested) != 0)
            return;
        if (Settings.Snapshot.NIconHide)
        {
            CloseTray();
            return;
        }

        try
        {
            if (_trayHost is null)
                _trayHost = TrayIconHost.Create(SystemUsesLightTheme, Tray_Click);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray icon unavailable: {exception.Message}");
            CloseTray();
        }
    }

    private bool SystemUsesLightTheme()
    {
        using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        if (personalize?.GetValue("SystemUsesLightTheme") is int lightTheme)
            return lightTheme != 0;
        var foreground = _trayUiSettings?.GetColorValue(UIColorType.Foreground);
        return foreground is { } color && color.R + color.G + color.B < 384;
    }

    private void CloseTray()
    {
        var trayHost = _trayHost;
        _trayHost = null;
        trayHost?.Dispose();
    }

    private void StopTray()
    {
        if (_trayUiSettings is not null)
        {
            _trayUiSettings.ColorValuesChanged -= OnTrayColorsChanged;
            _trayUiSettings = null;
        }
        CloseTray();
    }
}
