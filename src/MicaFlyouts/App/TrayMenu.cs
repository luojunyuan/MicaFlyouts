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
            TrayItem(localization.Get("TrayIcon_SettingsOption", "Settings"), openSettings, TrayMenuIcons.Settings20),
            MenuSeparator(),
            TrayItem(localization.Get("TrayIcon_GitHubRepositoryOption", "Repository"), openRepository, TrayMenuIcons.DocumentChevronDouble20),
            TrayItem(localization.Get("TrayIcon_ViewLogsOption", "View logs"), openLogs, TrayMenuIcons.FolderOpen20),
            TrayItem(localization.Get("TrayIcon_ReportBugOption", "Report bug"), reportBug, TrayMenuIcons.Bug20),
            MenuSeparator(),
            TrayItem(localization.Get("TrayIcon_QuitOption", "Quit {appName}")
                .Replace("{appName}", AppBranding.Name, StringComparison.Ordinal), exit, TrayMenuIcons.ArrowExit20),
        ];
    }

    private static MenuFlyoutItemData TrayItem(string text, Action onClick, string icon)
    {
        var item = MenuItem(text, onClick, icon);
        return icon.StartsWith("path:", StringComparison.Ordinal)
            ? item with { IconElement = PathIcon(icon[5..]) }
            : item;
    }
}
