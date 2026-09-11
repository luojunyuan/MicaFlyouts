using MicaFlyouts.App;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using MicaFlyouts.UI.Toolkit;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static MicaFlyouts.UI.Toolkit.SettingsCardElement;
using static MicaFlyouts.UI.Toolkit.SettingsExpanderElement;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.Settings;

public sealed class SettingsWindowComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;

        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var localization = UseExternalStore(services.LocalizationStore.Subscribe, () => services.LocalizationStore.Snapshot);
        var (page, setPage) = UseState(SettingsPage.Home);
        var (query, setQuery) = UseState(string.Empty);
        var menu = SettingsSearchIndex.MenuItems;
        var content = Component<SettingsPageComponent, SettingsPageProps>(new(page, settings));
        var searchEntries = UseMemo(() => SettingsSearchIndex.Query(query), query);
        var searchSuggestions = UseMemo(
            () => searchEntries.Select(entry => entry.Title).ToArray(),
            query);
        var search = (AutoSuggestBox(
                query,
                setQuery,
                submitted => SelectSearchResult(submitted, setPage)) with
            {
                Suggestions = searchSuggestions,
                OnSuggestionChosen = selected =>
                {
                    setQuery(selected);
                    SelectSearchResult(selected, setPage);
                },
            })
            .Width(320)
            .PlaceholderText("Search settings")
            .QueryIcon(SymbolIcon("Find"))
            .AutomationName("Search settings");
        var titleBar = (TitleBar("Mica Flyouts") with
        {
            Content = search,
            Icon = AppBranding.TitleBarIcon,
        }).Grid(row: 0);
        var navigation = NavigationView(menu, content) with
        {
            SelectedTag = SettingsSearchIndex.Tag(page),
            OnSelectedTagChanged = tag =>
            {
                if (SettingsSearchIndex.TryParse(tag, out var selected))
                    setPage(selected);
            },
            IsSettingsVisible = false,
            PaneTitle = "Settings",
            IsPaneToggleButtonVisible = true,
            IsTitleBarAutoPaddingEnabled = false,
        };
        var surface = Border(Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Auto, GridSize.Star()],
                titleBar,
                navigation.Grid(row: 1)))
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8)
            .Set(navigationControl => navigationControl.FlowDirection = localization.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight);
        return LocaleProvider(
            localization.Language,
            surface,
            services.Localization.ResourceProvider);
    }

    private static void SelectSearchResult(
        string query,
        Action<SettingsPage> setPage)
    {
        var results = SettingsSearchIndex.Query(query);
        if (results.Count == 0)
            return;

        var result = results[0];
        setPage(result.Page);
    }
}

public enum SettingsPage
{
    Home,
    Media,
    Volume,
    Taskbar,
    Visualizer,
    NextUp,
    LockKeys,
    System,
    About,
    AppFiltering,
    Advanced,
}

public readonly record struct SettingsSearchEntry(SettingsPage Page, string Title, string[] Terms);

public static class SettingsSearchIndex
{
    private static readonly SettingsSearchEntry[] Entries =
    [
        new(SettingsPage.Home, "Home", ["home", "overview"]),
        new(SettingsPage.Media, "Media", ["media", "player", "playback", "seek"]),
        new(SettingsPage.Volume, "Volume", ["volume", "mixer", "mute", "audio"]),
        new(SettingsPage.Taskbar, "Taskbar", ["taskbar", "widget", "shell", "explorer"]),
        new(SettingsPage.Visualizer, "Visualizer", ["visualizer", "bars", "spectrum"]),
        new(SettingsPage.NextUp, "Next Up", ["next", "up", "queue"]),
        new(SettingsPage.LockKeys, "Lock Keys", ["caps", "num", "scroll", "insert", "keyboard"]),
        new(SettingsPage.System, "System", ["system", "startup", "tray", "language", "theme"]),
        new(SettingsPage.About, "About", ["about", "version", "license", "update"]),
        new(SettingsPage.AppFiltering, "App Filtering", ["filter", "allow", "block", "application"]),
        new(SettingsPage.Advanced, "Advanced", ["advanced", "json", "import", "export", "debug"]),
    ];

    public static NavigationViewItemData[] MenuItems { get; } =
    [
        NavItem("Home", "Home", Tag(SettingsPage.Home)),
        NavItemHeader("Features"),
        NavItem("Media", "MusicInfo", Tag(SettingsPage.Media)),
        NavItem("Volume", "Volume", Tag(SettingsPage.Volume)),
        NavItem("Taskbar", "DockBottom", Tag(SettingsPage.Taskbar)),
        NavItem("Visualizer", "ViewAll", Tag(SettingsPage.Visualizer)),
        NavItem("Next Up", "Forward", Tag(SettingsPage.NextUp)),
        NavItem("Lock Keys", "Keyboard", Tag(SettingsPage.LockKeys)),
        NavItemHeader("Application"),
        NavItem("System", "Setting", Tag(SettingsPage.System)),
        NavItem("About", "Help", Tag(SettingsPage.About)),
        NavItem("App Filtering", "Filter", Tag(SettingsPage.AppFiltering)),
        NavItem("Advanced", "DeveloperTools", Tag(SettingsPage.Advanced)),
    ];

    public static IReadOnlyList<SettingsSearchEntry> Query(string query)
        => string.IsNullOrWhiteSpace(query)
            ? Array.Empty<SettingsSearchEntry>()
            : [.. Entries.Where(entry => entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Terms.Any(term => term.Contains(query, StringComparison.OrdinalIgnoreCase)))];

    public static string Tag(SettingsPage page) => page.ToString();

    public static bool TryParse(string? tag, out SettingsPage page)
    {
        if (Enum.TryParse(tag, ignoreCase: true, out page) && Enum.IsDefined(page))
            return true;
        page = SettingsPage.Home;
        return false;
    }

}

public sealed record SettingsPageProps(SettingsPage Page, SettingsSnapshot Settings);

public sealed class SettingsPageComponent : Component<SettingsPageProps>
{
    private IntlAccessor? _intl;

    public override Element Render()
    {
        var services = AppRuntime.Services;
        _intl = UseIntl();
        return Props.Page switch
        {
            SettingsPage.Home => Home(services),
            SettingsPage.Media => Media(services),
            SettingsPage.Volume => Volume(services),
            SettingsPage.Taskbar => Taskbar(services),
            SettingsPage.Visualizer => Visualizer(services),
            SettingsPage.NextUp => NextUp(services),
            SettingsPage.LockKeys => LockKeys(services),
            SettingsPage.System => System(services),
            SettingsPage.About => About(services),
            SettingsPage.AppFiltering => AppFiltering(services),
            _ => Advanced(services),
        };
    }

    private ScrollViewerElement Home(AppServices services)
        => Page(T(Loc.App.HomeTitle, "Home"), "Choose which surfaces Mica Flyouts keeps ready.",
            Card("Media flyout", "Show playback controls for the active session.", Toggle(Props.Settings.MediaFlyoutEnabled, value => Update(services, s => s with { MediaFlyoutEnabled = value }))),
            Card("Volume controls", "Show the volume surface when hardware keys are pressed.", Toggle(Props.Settings.VolumeControlEnabled, value => Update(services, s => s with { VolumeControlEnabled = value }))),
            Card("Lock keys", "Show keyboard state after Caps, Num, Scroll, or Insert.", Toggle(Props.Settings.LockKeysEnabled, value => Update(services, s => s with { LockKeysEnabled = value }))));

    private ScrollViewerElement Media(AppServices services)
        => Page(T(Loc.App.MediaFlyoutTitle, "Media Flyout"), T(Loc.App.MediaFlyoutDescription, "Playback controls, layout, and timeline behavior."),
            Card("Enable media flyout", "Display the active player surface.", Toggle(Props.Settings.MediaFlyoutEnabled, value => Update(services, s => s with { MediaFlyoutEnabled = value }))),
            Card("Compact layout", "Use the compact media presentation.", Toggle(Props.Settings.CompactLayout, value => Update(services, s => s with { CompactLayout = value }))),
            Card("Player information", "Show title, artist, and player details.", Toggle(Props.Settings.PlayerInfoEnabled, value => Update(services, s => s with { PlayerInfoEnabled = value }))),
            Card("Seek bar", "Allow timeline seeking when the player supports it.", Toggle(Props.Settings.SeekbarEnabled, value => Update(services, s => s with { SeekbarEnabled = value }))),
            Card("Repeat and shuffle", "Keep repeat and shuffle actions in the flyout.", HStack(8,
                ToggleSwitch(Props.Settings.RepeatEnabled, value => Update(services, s => s with { RepeatEnabled = value }), "Repeat", "Repeat"),
                ToggleSwitch(Props.Settings.ShuffleEnabled, value => Update(services, s => s with { ShuffleEnabled = value }), "Shuffle", "Shuffle"))),
            Card("Always display", "Keep the media surface open while a session is active.", Toggle(Props.Settings.MediaFlyoutAlwaysDisplay, value => Update(services, s => s with { MediaFlyoutAlwaysDisplay = value }))),
            Card("Display duration", "Milliseconds before the surface hides.", Number(Props.Settings.Duration, value => Update(services, s => s with { Duration = value }))),
            Card("Position", "Choose one of the six screen positions.", Combo(["Bottom left", "Bottom center", "Bottom right", "Top left", "Top center", "Top right"], Props.Settings.Position, value => Update(services, s => s with { Position = value }))));

    private ScrollViewerElement Volume(AppServices services)
        => Page(T(Loc.App.VolumeFlyoutTitle, "Volume Flyout"), T(Loc.App.VolumeFlyoutDescription, "Master volume, mixer behavior, and native OSD handling."),
            Card("Volume surface", "Show a custom surface for hardware volume keys.", Toggle(Props.Settings.VolumeControlEnabled, value => Update(services, s => s with { VolumeControlEnabled = value }))),
            Card("Above media flyout", "Place volume controls above the media surface.", Toggle(Props.Settings.VolumeControlAboveMediaFlyout, value => Update(services, s => s with { VolumeControlAboveMediaFlyout = value }))),
            Card("Volume mixer", "List and control individual application sessions.", Toggle(Props.Settings.VolumeMixerEnabled, value => Update(services, s => s with { VolumeMixerEnabled = value }))),
            Card("Highlight active applications", "Emphasize sessions currently using the default device.", Toggle(Props.Settings.VolumeMixerHighlightActiveApps, value => Update(services, s => s with { VolumeMixerHighlightActiveApps = value }))),
            Card("Display duration", "Milliseconds before volume controls hide.", Number(Props.Settings.VolumeControlDuration, value => Update(services, s => s with { VolumeControlDuration = value }))));

    private ScrollViewerElement Taskbar(AppServices services)
        => Page(T(Loc.App.TaskbarWidgetCustomizationTitle, "Taskbar Widget"), T(Loc.App.TaskbarWidgetDescription, "Embed the widget in the Windows taskbar and recover after Explorer restarts."),
            Card("Taskbar widget", "Show title, artist, and cover art in the taskbar.", Toggle(Props.Settings.TaskbarWidgetEnabled, value => Update(services, s => s with { TaskbarWidgetEnabled = value }))),
            Card("Widget position", "Place the widget at the start, center, or end.", Combo(["Start", "Center", "End"], Props.Settings.TaskbarWidgetPosition, value => Update(services, s => s with { TaskbarWidgetPosition = value }))),
            Card("Automatic widget padding", "Reserve space around native Windows widgets.", Toggle(Props.Settings.TaskbarWidgetPadding, value => Update(services, s => s with { TaskbarWidgetPadding = value }))),
            Card("Manual padding", "Fine-tune the widget position in pixels.", Number(Props.Settings.TaskbarWidgetManualPadding, value => Update(services, s => s with { TaskbarWidgetManualPadding = value }))),
            Card("Fixed width", "Keep the widget width stable as titles change.", Toggle(Props.Settings.TaskbarWidgetFixedWidth, value => Update(services, s => s with { TaskbarWidgetFixedWidth = value }))),
            Card("Automatic hide", "Hide the widget after playback pauses.", Toggle(Props.Settings.TaskbarWidgetAutoHide, value => Update(services, s => s with { TaskbarWidgetAutoHide = value }))),
            Card("Pause overlay", "Show a pause marker over the cover art.", Toggle(Props.Settings.TaskbarWidgetShowPauseOverlay, value => Update(services, s => s with { TaskbarWidgetShowPauseOverlay = value }))));

    private ScrollViewerElement Visualizer(AppServices services)
        => Page(T(Loc.App.TaskbarVisualizerTitle, "Taskbar Visualizer"), T(Loc.App.TaskbarVisualizerDescription, "Real-time FFT bars from the default output device."),
            Card("Enable visualizer", "Listen to loopback audio and draw taskbar bars.", Toggle(Props.Settings.TaskbarVisualizerEnabled, value => Update(services, s => s with { TaskbarVisualizerEnabled = value }))),
            Card("Bar count", "Number of spectrum bars.", Number(Props.Settings.TaskbarVisualizerBarCount, value => Update(services, s => s with { TaskbarVisualizerBarCount = value }))),
            Card("Centered bars", "Grow bars symmetrically around the baseline.", Toggle(Props.Settings.TaskbarVisualizerCenteredBars, value => Update(services, s => s with { TaskbarVisualizerCenteredBars = value }))),
            Card("Baseline", "Display a zero-level baseline when the signal is quiet.", Toggle(Props.Settings.TaskbarVisualizerBaseline, value => Update(services, s => s with { TaskbarVisualizerBaseline = value }))),
            Card("Baseline auto-hide", "Hide the baseline when no signal is present.", Toggle(Props.Settings.TaskbarVisualizerBaselineAutoHide, value => Update(services, s => s with { TaskbarVisualizerBaselineAutoHide = value }))),
            Card("Sensitivity", "Calibrate when bars begin moving.", Combo(["Low", "Medium", "High"], Props.Settings.TaskbarVisualizerAudioSensitivity - 1, value => Update(services, s => s with { TaskbarVisualizerAudioSensitivity = value + 1 }))));

    private ScrollViewerElement NextUp(AppServices services)
        => Page(T(Loc.App.NextUpCustomizationTitle, "Next Up"), T(Loc.App.NextUpDescription, "Show the next media change without duplicate titles."),
            Card("Enable Next Up", "Display a short transition surface when a track changes.", Toggle(Props.Settings.NextUpEnabled, value => Update(services, s => s with { NextUpEnabled = value }))),
            Card("Display duration", "Milliseconds before Next Up hides.", Number(Props.Settings.NextUpDuration, value => Update(services, s => s with { NextUpDuration = value }))));

    private ScrollViewerElement LockKeys(AppServices services)
        => Page(T(Loc.App.LockKeysCustomizationTitle, "Lock Keys"), T(Loc.App.LockKeysDescription, "Keyboard indicators for Caps, Num, Scroll, and Insert."),
            Card("Enable lock key surface", "Listen for low-level keyboard changes.", Toggle(Props.Settings.LockKeysEnabled, value => Update(services, s => s with { LockKeysEnabled = value }))),
            Card("Caps Lock", "Show the Caps Lock state.", Toggle(Props.Settings.LockKeysCapsEnabled, value => Update(services, s => s with { LockKeysCapsEnabled = value }))),
            Card("Num Lock", "Show the Num Lock state.", Toggle(Props.Settings.LockKeysNumEnabled, value => Update(services, s => s with { LockKeysNumEnabled = value }))),
            Card("Scroll Lock", "Show the Scroll Lock state.", Toggle(Props.Settings.LockKeysScrollEnabled, value => Update(services, s => s with { LockKeysScrollEnabled = value }))),
            Card("Insert", "Show a surface after Insert is pressed.", Toggle(Props.Settings.LockKeysInsertEnabled, value => Update(services, s => s with { LockKeysInsertEnabled = value }))),
            Card("Display duration", "Milliseconds before the indicator hides.", Number(Props.Settings.LockKeysDuration, value => Update(services, s => s with { LockKeysDuration = value }))),
            Card("Animated indicators", "Use the original bounce and shackle motion.", Toggle(Props.Settings.LockKeysAnimated, value => Update(services, s => s with { LockKeysAnimated = value }))));

    private ScrollViewerElement System(AppServices services)
        => Page(T(Loc.App.SystemSettingsTitle, "System"), "Startup, appearance, localization, and tray behavior.",
            Card("Start with Windows", "Launch minimized to the notification area.", Toggle(Props.Settings.Startup, value => Update(services, s => s with { Startup = value }))),
            Card("Hide tray icon", "Run without a persistent tray icon.", Toggle(Props.Settings.NIconHide, value => Update(services, s => s with { NIconHide = value }))),
            Card("Theme", "Use the system, light, or dark theme.", Combo(["System", "Light", "Dark"], Props.Settings.AppTheme, value => Update(services, s => s with { AppTheme = value }))),
            Card("Language", "Choose the UI language; system follows Windows.", Combo([.. LocalizationCatalog.SupportedLanguages],
                Math.Max(0, Array.IndexOf([.. LocalizationCatalog.SupportedLanguages], Props.Settings.AppLanguage)),
                value => Update(services, s => s with { AppLanguage = LocalizationCatalog.SupportedLanguages[value] }))),
            Card("Font family", "Font fallback list used by the application.",
                TextBox(Props.Settings.FontFamily, value => Update(services, s => s with { FontFamily = value }))
                    .AutomationName("Font family")));

    private ScrollViewerElement About(AppServices services)
        => Page(T(Loc.App.AboutTitle, "About"), "Open-source project information and update status.",
            Card("Mica Flyouts", "A native Microsoft UI Reactor media overlay.", VStack(8,
                TextBlock("Version: " + (string.IsNullOrWhiteSpace(Props.Settings.LastKnownVersion) ? "development" : Props.Settings.LastKnownVersion)),
                HyperlinkButton("Project repository", new Uri("https://github.com/unchihugo/FluentFlyout")),
                Button("Check for updates", () => _ = services.Updater.CheckAsync(Props.Settings.LastKnownVersion)))));

    private ScrollViewerElement AppFiltering(AppServices services)
        => Page(T(Loc.App.AppFilteringTitle, "App Filtering"), T(Loc.App.AppFilteringRulesDescription, "Allow or block sessions by display name or session ID."),
            Card("Enable filtering", "Apply the selected app list to media and taskbar updates.", Toggle(Props.Settings.AppFilteringEnabled, value => Update(services, s => s with { AppFilteringEnabled = value }))),
            Card("Mode", "Blacklist blocks matching entries; whitelist allows only matches.", Combo(["Blacklist", "Whitelist"], Props.Settings.AppFilteringMode, value => Update(services, s => s with { AppFilteringMode = value }))),
            Card("Allowed applications", "One display name or session ID fragment per line.",
                TextBox(string.Join(Environment.NewLine, Props.Settings.AllowedApps), value => Update(services, s => s with { AllowedApps = Entries(value) }))
                    .AutomationName("Allowed applications")),
            Card("Blocked applications", "One display name or session ID fragment per line.",
                TextBox(string.Join(Environment.NewLine, Props.Settings.BlockedApps), value => Update(services, s => s with { BlockedApps = Entries(value) }))
                    .AutomationName("Blocked applications")));

    private ScrollViewerElement Advanced(AppServices services)
        => Page(T(Loc.App.AdvancedSettingsTitle, "Advanced Settings"), "Settings export, import, and compatibility controls.",
            Card("Legacy taskbar calculation", "Use the compatibility width calculation for other taskbar modifications.", Toggle(Props.Settings.LegacyTaskbarWidthEnabled, value => Update(services, s => s with { LegacyTaskbarWidthEnabled = value }))),
            Card("Anonymous diagnostics", "Allow anonymous operational diagnostics.", Toggle(Props.Settings.AnonymousTelemetryAllowed, value => Update(services, s => s with { AnonymousTelemetryAllowed = value }))),
            Card("Settings data", "Export excludes the installation identifier; import keeps the current identifier.", HStack(8,
                Button("Export", () =>
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(services.Settings.Export());
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                }),
                Button("Save now", services.Settings.SaveNow))));

    private static ScrollViewerElement Page(string title, string description, params Element[] cards)
        => ScrollViewer(VStack(16,
                Heading(title),
                Caption(description).Opacity(0.7),
                VStack(12, cards)))
            .Padding(24);

    private static BorderElement Card(string header, string description, Element content)
        => Border(Component<SettingRow, SettingRowProps>(new(
                header,
                description,
                content,
                Icon("Setting"))))
            .Padding(14)
            .Background(CardBackground)
            .WithBorder(CardStroke, 1)
            .CornerRadius(6);

    private static ToggleSwitchElement Toggle(bool value, Action<bool> setter)
        => ToggleSwitch(Optional<bool>.Of(value), setter).AutomationName("Toggle setting");

    private static NumberBoxElement Number(int value, Action<int> setter)
        => NumberBox(Optional<double>.Of(value), number => setter((int)Math.Round(number)))
            .AutomationName("Numeric setting");

    private static ComboBoxElement Combo(string[] items, int selected, Action<int> setter)
        => ComboBox(items, Optional<int>.Of(Math.Clamp(selected, 0, Math.Max(0, items.Length - 1))), setter)
            .AutomationName("Choose setting");

    private static void Update(AppServices services, Func<SettingsSnapshot, SettingsSnapshot> reducer)
        => services.Settings.Update(reducer);

    private static string[] Entries(string value)
        => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private string T(MessageKey key, string fallback)
    {
        var value = _intl?.Message(key);
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("[??", StringComparison.Ordinal)
            ? fallback
            : value;
    }
}
