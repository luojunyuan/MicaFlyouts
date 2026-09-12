using Kumo.H.NotifyIcon.Reactor;
using MicaFlyouts.App;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Infrastructure.Localization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.Tray;

/// <summary>
/// Tray surface mounted as the root of <see cref="TrayIconFeature"/>'s hidden
/// host window rather than by any application window.
/// </summary>
public sealed class TrayIconComponent : HNotifyComponent
{
    private static readonly object NoTrayIconSentinel = new();
    private static readonly WindowKey TrayIconKey = WindowKey.Of("tray-icon");

    private readonly EventHandler _clickHandler;

    public TrayIconComponent()
    {
        _clickHandler = static (_, _) => OnClick();
    }

    public override Element Render()
    {
        var services = AppRuntime.Services;
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var localization = UseExternalStore(
            services.LocalizationStore.Subscribe,
            () => services.LocalizationStore.Snapshot);

        bool useSymbol = settings.NIconSymbol;
        string iconResource = TrayIconAssets.SelectResource(
            useSymbol,
            useSymbol && SystemUsesLightTheme());
        var icon = UseMemo(() => TrayIconAssets.Load(iconResource), iconResource);
        var contextMenu = UseMemo(
            () => CreateContextMenu(services.Localization, localization),
            localization.ResourceVersion,
            localization.IsRightToLeft,
            localization.FontFamily);
        var spec = new HNotifyTrayIconSpec(
            icon,
            AppBranding.Name,
            TrayIconKey,
            isVisible: !settings.NIconHide)
        {
            ContextMenu = contextMenu,
            MenuActivation = HNotifyMenuActivation.RightClick,
            ContextMenuMode = HNotifyContextMenuMode.SecondWindow,
            ContextMenuTheme = HNotifyContextMenuTheme.System,
        };
        var tray = UseTrayIcon(spec);

        UseEffect(() => () => icon.Dispose(), icon);
        UseEffect(() =>
        {
            if (tray is null)
                return (Action)(static () => { });

            tray.Click += _clickHandler;
            return () => tray.Click -= _clickHandler;
        }, tray ?? NoTrayIconSentinel);

        return Empty();
    }

    private static Microsoft.UI.Xaml.Controls.MenuFlyout CreateContextMenu(
        LocalizationService localization,
        LocalizationSnapshot locale)
    {
        var menu = HNotifyMenu.Create(TrayMenu.Create(
            localization,
            AppRuntime.Services.Commands.ShowSettings,
            () => OpenUrl("https://github.com/unchihugo/FluentFlyout"),
            () => OpenUrl(AppRuntime.Services.Logger.LogDirectory),
            () => OpenUrl("https://github.com/unchihugo/FluentFlyout/issues/new/choose"),
            AppRuntime.Services.Commands.Exit));

        var presenterStyle = new Style
        {
            TargetType = typeof(MenuFlyoutPresenter),
        };
        presenterStyle.Setters.Add(new Setter(
            FrameworkElement.FlowDirectionProperty,
            locale.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight));
        presenterStyle.Setters.Add(new Setter(
            Control.FontFamilyProperty,
            new FontFamily(locale.FontFamily)));
        menu.MenuFlyoutPresenterStyle = presenterStyle;
        return menu;
    }

    // The taskbar follows SystemUsesLightTheme, so symbol icons use the black
    // glyph on a light taskbar and the white glyph on a dark one.
    private static bool SystemUsesLightTheme()
    {
        using var personalize = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return personalize?.GetValue("SystemUsesLightTheme") is int lightTheme && lightTheme != 0;
    }

    private static void OnClick()
    {
        var services = AppRuntime.Services;
        if (services.Settings.Snapshot.NIconLeftClick == 1)
            services.Commands.ShowMediaFlyout();
        else
            services.Commands.ShowSettings();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
