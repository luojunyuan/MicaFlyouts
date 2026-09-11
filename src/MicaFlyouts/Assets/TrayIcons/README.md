# Tray icon sources

## Notification-area icons

The original FluentFlyout checkout is pinned at
`39c9f1636638ecc16f932d4b4eb57f4a6d89d90b`.

- `FluentFlyoutWPF/Resources/FluentFlyout2.ico` is byte-identical to our
  `Assets/MicaFlyouts.ico`, which remains the default colored icon.
- `FluentFlyoutWPF/Resources/TrayIcons/FluentFlyoutBlack.png` and
  `FluentFlyoutWhite.png` are the original 24-by-24 transparent monochrome icons.
  Our matching `.ico` files wrap those unchanged PNG bytes in a single-frame ICO
  header so H.NotifyIcon can consume them as `System.Drawing.Icon` resources.
- Original selection: `FluentFlyoutWPF/Classes/ThemeManager.cs`,
  `UpdateTrayIcon()`. `NIconSymbol=false` selects the colored icon; otherwise the
  **Windows taskbar** theme selects black for light and white for dark. This is
  independent of the application's theme preference.
- Upstream license: `FluentFlyout.LICENSE` (GPL-3.0).

## Context-menu icons

The original `FluentFlyoutWPF/MainWindow.xaml` uses WPF-UI's regular 20px symbols:

| Action | Original symbol | Our vector constant |
| --- | --- | --- |
| Settings | `Settings20` | `TrayMenuIcons.Settings20` |
| Repository | `DocumentChevronDouble20` | `TrayMenuIcons.DocumentChevronDouble20` |
| View logs | `FolderOpen20` | `TrayMenuIcons.FolderOpen20` |
| Report bug | `Bug20` | `TrayMenuIcons.Bug20` |
| Quit | `ArrowExit20` | `TrayMenuIcons.ArrowExit20` |

The path data in `Features/Tray/TrayMenuIcons.cs` comes from Microsoft's
`fluentui-system-icons` repository, commit
`74727164b4a18933e5533f84109d3f36c1355422`, under
`assets/<Icon Name>/SVG/ic_fluent_<icon_name>_20_regular.svg`.
`HNotifyMenu` converts the `path:` data to native WinUI `PathIcon` elements —
rendered by the dependency library's `SecondWindow` menu, which retains SVG's
nonzero fill rule (`F1`) and inherits the menu foreground color. The menu
currently uses the native `PopupMenu` mode (see below), which renders text-only
items, so the icon data is kept in the menu DSL but not drawn. No WPF or
icon-font dependency is required. See `FluentSystemIcons.LICENSE` (MIT).

## Localization and live updates

`TrayMenu.Create` resolves the five existing `TrayIcon_*Option` keys through
`LocalizationService`. All supported locale files retain their original
translations; the quit label substitutes `{appName}` with `Mica Flyouts`.
The tooltip is the product name, not a translated sentence.

`TrayIconFeature` owns a hidden host window (`ActivateOnOpen = false`) whose root
component is `TrayIconComponent`. The component subscribes to `SettingsStore`
and `LocalizationStore` through Reactor's `UseExternalStore` and owns the icon
via `HNotifyComponent.UseTrayIcon`. A changed locale produces a new
`HNotifyMenu` and spec, so the existing tray handle picks up its menu.
`NIconHide` closes the host window (and reopens it on demand), and `NIconSymbol`
selects the colored or monochrome icon — the monochrome resource reads
`SystemUsesLightTheme` from the registry when the handle is opened, so symbol
icons match the taskbar theme at open time. The menu uses
`HNotifyContextMenuMode.PopupMenu` (the mode the dependency library's component
sample uses): the native menu is drawn above the Shell's XAML popups, so the
tray tooltip cannot swallow clicks on the bottom item, and icon / presenter
styling are not rendered in this mode. All subscriptions and the tray handle
are released during shutdown; the quit command releases the tray host first
(which takes the native menu off the stack) and then calls `ReactorApp.Exit()`
directly.
