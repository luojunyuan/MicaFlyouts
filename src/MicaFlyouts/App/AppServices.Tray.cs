using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace MicaFlyouts.App;

public sealed partial class AppServices
{
    private UISettings? _trayUiSettings;
    private Action? _trayLocalizationUnsubscribe;
    private string? _trayIconResource;

    private void StartTray()
    {
        _trayLocalizationUnsubscribe = LocalizationStore.Subscribe(() => UiDispatcher.EnqueueOrRun(RefreshTrayMenu));
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
        UiDispatcher.TryEnqueue(SyncTray);

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
            bool useSymbol = Settings.Snapshot.NIconSymbol;
            string resource = TrayIconAssets.SelectResource(useSymbol, useSymbol && SystemUsesLightTheme());
            if (_tray is null)
            {
                using var icon = TrayIconAssets.Load(resource);
                var locale = Localization.Snapshot;
                _tray = HNotifyIconApp.OpenTrayIcon(new HNotifyIconSpec(
                    HNotifyIcon.FromDrawingIcon(icon), AppBranding.Name, CreateTrayMenu())
                {
                    FlowDirection = locale.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                    FontFamily = locale.FontFamily,
                });
                _tray.LeftClick += Tray_Click;
            }
            else if (!string.Equals(_trayIconResource, resource, StringComparison.Ordinal))
            {
                using var icon = TrayIconAssets.Load(resource);
                _tray.UpdateIcon(HNotifyIcon.FromDrawingIcon(icon));
            }
            _trayIconResource = resource;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Tray icon unavailable: {exception.Message}");
        }
    }

    private MenuFlyoutItemBase[] CreateTrayMenu() => TrayMenu.Create(
        Localization,
        OpenSettings,
        () => OpenUrl("https://github.com/unchihugo/FluentFlyout"),
        () => OpenUrl(_logger.LogDirectory),
        () => OpenUrl("https://github.com/unchihugo/FluentFlyout/issues/new/choose"),
        Exit);

    private void RefreshTrayMenu()
    {
        if (_tray is null || Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _exitRequested) != 0)
            return;
        CloseTray();
        SyncTray();
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
        var tray = _tray;
        _tray = null;
        _trayIconResource = null;
        if (tray is not null)
        {
            tray.LeftClick -= Tray_Click;
            tray.Dispose();
        }
    }

    private void StopTray()
    {
        _trayLocalizationUnsubscribe?.Invoke();
        _trayLocalizationUnsubscribe = null;
        if (_trayUiSettings is not null)
        {
            _trayUiSettings.ColorValuesChanged -= OnTrayColorsChanged;
            _trayUiSettings = null;
        }
        CloseTray();
    }
}
