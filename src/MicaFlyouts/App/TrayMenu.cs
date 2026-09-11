using MicaFlyouts.Infrastructure.Localization;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.App;

public static class TrayMenu
{
    public static MenuFlyoutItemBase[] Create(
        LocalizationService localization,
        Action openSettings,
        Action openRepository,
        Action openLogs,
        Action reportBug,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return
        [
            MenuItem(localization.Get("TrayIcon_SettingsOption", "Settings"), openSettings, icon: TrayMenuIcons.Settings20),
            MenuSeparator(),
            MenuItem(localization.Get("TrayIcon_GitHubRepositoryOption", "Repository"), openRepository, icon: TrayMenuIcons.DocumentChevronDouble20),
            MenuItem(localization.Get("TrayIcon_ViewLogsOption", "View logs"), openLogs, icon: TrayMenuIcons.FolderOpen20),
            MenuItem(localization.Get("TrayIcon_ReportBugOption", "Report bug"), reportBug, icon: TrayMenuIcons.Bug20),
            MenuSeparator(),
            MenuItem(localization.Get("TrayIcon_QuitOption", "Quit {appName}")
                .Replace("{appName}", AppBranding.Name, StringComparison.Ordinal), exit, icon: TrayMenuIcons.ArrowExit20),
        ];
    }
}
