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
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.Features.Settings;

public sealed class SettingsWindowComponent : LocalizedWindowComponent
{
    public override Element Render()
    {
        var (page, setPage) = UseState(SettingsPage.Home);
        return UseLocalized(Component<SettingsContentComponent, SettingsContentProps>(new(page, setPage)));
    }
}

public sealed record SettingsContentProps(SettingsPage Page, Action<SettingsPage> SetPage);

internal sealed class SettingsContentComponent : Component<SettingsContentProps>
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var (query, setQuery) = UseState(string.Empty);
        var page = Props.Page;
        var setPage = Props.SetPage;
        var menu = SettingsSearchIndex.MenuItems(t);
        var content = Component<SettingsPageComponent, SettingsPageProps>(new(page, settings));
        var searchEntries = UseMemo(() => SettingsSearchIndex.Query(query, t), query, t.Locale);
        var searchSuggestions = searchEntries
            .Select(entry => t.Message(entry.TitleKey))
            .ToArray();

        return RenderSettingsFrame(
            t,
            RenderTitleBar(query, setQuery, searchSuggestions, t, setPage),
            RenderNavigation(menu, content, page, setPage, t));
    }

    private static TitleBarElement RenderTitleBar(
        string query,
        Action<string> setQuery,
        string[] searchSuggestions,
        IntlAccessor t,
        Action<SettingsPage> setPage)
    {
        var search = (AutoSuggestBox(
                query,
                setQuery,
                submitted => SelectSearchResult(submitted, t, setPage)) with
        {
            Suggestions = searchSuggestions,
            OnSuggestionChosen = selected =>
            {
                setQuery(selected);
                SelectSearchResult(selected, t, setPage);
            },
        })
            .Width(320)
            .PlaceholderText(t.Message(Loc.App.SearchSettings))
            .QueryIcon(SymbolIcon("Find"))
            .AutomationName(t.Message(Loc.App.SearchSettings));

        return (TitleBar(AppBranding.Name) with
        {
            Content = search,
            Icon = AppBranding.TitleBarIcon,
        }).Grid(row: 0);
    }

    private static NavigationViewElement RenderNavigation(
        NavigationViewItemData[] menu,
        Element content,
        SettingsPage page,
        Action<SettingsPage> setPage,
        IntlAccessor t)
    {
        var navigation = NavigationView(menu, content) with
        {
            SelectedTag = SettingsSearchIndex.Tag(page),
            OnSelectedTagChanged = tag =>
            {
                if (SettingsSearchIndex.TryParse(tag, out var selected))
                    setPage(selected);
            },
            IsSettingsVisible = false,
            PaneTitle = t.Message(Loc.App.SettingsTitle),
            IsPaneToggleButtonVisible = true,
            IsTitleBarAutoPaddingEnabled = false,
        };

        return navigation.Grid(row: 1);
    }

    private static BorderElement RenderSettingsFrame(IntlAccessor t, Element titleBar, Element navigation)
        => Border(Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Auto, GridSize.Star()],
                titleBar,
                navigation))
            .Background(SolidBackground)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8)
            .Set(navigationControl => navigationControl.FlowDirection = t.Direction);

    private static void SelectSearchResult(
        string query,
        IntlAccessor t,
        Action<SettingsPage> setPage)
    {
        var results = SettingsSearchIndex.Query(query, t);
        if (results.Count > 0)
            setPage(results[0].Page);
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

public readonly record struct SettingsSearchEntry(
    SettingsPage Page,
    string Title,
    MessageKey TitleKey,
    string[] Terms);

public static class SettingsSearchIndex
{
    private static readonly SettingsSearchEntry[] Entries =
    [
        new(SettingsPage.Home, "Home", Loc.App.HomeTitle, ["home", "overview"]),
        new(SettingsPage.Media, "Media", Loc.App.MediaFlyoutTitle, ["media", "player", "playback", "seek"]),
        new(SettingsPage.Volume, "Volume", Loc.App.VolumeFlyoutTitle, ["volume", "mixer", "mute", "audio"]),
        new(SettingsPage.Taskbar, "Taskbar", Loc.App.TaskbarWidgetCustomizationTitle, ["taskbar", "widget", "shell", "explorer"]),
        new(SettingsPage.Visualizer, "Visualizer", Loc.App.TaskbarVisualizerTitle, ["visualizer", "bars", "spectrum"]),
        new(SettingsPage.NextUp, "Next Up", Loc.App.NextUpCustomizationTitle, ["next", "up", "queue"]),
        new(SettingsPage.LockKeys, "Lock Keys", Loc.App.LockKeysCustomizationTitle, ["caps", "num", "scroll", "insert", "keyboard"]),
        new(SettingsPage.System, "System", Loc.App.SystemSettingsTitle, ["system", "startup", "tray", "language", "theme"]),
        new(SettingsPage.About, "About", Loc.App.AboutTitle, ["about", "version", "license", "update"]),
        new(SettingsPage.AppFiltering, "App Filtering", Loc.App.AppFilteringTitle, ["filter", "allow", "block", "application"]),
        new(SettingsPage.Advanced, "Advanced", Loc.App.AdvancedSettingsTitle, ["advanced", "json", "import", "export", "debug"]),
    ];

    public static NavigationViewItemData[] MenuItems(IntlAccessor t) =>
    [
        NavItem(t.Message(Loc.App.HomeTitle), "Home", Tag(SettingsPage.Home)),
        NavItemHeader(t.Message(Loc.App.FeaturesSectionTitle)),
        NavItem(t.Message(Loc.App.MediaFlyoutTitle), "MusicInfo", Tag(SettingsPage.Media)),
        NavItem(t.Message(Loc.App.VolumeFlyoutTitle), "Volume", Tag(SettingsPage.Volume)),
        NavItem(t.Message(Loc.App.TaskbarWidgetCustomizationTitle), "DockBottom", Tag(SettingsPage.Taskbar)),
        NavItem(t.Message(Loc.App.TaskbarVisualizerTitle), "ViewAll", Tag(SettingsPage.Visualizer)),
        NavItem(t.Message(Loc.App.NextUpCustomizationTitle), "Forward", Tag(SettingsPage.NextUp)),
        NavItem(t.Message(Loc.App.LockKeysCustomizationTitle), "Keyboard", Tag(SettingsPage.LockKeys)),
        NavItemHeader(t.Message(Loc.App.ApplicationSectionTitle)),
        NavItem(t.Message(Loc.App.SystemSettingsTitle), "Setting", Tag(SettingsPage.System)),
        NavItem(t.Message(Loc.App.AboutTitle), "Help", Tag(SettingsPage.About)),
        NavItem(t.Message(Loc.App.AppFilteringTitle), "Filter", Tag(SettingsPage.AppFiltering)),
        NavItem(t.Message(Loc.App.AdvancedSettingsTitle), "DeveloperTools", Tag(SettingsPage.Advanced)),
    ];

    public static IReadOnlyList<SettingsSearchEntry> Query(string query, IntlAccessor? t = null)
        => string.IsNullOrWhiteSpace(query)
            ? Array.Empty<SettingsSearchEntry>()
            : [.. Entries.Where(entry =>
                entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (t is not null
                    && t.Message(entry.TitleKey).Contains(query, StringComparison.OrdinalIgnoreCase))
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
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        return Props.Page switch
        {
            SettingsPage.Home => Home(services, t),
            SettingsPage.Media => Media(services, t),
            SettingsPage.Volume => Volume(services, t),
            SettingsPage.Taskbar => Taskbar(services, t),
            SettingsPage.Visualizer => Visualizer(services, t),
            SettingsPage.NextUp => NextUp(services, t),
            SettingsPage.LockKeys => LockKeys(services, t),
            SettingsPage.System => System(services, t),
            SettingsPage.About => About(services, t),
            SettingsPage.AppFiltering => AppFiltering(services, t),
            _ => Advanced(services, t),
        };
    }

    private ScrollViewerElement Home(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.HomeTitle), t.Message(Loc.App.HomeDescription),
            Card(t.Message(Loc.App.TextBlockText1), t.Message(Loc.App.TextBlockText3),
                Toggle(t, Props.Settings.MediaFlyoutEnabled, value => Update(services, s => s with { MediaFlyoutEnabled = value }))),
            Card(t.Message(Loc.App.EnableVolumeFlyoutTitle), t.Message(Loc.App.EnableVolumeFlyoutDescription),
                Toggle(t, Props.Settings.VolumeControlEnabled, value => Update(services, s => s with { VolumeControlEnabled = value }))),
            Card(t.Message(Loc.App.EnableLockKeysTitle), t.Message(Loc.App.EnableLockKeysDescription),
                Toggle(t, Props.Settings.LockKeysEnabled, value => Update(services, s => s with { LockKeysEnabled = value }))));

    private ScrollViewerElement Media(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.MediaFlyoutTitle), t.Message(Loc.App.MediaFlyoutDescription),
            Card(t.Message(Loc.App.TextBlockText1), t.Message(Loc.App.TextBlockText3),
                Toggle(t, Props.Settings.MediaFlyoutEnabled, value => Update(services, s => s with { MediaFlyoutEnabled = value }))),
            Card(t.Message(Loc.App.CompactLayoutTitle), t.Message(Loc.App.CompactLayoutDescription),
                Toggle(t, Props.Settings.CompactLayout, value => Update(services, s => s with { CompactLayout = value }))),
            Card(t.Message(Loc.App.ShowMediaPlayerNameTitle), t.Message(Loc.App.PlayerInformationDescription),
                Toggle(t, Props.Settings.PlayerInfoEnabled, value => Update(services, s => s with { PlayerInfoEnabled = value }))),
            Card(t.Message(Loc.App.ShowSeekbarTitle), t.Message(Loc.App.ShowSeekbarDescription),
                Toggle(t, Props.Settings.SeekbarEnabled, value => Update(services, s => s with { SeekbarEnabled = value }))),
            Card(t.Message(Loc.App.RepeatButtonTitle), t.Message(Loc.App.RepeatButtonDescription), HStack(8,
                ToggleSwitch(Optional<bool>.Of(Props.Settings.RepeatEnabled),
                    value => Update(services, s => s with { RepeatEnabled = value }),
                    t.Message(Loc.App.MediaRepeat),
                    t.Message(Loc.App.MediaRepeat)),
                ToggleSwitch(Optional<bool>.Of(Props.Settings.ShuffleEnabled),
                    value => Update(services, s => s with { ShuffleEnabled = value }),
                    t.Message(Loc.App.MediaShuffle),
                    t.Message(Loc.App.MediaShuffle)))),
            Card(t.Message(Loc.App.MediaFlyoutAlwaysDisplay), t.Message(Loc.App.MediaFlyoutAlwaysDisplayDescription),
                Toggle(t, Props.Settings.MediaFlyoutAlwaysDisplay, value => Update(services, s => s with { MediaFlyoutAlwaysDisplay = value }))),
            Card(t.Message(Loc.App.FlyoutStayDurationTitle), t.Message(Loc.App.FlyoutStayDurationDescription),
                Number(t, Props.Settings.Duration, value => Update(services, s => s with { Duration = value }))),
            Card(t.Message(Loc.App.FlyoutPositionTitle), t.Message(Loc.App.FlyoutPositionTitle),
                Combo(t,
                    [
                        t.Message(Loc.App.PositionBottomLeft),
                        t.Message(Loc.App.PositionBottomCenter),
                        t.Message(Loc.App.PositionBottomRight),
                        t.Message(Loc.App.PositionTopLeft),
                        t.Message(Loc.App.PositionTopCenter),
                        t.Message(Loc.App.PositionTopRight),
                    ],
                    Props.Settings.Position,
                    value => Update(services, s => s with { Position = value }))));

    private ScrollViewerElement Volume(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.VolumeFlyoutTitle), t.Message(Loc.App.VolumeFlyoutDescription),
            Card(t.Message(Loc.App.EnableVolumeFlyoutTitle), t.Message(Loc.App.EnableVolumeFlyoutDescription),
                Toggle(t, Props.Settings.VolumeControlEnabled, value => Update(services, s => s with { VolumeControlEnabled = value }))),
            Card(t.Message(Loc.App.VolumeAboveMediaFlyoutTitle), t.Message(Loc.App.VolumeAboveMediaFlyoutDescription),
                Toggle(t, Props.Settings.VolumeControlAboveMediaFlyout, value => Update(services, s => s with { VolumeControlAboveMediaFlyout = value }))),
            Card(t.Message(Loc.App.EnableVolumeMixerTitle), t.Message(Loc.App.EnableVolumeMixerDescription),
                Toggle(t, Props.Settings.VolumeMixerEnabled, value => Update(services, s => s with { VolumeMixerEnabled = value }))),
            Card(t.Message(Loc.App.VolumeMixerHighlightTitle), t.Message(Loc.App.VolumeMixerHighlightDescription),
                Toggle(t, Props.Settings.VolumeMixerHighlightActiveApps, value => Update(services, s => s with { VolumeMixerHighlightActiveApps = value }))),
            Card(t.Message(Loc.App.VolumeFlyoutStayDurationTitle), t.Message(Loc.App.VolumeFlyoutStayDurationDescription),
                Number(t, Props.Settings.VolumeControlDuration, value => Update(services, s => s with { VolumeControlDuration = value }))));

    private ScrollViewerElement Taskbar(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.TaskbarWidgetCustomizationTitle), t.Message(Loc.App.TaskbarWidgetDescription),
            Card(t.Message(Loc.App.TaskbarWidgetEnabledTitle), t.Message(Loc.App.TaskbarWidgetEnabledDescription),
                Toggle(t, Props.Settings.TaskbarWidgetEnabled, value => Update(services, s => s with { TaskbarWidgetEnabled = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetPosition), t.Message(Loc.App.WidgetPositionDescription),
                Combo(t,
                    [t.Message(Loc.App.PositionStart), t.Message(Loc.App.PositionCenter), t.Message(Loc.App.PositionEnd)],
                    Props.Settings.TaskbarWidgetPosition,
                    value => Update(services, s => s with { TaskbarWidgetPosition = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetPaddingTitle), t.Message(Loc.App.TaskbarWidgetPaddingDescription),
                Toggle(t, Props.Settings.TaskbarWidgetPadding, value => Update(services, s => s with { TaskbarWidgetPadding = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetManualPaddingTitle), t.Message(Loc.App.TaskbarWidgetManualPaddingDescription),
                Number(t, Props.Settings.TaskbarWidgetManualPadding, value => Update(services, s => s with { TaskbarWidgetManualPadding = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetFixedWidthTitle), t.Message(Loc.App.TaskbarWidgetFixedWidthDescription),
                Toggle(t, Props.Settings.TaskbarWidgetFixedWidth, value => Update(services, s => s with { TaskbarWidgetFixedWidth = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetAutoHideTitle), t.Message(Loc.App.TaskbarWidgetAutoHideDescription),
                Toggle(t, Props.Settings.TaskbarWidgetAutoHide, value => Update(services, s => s with { TaskbarWidgetAutoHide = value }))),
            Card(t.Message(Loc.App.TaskbarWidgetShowPauseOverlayTitle), t.Message(Loc.App.TaskbarWidgetShowPauseOverlayDescription),
                Toggle(t, Props.Settings.TaskbarWidgetShowPauseOverlay, value => Update(services, s => s with { TaskbarWidgetShowPauseOverlay = value }))));

    private ScrollViewerElement Visualizer(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.TaskbarVisualizerTitle), t.Message(Loc.App.TaskbarVisualizerDescription),
            Card(t.Message(Loc.App.TaskbarVisualizerEnabledTitle), t.Message(Loc.App.TaskbarVisualizerEnabledDescription),
                Toggle(t, Props.Settings.TaskbarVisualizerEnabled, value => Update(services, s => s with { TaskbarVisualizerEnabled = value }))),
            Card(t.Message(Loc.App.TaskbarVisualizerBarCountTitle), t.Message(Loc.App.TaskbarVisualizerBarCountDescription),
                Number(t, Props.Settings.TaskbarVisualizerBarCount, value => Update(services, s => s with { TaskbarVisualizerBarCount = value }))),
            Card(t.Message(Loc.App.TaskbarVisualizerCenteredBarsTitle), t.Message(Loc.App.TaskbarVisualizerCenteredBarsDescription),
                Toggle(t, Props.Settings.TaskbarVisualizerCenteredBars, value => Update(services, s => s with { TaskbarVisualizerCenteredBars = value }))),
            Card(t.Message(Loc.App.TaskbarVisualizerBaselineTitle), t.Message(Loc.App.TaskbarVisualizerBaselineDescription),
                Toggle(t, Props.Settings.TaskbarVisualizerBaseline, value => Update(services, s => s with { TaskbarVisualizerBaseline = value }))),
            Card(t.Message(Loc.App.BaselineAutoHideTitle), t.Message(Loc.App.TaskbarVisualizerBaselineAutoHideDescription),
                Toggle(t, Props.Settings.TaskbarVisualizerBaselineAutoHide, value => Update(services, s => s with { TaskbarVisualizerBaselineAutoHide = value }))),
            Card(t.Message(Loc.App.TaskbarVisualizerAudioSensitivityTitle), t.Message(Loc.App.TaskbarVisualizerAudioSensitivityDescription),
                Combo(t,
                    [t.Message(Loc.App.SensitivityLow), t.Message(Loc.App.SensitivityMedium), t.Message(Loc.App.SensitivityHigh)],
                    Props.Settings.TaskbarVisualizerAudioSensitivity - 1,
                    value => Update(services, s => s with { TaskbarVisualizerAudioSensitivity = value + 1 }))));

    private ScrollViewerElement NextUp(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.NextUpCustomizationTitle), t.Message(Loc.App.NextUpDescription),
            Card(t.Message(Loc.App.EnableNextUpTitle), t.Message(Loc.App.EnableNextUpDescription),
                Toggle(t, Props.Settings.NextUpEnabled, value => Update(services, s => s with { NextUpEnabled = value }))),
            Card(t.Message(Loc.App.NextUpStayDurationTitle), t.Message(Loc.App.NextUpStayDurationDescription),
                Number(t, Props.Settings.NextUpDuration, value => Update(services, s => s with { NextUpDuration = value }))));

    private ScrollViewerElement LockKeys(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.LockKeysCustomizationTitle), t.Message(Loc.App.LockKeysDescription),
            Card(t.Message(Loc.App.EnableLockKeysTitle), t.Message(Loc.App.EnableLockKeysDescription),
                Toggle(t, Props.Settings.LockKeysEnabled, value => Update(services, s => s with { LockKeysEnabled = value }))),
            Card(t.Message(Loc.App.EnableCapsTitle), t.Message(Loc.App.EnableCapsDescription),
                Toggle(t, Props.Settings.LockKeysCapsEnabled, value => Update(services, s => s with { LockKeysCapsEnabled = value }))),
            Card(t.Message(Loc.App.EnableNumTitle), t.Message(Loc.App.EnableNumDescription),
                Toggle(t, Props.Settings.LockKeysNumEnabled, value => Update(services, s => s with { LockKeysNumEnabled = value }))),
            Card(t.Message(Loc.App.EnableScrollTitle), t.Message(Loc.App.EnableScrollDescription),
                Toggle(t, Props.Settings.LockKeysScrollEnabled, value => Update(services, s => s with { LockKeysScrollEnabled = value }))),
            Card(t.Message(Loc.App.EnableInsertKeyTitle), t.Message(Loc.App.EnableInsertKeyDescription),
                Toggle(t, Props.Settings.LockKeysInsertEnabled, value => Update(services, s => s with { LockKeysInsertEnabled = value }))),
            Card(t.Message(Loc.App.LockKeysStayDurationTitle), t.Message(Loc.App.LockKeysStayDurationDescription),
                Number(t, Props.Settings.LockKeysDuration, value => Update(services, s => s with { LockKeysDuration = value }))),
            Card(t.Message(Loc.App.LockKeysAnimatedTitle), t.Message(Loc.App.LockKeysAnimatedDescription),
                Toggle(t, Props.Settings.LockKeysAnimated, value => Update(services, s => s with { LockKeysAnimated = value }))));

    private ScrollViewerElement System(AppServices services, IntlAccessor t)
    {
        var languageOptions = LocalizationCatalog.CreateLanguageOptions(
            t.Message(Loc.App.SystemSettingsTitle));
        return Page(t.Message(Loc.App.SystemSettingsTitle), t.Message(Loc.App.SystemDescription),
                Card(t.Message(Loc.App.LaunchOnStartupTitle), t.Message(Loc.App.LaunchOnStartupDescription),
                    Toggle(t, Props.Settings.Startup, value => Update(services, s => s with { Startup = value }))),
                Card(t.Message(Loc.App.HideTrayIconTitle), t.Message(Loc.App.HideTrayIconDescription),
                    Toggle(t, Props.Settings.NIconHide, value => Update(services, s => s with { NIconHide = value }))),
                Card(t.Message(Loc.App.Win11TrayIconTitle), t.Message(Loc.App.Win11TrayIconDescription),
                    Toggle(t, Props.Settings.NIconSymbol, value => Update(services, s => s with { NIconSymbol = value }))),
                Card(t.Message(Loc.App.AppThemeTitle), t.Message(Loc.App.AppThemeDescription),
                    Combo(t,
                        [t.Message(Loc.App.AppThemeDefault), t.Message(Loc.App.AppThemeLight), t.Message(Loc.App.AppThemeDark)],
                        Props.Settings.AppTheme,
                        value => Update(services, s => s with { AppTheme = value }))),
                Card(t.Message(Loc.App.AppLanguageTitle), t.Message(Loc.App.AppLanguageDescription),
                    Combo(t,
                        [.. languageOptions.Select(option => option.DisplayName)],
                        LocalizationCatalog.IndexOfLanguage(languageOptions, Props.Settings.AppLanguage),
                        value =>
                        {
                            if (LocalizationCatalog.TryGetLanguageValue(languageOptions, value, out var language))
                                Update(services, s => s with { AppLanguage = language });
                        })),
                Card(t.Message(Loc.App.FontFamilyTitle), t.Message(Loc.App.FontFamilyDescription),
                    TextBox(Props.Settings.FontFamily, value => Update(services, s => s with { FontFamily = value }))
                        .AutomationName(t.Message(Loc.App.FontFamilyTitle))));
    }

    private ScrollViewerElement About(AppServices services, IntlAccessor t)
    {
        var version = string.IsNullOrWhiteSpace(Props.Settings.LastKnownVersion)
            ? t.Message(Loc.App.DevelopmentVersion)
            : Props.Settings.LastKnownVersion;
        return Page(t.Message(Loc.App.AboutTitle), t.Message(Loc.App.AboutDescription),
            Card(AppBranding.Name, t.Message(Loc.App.AboutProjectDescription), VStack(8,
                TextBlock(t.Message(Loc.App.VersionLabel, ("version", version))),
                HyperlinkButton(t.Message(Loc.App.GitHubRepoLink), new Uri("https://github.com/unchihugo/FluentFlyout")),
                Button(t.Message(Loc.App.CheckForUpdates), () => _ = services.Updater.CheckAsync(Props.Settings.LastKnownVersion))
                    .AutomationName(t.Message(Loc.App.CheckForUpdates)))));
    }

    private ScrollViewerElement AppFiltering(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.AppFilteringTitle), t.Message(Loc.App.AppFilteringRulesDescription),
            Card(t.Message(Loc.App.EnableAppFilteringTitle), t.Message(Loc.App.EnableAppFilteringDescription),
                Toggle(t, Props.Settings.AppFilteringEnabled, value => Update(services, s => s with { AppFilteringEnabled = value }))),
            Card(t.Message(Loc.App.AppFilteringModeTitle), t.Message(Loc.App.AppFilteringModeDescription),
                Combo(t, [t.Message(Loc.App.AppFilteringModeBlacklist), t.Message(Loc.App.AppFilteringModeWhitelist)],
                    Props.Settings.AppFilteringMode,
                    value => Update(services, s => s with { AppFilteringMode = value }))),
            Card(t.Message(Loc.App.AllowedAppsTitle), t.Message(Loc.App.AllowedAppsDescription),
                TextBox(string.Join(Environment.NewLine, Props.Settings.AllowedApps),
                    value => Update(services, s => s with { AllowedApps = Entries(value) }))
                    .AutomationName(t.Message(Loc.App.AllowedAppsTitle))),
            Card(t.Message(Loc.App.BlockedAppsTitle), t.Message(Loc.App.BlockedAppsDescription),
                TextBox(string.Join(Environment.NewLine, Props.Settings.BlockedApps),
                    value => Update(services, s => s with { BlockedApps = Entries(value) }))
                    .AutomationName(t.Message(Loc.App.BlockedAppsTitle))));

    private ScrollViewerElement Advanced(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.AdvancedSettingsTitle), t.Message(Loc.App.AdvancedDescription),
            Card(t.Message(Loc.App.LegacyTaskbarWidthTitle), t.Message(Loc.App.LegacyTaskbarWidthDescription),
                Toggle(t, Props.Settings.LegacyTaskbarWidthEnabled, value => Update(services, s => s with { LegacyTaskbarWidthEnabled = value }))),
            Card(t.Message(Loc.App.AnonymousUsageDataTitle), t.Message(Loc.App.AnonymousUsageDataDescription),
                Toggle(t, Props.Settings.AnonymousTelemetryAllowed, value => Update(services, s => s with { AnonymousTelemetryAllowed = value }))),
            Card(t.Message(Loc.App.BackupRestoreCardTitle), t.Message(Loc.App.SettingsDataDescription), HStack(8,
                Button(t.Message(Loc.App.ExportSettings), () =>
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(services.Settings.Export());
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                })
                    .AutomationName(t.Message(Loc.App.ExportSettings)),
                Button(t.Message(Loc.App.SaveNow), services.Settings.SaveNow)
                    .AutomationName(t.Message(Loc.App.SaveNow)))));

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

    private static ToggleSwitchElement Toggle(IntlAccessor t, bool value, Action<bool> setter)
        => ToggleSwitch(Optional<bool>.Of(value), setter)
            .AutomationName(t.Message(Loc.App.ToggleSetting));

    private static NumberBoxElement Number(IntlAccessor t, int value, Action<int> setter)
        => NumberBox(Optional<double>.Of(value), number => setter((int)Math.Round(number)))
            .AutomationName(t.Message(Loc.App.NumericSetting));

    private static ComboBoxElement Combo(IntlAccessor t, string[] items, int selected, Action<int> setter)
        => ComboBox(items, Optional<int>.Of(Math.Clamp(selected, 0, Math.Max(0, items.Length - 1))), setter)
            .AutomationName(t.Message(Loc.App.ChooseSetting));

    private static void Update(AppServices services, Func<SettingsSnapshot, SettingsSnapshot> reducer)
        => services.Settings.Update(reducer);

    private static string[] Entries(string value)
        => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
