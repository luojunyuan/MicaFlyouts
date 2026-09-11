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

The path data in `App/TrayMenuIcons.cs` comes from Microsoft's
`fluentui-system-icons` repository, commit
`74727164b4a18933e5533f84109d3f36c1355422`, under
`assets/<Icon Name>/SVG/ic_fluent_<icon_name>_20_regular.svg`.
`HNotifyIconTray` renders the `path:` data with native WinUI `PathIcon`, retaining
SVG's nonzero fill rule (`F1`) and inheriting the menu foreground color. No WPF
or icon-font dependency is required. See `FluentSystemIcons.LICENSE` (MIT).

## Localization and live updates

`TrayMenu.Create` resolves the five existing `TrayIcon_*Option` keys through
`LocalizationService`. All supported locale files retain their original
translations; the quit label substitutes `{appName}` with `Mica Flyouts`.
The tooltip is the product name, not a translated sentence.

`AppServices` subscribes to `LocalizationStore`. On a language/resources/font
change it safely disposes and recreates the native tray surface on the UI thread;
this avoids leaving H.NotifyIcon's `SecondWindow` menu clone with stale item
references. Menu presenter style carries the locale's flow direction and font
fallback. This does not happen for a Windows theme-only icon refresh.
`UISettings.ColorValuesChanged` refreshes symbol icons, and settings updates apply
the `NIconSymbol`/`NIconHide` preferences immediately. All subscriptions and the
tray handle are released during shutdown.
