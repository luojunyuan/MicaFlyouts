using Kumo.H.NotifyIcon.Reactor;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Infrastructure.Localization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.App;

internal sealed partial class TrayIconHost : IDisposable
{
    private readonly ReactorHostControl _host = new();
    private int _disposed;

    private TrayIconHost()
    {
    }

    public static TrayIconHost Create(Func<bool> systemUsesLightTheme, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(systemUsesLightTheme);
        ArgumentNullException.ThrowIfNull(onClick);

        var host = new TrayIconHost();
        try
        {
            host._host.Mount(new TrayIconComponent(systemUsesLightTheme, onClick));
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

/// <summary>
/// Independent tray surface. Its lifetime is owned by <see cref="TrayIconHost"/>
/// rather than by any application window.
/// </summary>
internal sealed class TrayIconComponent : HNotifyComponent
{
    private static readonly object NoTrayIconSentinel = new();
    private static readonly WindowKey TrayIconKey = WindowKey.Of("tray-icon");

    private readonly Func<bool> _systemUsesLightTheme;
    private readonly EventHandler _clickHandler;

    public TrayIconComponent(Func<bool> systemUsesLightTheme, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(systemUsesLightTheme);
        ArgumentNullException.ThrowIfNull(onClick);

        _systemUsesLightTheme = systemUsesLightTheme;
        _clickHandler = (_, _) => onClick();
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
            useSymbol && _systemUsesLightTheme());
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
            AppRuntime.Services.OpenSettings,
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
