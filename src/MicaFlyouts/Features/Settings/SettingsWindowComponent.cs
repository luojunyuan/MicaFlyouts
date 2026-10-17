using System.Diagnostics;
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
using static Microsoft.UI.Reactor.Core.InfoBarElement;
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
        var (isPaneOpen, setPaneOpen) = UseState(true);
        var page = Props.Page;
        var setPage = Props.SetPage;
        var menu = SettingsSearchIndex.MenuItems(t);
        var content = Component<SettingsPageComponent, SettingsPageProps>(new(page, settings, setPage));
        var searchEntries = UseMemo(() => SettingsSearchIndex.Query(query, t), query, t.Locale);
        var searchSuggestions = searchEntries
            .Select(entry => t.Message(entry.TitleKey))
            .ToArray();

        return RenderSettingsFrame(
            t,
            RenderTitleBar(query, setQuery, searchSuggestions, t, page, setPage, isPaneOpen, setPaneOpen),
            RenderNavigation(menu, content, page, setPage, isPaneOpen, setPaneOpen, t));
    }

    private static TitleBarElement RenderTitleBar(
        string query,
        Action<string> setQuery,
        string[] searchSuggestions,
        IntlAccessor t,
        SettingsPage page,
        Action<SettingsPage> setPage,
        bool isPaneOpen,
        Action<bool> setPaneOpen)
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
            .Width(300)
            .PlaceholderText(t.Message(Loc.App.SearchSettings))
            .QueryIcon(SymbolIcon("Find"))
            .AutomationName(t.Message(Loc.App.SearchSettings));

        return (TitleBar(AppBranding.Name) with
        {
            Content = search,
            Icon = AppBranding.TitleBarIcon,
            IsBackButtonVisible = true,
            IsBackButtonEnabled = page != SettingsPage.Home,
            OnBackRequested = () => setPage(SettingsPage.Home),
            IsPaneToggleButtonVisible = true,
            OnPaneToggleRequested = () => setPaneOpen(!isPaneOpen),
        }).Grid(row: 0);
    }

    private static NavigationViewElement RenderNavigation(
        NavigationViewItemData[] menu,
        Element content,
        SettingsPage page,
        Action<SettingsPage> setPage,
        bool isPaneOpen,
        Action<bool> setPaneOpen,
        IntlAccessor t)
    {
        var navigation = (NavigationView(menu, content) with
        {
            SelectedTag = SettingsSearchIndex.Tag(page),
            OnSelectedTagChanged = tag =>
            {
                if (SettingsSearchIndex.TryParse(tag, out var selected))
                    setPage(selected);
            },
            IsSettingsVisible = false,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible = false,
            IsPaneOpen = isPaneOpen,
            OnPaneOpenChanged = setPaneOpen,
            IsTitleBarAutoPaddingEnabled = false,
            FooterMenuItems =
            [
                PageItem(t.Message(Loc.App.AboutTitle), SettingsPage.About, FluentSystemIcon.Info24),
            ],
        }).Set(control =>
        {
            var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.Resources["NavigationViewContentBackground"] = transparentBrush;
            control.Resources["NavigationViewContentGridBorderBrush"] = transparentBrush;
        }).VerticalAlignment(VerticalAlignment.Stretch);

        return navigation.Grid(row: 1);
    }

    private static NavigationViewItemData PageItem(string content, SettingsPage page, FluentSystemIcon icon)
        => NavItem(content, null, SettingsSearchIndex.Tag(page)) with
        {
            IconElement = FluentSystemIcons.CreatePathIconData(icon),
        };

    private static BorderElement RenderSettingsFrame(IntlAccessor t, Element titleBar, Element navigation)
        => Border(Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Auto, GridSize.Star()],
                titleBar,
                navigation))
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
        PageItem(t.Message(Loc.App.MediaFlyoutTitle), SettingsPage.Media, FluentSystemIcon.VideoPlayPause24),
        PageItem(t.Message(Loc.App.VolumeFlyoutTitle), SettingsPage.Volume, FluentSystemIcon.Speaker224),
        PageItem(
            t.Message(Loc.App.TaskbarWidgetCustomizationTitle),
            SettingsPage.Taskbar,
            FluentSystemIcon.TextboxAlignBottom24),
        PageItem(
            t.Message(Loc.App.TaskbarVisualizerTitle),
            SettingsPage.Visualizer,
            FluentSystemIcon.SoundWaveCircle24),
        PageItem(
            t.Message(Loc.App.NextUpCustomizationTitle),
            SettingsPage.NextUp,
            FluentSystemIcon.MusicNote2Play20),
        PageItem(
            t.Message(Loc.App.LockKeysCustomizationTitle),
            SettingsPage.LockKeys,
            FluentSystemIcon.LockMultiple24),
        NavItemHeader(t.Message(Loc.App.ApplicationSectionTitle)),
        PageItem(t.Message(Loc.App.SystemSettingsTitle), SettingsPage.System, FluentSystemIcon.Settings24),
        PageItem(t.Message(Loc.App.AppFilteringTitle), SettingsPage.AppFiltering, FluentSystemIcon.Filter24),
        PageItem(t.Message(Loc.App.AdvancedSettingsTitle), SettingsPage.Advanced, FluentSystemIcon.Wrench24),
    ];

    private static NavigationViewItemData PageItem(string content, SettingsPage page, FluentSystemIcon icon)
        => NavItem(content, null, Tag(page)) with
        {
            IconElement = FluentSystemIcons.CreatePathIconData(icon),
        };

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

public sealed record SettingsPageProps(
    SettingsPage Page,
    SettingsSnapshot Settings,
    Action<SettingsPage> SetPage);

public sealed class SettingsPageComponent : Component<SettingsPageProps>
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        return Props.Page switch
        {
            SettingsPage.Home => Component<SettingsHomeComponent, SettingsHomeProps>(
                new(Props.Settings, Props.SetPage)),
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

    private ScrollViewerElement Media(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.MediaFlyoutTitle), t.Message(Loc.App.MediaFlyoutDescription),
            "ms-appx:///Assets/FluentFlyoutDemo3.1.png",
            SettingCard(FluentSystemIcon.VideoPlayPause24, t.Message(Loc.App.TextBlockText1), t.Message(Loc.App.TextBlockText3),
                Toggle(t, Props.Settings.MediaFlyoutEnabled, value => Update(services, s => s with { MediaFlyoutEnabled = value }))),
            Section(t.Message(Loc.App.MediaFlyoutAppearanceSectionTitle),
                SettingCard(FluentSystemIcon.Color24, t.Message(Loc.App.BackgroundBlurTitle), t.Message(Loc.App.TextBlockText2),
                    Combo(t,
                        [
                            t.Message(Loc.App.BackgroundBlurOption1),
                            t.Message(Loc.App.BackgroundBlurOption2),
                            t.Message(Loc.App.BackgroundBlurOption3),
                            t.Message(Loc.App.BackgroundBlurOption4),
                        ],
                        Props.Settings.MediaFlyoutBackgroundBlur,
                        static _ => { }),
                    enabled: false),
                SettingCard(FluentSystemIcon.ArrowAutofitWidth24, t.Message(Loc.App.CompactLayoutTitle), t.Message(Loc.App.CompactLayoutDescription),
                    Toggle(t, Props.Settings.CompactLayout, value => Update(services, s => s with { CompactLayout = value }))),
                SettingCard(FluentSystemIcon.ArrowMove24, t.Message(Loc.App.FlyoutPositionTitle), string.Empty,
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
                        value => Update(services, s => s with { Position = value })))),
            Section(t.Message(Loc.App.MediaFlyoutTimingSectionTitle),
                SettingCard(FluentSystemIcon.Timer24, t.Message(Loc.App.FlyoutStayDurationTitle), t.Message(Loc.App.FlyoutStayDurationDescription),
                    Number(t, Props.Settings.Duration, value => Update(services, s => s with { Duration = value }))),
                SettingCard(FluentSystemIcon.TimerOff24, t.Message(Loc.App.MediaFlyoutAlwaysDisplay), t.Message(Loc.App.MediaFlyoutAlwaysDisplayDescription),
                    Toggle(t, Props.Settings.MediaFlyoutAlwaysDisplay, value => Update(services, s => s with { MediaFlyoutAlwaysDisplay = value })))),
            Section(t.Message(Loc.App.MediaFlyoutTrackSectionTitle),
                SettingCard(FluentSystemIcon.AlignCenterHorizontal24, t.Message(Loc.App.CenterTitleArtistTitle), t.Message(Loc.App.CenterTitleArtistDescription),
                    Toggle(t, Props.Settings.CenterTitleArtist, value => Update(services, s => s with { CenterTitleArtist = value }))),
                SettingCard(FluentSystemIcon.Subtitles24, t.Message(Loc.App.ShowMediaPlayerNameTitle), t.Message(Loc.App.PlayerInformationDescription),
                    Toggle(t, Props.Settings.PlayerInfoEnabled, value => Update(services, s => s with { PlayerInfoEnabled = value })))),
            Section(t.Message(Loc.App.MediaFlyoutControlsSectionTitle),
                SettingCard(FluentSystemIcon.ArrowRepeatAll24, t.Message(Loc.App.RepeatButtonTitle), t.Message(Loc.App.RepeatButtonDescription),
                    Toggle(t, Props.Settings.RepeatEnabled, value => Update(services, s => s with { RepeatEnabled = value }))),
                SettingCard(FluentSystemIcon.ArrowShuffle24, t.Message(Loc.App.ShuffleButtonTitle), t.Message(Loc.App.ShuffleButtonDescription),
                    Toggle(t, Props.Settings.ShuffleEnabled, value => Update(services, s => s with { ShuffleEnabled = value }))),
                SettingCard(FluentSystemIcon.SlideArrowRight24, t.Message(Loc.App.ShowSeekbarTitle), t.Message(Loc.App.ShowSeekbarDescription),
                    Toggle(t, Props.Settings.SeekbarEnabled, value => Update(services, s => s with { SeekbarEnabled = value }))),
                Info(t.Message(Loc.App.UnsupportedControlsWarning), InfoBarSeverity.Warning)),
            Section(t.Message(Loc.App.MediaFlyoutBehaviorSectionTitle),
                SettingCard(FluentSystemIcon.Pause24, t.Message(Loc.App.PauseOtherMediaTitle), t.Message(Loc.App.PauseOtherMediaDescription),
                    Toggle(t, Props.Settings.PauseOtherSessionsEnabled, static _ => { }),
                    enabled: false),
                SettingCard(FluentSystemIcon.Speaker224, t.Message(Loc.App.MediaFlyoutExcludeVolumeTitle), t.Message(Loc.App.MediaFlyoutExcludeVolumeDescription),
                    Toggle(t, Props.Settings.MediaFlyoutVolumeKeysExcluded, value => Update(services, s => s with { MediaFlyoutVolumeKeysExcluded = value })))));

    private ScrollViewerElement Volume(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.VolumeFlyoutTitle), t.Message(Loc.App.VolumeFlyoutDescription),
            "ms-appx:///Assets/FluentFlyoutVolumeDemo.png",
            SettingCard(FluentSystemIcon.Speaker224, t.Message(Loc.App.EnableVolumeFlyoutTitle), t.Message(Loc.App.EnableVolumeFlyoutDescription),
                Toggle(t, Props.Settings.VolumeControlEnabled, value => Update(services, s => s with { VolumeControlEnabled = value }))),
            SettingCard(FluentSystemIcon.ArrowMove24, t.Message(Loc.App.VolumeAboveMediaFlyoutTitle), t.Message(Loc.App.VolumeAboveMediaFlyoutDescription),
                Toggle(t, Props.Settings.VolumeControlAboveMediaFlyout, value => Update(services, s => s with { VolumeControlAboveMediaFlyout = value }))),
            SettingCard(FluentSystemIcon.Timer24, t.Message(Loc.App.VolumeFlyoutStayDurationTitle), t.Message(Loc.App.VolumeFlyoutStayDurationDescription),
                Number(t, Props.Settings.VolumeControlDuration, value => Update(services, s => s with { VolumeControlDuration = value }))),
            Section(t.Message(Loc.App.VolumeMixerSectionTitle),
                SettingCard(FluentSystemIcon.SpeakerSettings24, t.Message(Loc.App.EnableVolumeMixerTitle), t.Message(Loc.App.EnableVolumeMixerDescription),
                    Toggle(t, Props.Settings.VolumeMixerEnabled, value => Update(services, s => s with { VolumeMixerEnabled = value }))),
                SettingCard(FluentSystemIcon.Highlight24, t.Message(Loc.App.VolumeMixerHighlightTitle), t.Message(Loc.App.VolumeMixerHighlightDescription),
                    Toggle(t, Props.Settings.VolumeMixerHighlightActiveApps, value => Update(services, s => s with { VolumeMixerHighlightActiveApps = value })))));

    private ScrollViewerElement Taskbar(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.TaskbarWidgetCustomizationTitle), t.Message(Loc.App.TaskbarWidgetDescription),
            "ms-appx:///Assets/FluentFlyoutDemo9.1.png",
            SettingCard(FluentSystemIcon.TextboxAlignBottom24, t.Message(Loc.App.TaskbarWidgetEnabledTitle), t.Message(Loc.App.TaskbarWidgetEnabledDescription),
                Toggle(t, Props.Settings.TaskbarWidgetEnabled, value => Update(services, s => s with { TaskbarWidgetEnabled = value }))),
            Section(t.Message(Loc.App.AppearanceBehaviorSectionTitle),
                Info(t.Message(Loc.App.TaskbarWidgetVisualIssuesWarning), InfoBarSeverity.Informational),
                SettingCard(FluentSystemIcon.ArrowMove24, t.Message(Loc.App.TaskbarWidgetPosition), t.Message(Loc.App.WidgetPositionDescription),
                    Combo(t,
                        [t.Message(Loc.App.PositionStart), t.Message(Loc.App.PositionCenter), t.Message(Loc.App.PositionEnd)],
                        Props.Settings.TaskbarWidgetPosition,
                        value => Update(services, s => s with { TaskbarWidgetPosition = value }))),
                SettingsGroup(FluentSystemIcon.Wrench24,
                    t.Message(Loc.App.TaskbarWidgetPaddingSettingsTitle),
                    t.Message(Loc.App.TaskbarWidgetPaddingSettingsDescription),
                    [
                        SettingCard(FluentSystemIcon.PaddingRight24, t.Message(Loc.App.TaskbarWidgetPaddingTitle), t.Message(Loc.App.TaskbarWidgetPaddingDescription),
                            Toggle(t, Props.Settings.TaskbarWidgetPadding, value => Update(services, s => s with { TaskbarWidgetPadding = value }))),
                        SettingCard(FluentSystemIcon.ArrowFit20, t.Message(Loc.App.TaskbarWidgetManualPaddingTitle), t.Message(Loc.App.TaskbarWidgetManualPaddingDescription),
                            Number(t, Props.Settings.TaskbarWidgetManualPadding, value => Update(services, s => s with { TaskbarWidgetManualPadding = value }))),
                    ]),
                SettingCard(FluentSystemIcon.ArrowAutofitWidth24, t.Message(Loc.App.TaskbarWidgetFixedWidthTitle), t.Message(Loc.App.TaskbarWidgetFixedWidthDescription),
                    Toggle(t, Props.Settings.TaskbarWidgetFixedWidth, value => Update(services, s => s with { TaskbarWidgetFixedWidth = value }))),
                SettingCard(FluentSystemIcon.EyeLines24, t.Message(Loc.App.TaskbarWidgetAutoHideTitle), t.Message(Loc.App.TaskbarWidgetAutoHideDescription),
                    Toggle(t, Props.Settings.TaskbarWidgetAutoHide, value => Update(services, s => s with { TaskbarWidgetAutoHide = value }))),
                SettingCard(FluentSystemIcon.Pause24, t.Message(Loc.App.TaskbarWidgetShowPauseOverlayTitle), t.Message(Loc.App.TaskbarWidgetShowPauseOverlayDescription),
                    Toggle(t, Props.Settings.TaskbarWidgetShowPauseOverlay, value => Update(services, s => s with { TaskbarWidgetShowPauseOverlay = value }))),
                SettingsGroup(FluentSystemIcon.ArrowAutofitWidth24,
                    t.Message(Loc.App.TaskbarWidgetScrollingTextTitle),
                    t.Message(Loc.App.TaskbarWidgetScrollingTextDescription),
                    [
                        SettingCard(FluentSystemIcon.EyeLines24, t.Message(Loc.App.TaskbarWidgetScrollingTextLoopForeverTitle), t.Message(Loc.App.TaskbarWidgetScrollingTextLoopForeverDescription),
                            Toggle(t, Props.Settings.TaskbarWidgetScrollingTextLoopForever, value => Update(services, s => s with { TaskbarWidgetScrollingTextLoopForever = value }))),
                        SettingCard(FluentSystemIcon.ArrowFit20, t.Message(Loc.App.TaskbarWidgetScrollingTextSpeedTitle), t.Message(Loc.App.TaskbarWidgetScrollingTextSpeedDescription),
                            Number(t, Props.Settings.TaskbarWidgetScrollingTextSpeed, value => Update(services, s => s with { TaskbarWidgetScrollingTextSpeed = value }))),
                    ],
                    headerContent: Toggle(t, Props.Settings.TaskbarWidgetScrollingEnabled, value => Update(services, s => s with { TaskbarWidgetScrollingEnabled = value })))));

    private ScrollViewerElement Visualizer(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.TaskbarVisualizerTitle), t.Message(Loc.App.TaskbarVisualizerDescription),
            "ms-appx:///Assets/FluentFlyoutTaskbar-NoTitle-Zoomed.png",
            SettingCard(FluentSystemIcon.SoundWaveCircle24, t.Message(Loc.App.TaskbarVisualizerEnabledTitle), t.Message(Loc.App.TaskbarVisualizerEnabledDescription),
                Toggle(t, Props.Settings.TaskbarVisualizerEnabled, value => Update(services, s => s with { TaskbarVisualizerEnabled = value }))),
            Section(t.Message(Loc.App.AppearanceBehaviorSectionTitle),
                SettingCard(FluentSystemIcon.DataBarVertical24, t.Message(Loc.App.TaskbarVisualizerBarCountTitle), t.Message(Loc.App.TaskbarVisualizerBarCountDescription),
                    Number(t, Props.Settings.TaskbarVisualizerBarCount, value => Update(services, s => s with { TaskbarVisualizerBarCount = value }))),
                SettingCard(FluentSystemIcon.AlignCenterHorizontal24, t.Message(Loc.App.TaskbarVisualizerCenteredBarsTitle), t.Message(Loc.App.TaskbarVisualizerCenteredBarsDescription),
                    Toggle(t, Props.Settings.TaskbarVisualizerCenteredBars, value => Update(services, s => s with { TaskbarVisualizerCenteredBars = value }))),
                SettingsGroup(FluentSystemIcon.LineHorizontal120,
                    t.Message(Loc.App.TaskbarVisualizerBaselineTitle),
                    t.Message(Loc.App.TaskbarVisualizerBaselineDescription),
                    [
                        SettingCard(FluentSystemIcon.EyeLines24, t.Message(Loc.App.BaselineAutoHideTitle), t.Message(Loc.App.TaskbarVisualizerBaselineAutoHideDescription),
                            Toggle(t, Props.Settings.TaskbarVisualizerBaselineAutoHide, value => Update(services, s => s with { TaskbarVisualizerBaselineAutoHide = value }))),
                    ],
                    headerContent: Toggle(t, Props.Settings.TaskbarVisualizerBaseline, value => Update(services, s => s with { TaskbarVisualizerBaseline = value })))),
            Section(t.Message(Loc.App.TaskbarVisualizerAudioSectionTitle),
                SettingCard(FluentSystemIcon.BezierCurveSquare20, t.Message(Loc.App.TaskbarVisualizerAudioSensitivityTitle), t.Message(Loc.App.TaskbarVisualizerAudioSensitivityDescription),
                    Combo(t,
                        [t.Message(Loc.App.SensitivityLow), t.Message(Loc.App.SensitivityMedium), t.Message(Loc.App.SensitivityHigh)],
                        Props.Settings.TaskbarVisualizerAudioSensitivity - 1,
                        value => Update(services, s => s with { TaskbarVisualizerAudioSensitivity = value + 1 })))));

    private ScrollViewerElement NextUp(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.NextUpCustomizationTitle), t.Message(Loc.App.NextUpDescription),
            "ms-appx:///Assets/FluentFlyoutDemo4.1.png",
            SettingCard(FluentSystemIcon.MusicNote2Play20, t.Message(Loc.App.EnableNextUpTitle), t.Message(Loc.App.EnableNextUpDescription),
                Toggle(t, Props.Settings.NextUpEnabled, value => Update(services, s => s with { NextUpEnabled = value }))),
            Section(t.Message(Loc.App.NextUpTimingSectionTitle),
                SettingCard(FluentSystemIcon.Timer24, t.Message(Loc.App.NextUpStayDurationTitle), t.Message(Loc.App.NextUpStayDurationDescription),
                    Number(t, Props.Settings.NextUpDuration, value => Update(services, s => s with { NextUpDuration = value })))));

    private ScrollViewerElement LockKeys(AppServices services, IntlAccessor t)
        => IllustratedPage(t.Message(Loc.App.LockKeysCustomizationTitle), t.Message(Loc.App.LockKeysDescription),
            "ms-appx:///Assets/FluentFlyoutDemo5.1.png",
            SettingCard(FluentSystemIcon.LockMultiple24, t.Message(Loc.App.EnableLockKeysTitle), t.Message(Loc.App.EnableLockKeysDescription),
                Toggle(t, Props.Settings.LockKeysEnabled, value => Update(services, s => s with { LockKeysEnabled = value }))),
            Section(t.Message(Loc.App.AppearanceBehaviorSectionTitle),
                SettingCard(FluentSystemIcon.Timer24, t.Message(Loc.App.LockKeysStayDurationTitle), t.Message(Loc.App.LockKeysStayDurationDescription),
                    Number(t, Props.Settings.LockKeysDuration, value => Update(services, s => s with { LockKeysDuration = value }))),
                SettingCard(FluentSystemIcon.TextFontInfo24, t.Message(Loc.App.LockKeysBoldUITitle), t.Message(Loc.App.LockKeysBoldUIDescription),
                    Toggle(t, Props.Settings.LockKeysBoldUi, value => Update(services, s => s with { LockKeysBoldUi = value }))),
                SettingCard(FluentSystemIcon.Wand24, t.Message(Loc.App.LockKeysAnimatedTitle), t.Message(Loc.App.LockKeysAnimatedDescription),
                    Toggle(t, Props.Settings.LockKeysAnimated, value => Update(services, s => s with { LockKeysAnimated = value }))),
                SettingCard(FluentSystemIcon.ShareScreenStart24, t.Message(Loc.App.LockKeysCursorUITitle), t.Message(Loc.App.LockKeysCursorUIDescription),
                    Combo(t,
                        [t.Message(Loc.App.DefaultLockLayoutPosition), t.Message(Loc.App.MonitorWithFocusedWindow), t.Message(Loc.App.MonitorWithCursor)],
                        Props.Settings.LockKeysMonitorPreference,
                        value => Update(services, s => s with { LockKeysMonitorPreference = value })))),
            Section(t.Message(Loc.App.LockKeysKeysSectionTitle),
                SettingCard(FluentSystemIcon.Info24, t.Message(Loc.App.EnableInsertKeyTitle), t.Message(Loc.App.EnableInsertKeyDescription),
                    Toggle(t, Props.Settings.LockKeysInsertEnabled, value => Update(services, s => s with { LockKeysInsertEnabled = value }))),
                SettingCard(FluentSystemIcon.TextCaseTitle24, t.Message(Loc.App.EnableCapsTitle), t.Message(Loc.App.EnableCapsDescription),
                    Toggle(t, Props.Settings.LockKeysCapsEnabled, value => Update(services, s => s with { LockKeysCapsEnabled = value }))),
                SettingCard(FluentSystemIcon.NumberSymbol24, t.Message(Loc.App.EnableNumTitle), t.Message(Loc.App.EnableNumDescription),
                    Toggle(t, Props.Settings.LockKeysNumEnabled, value => Update(services, s => s with { LockKeysNumEnabled = value }))),
                SettingCard(FluentSystemIcon.ArrowBetweenDown24, t.Message(Loc.App.EnableScrollTitle), t.Message(Loc.App.EnableScrollDescription),
                    Toggle(t, Props.Settings.LockKeysScrollEnabled, value => Update(services, s => s with { LockKeysScrollEnabled = value })))));

    private ScrollViewerElement System(AppServices services, IntlAccessor t)
    {
        var languageOptions = LocalizationCatalog.CreateLanguageOptions(
            t.Message(Loc.App.SystemSettingsTitle));
        return Page(t.Message(Loc.App.SystemSettingsTitle), t.Message(Loc.App.SystemDescription),
            Section(t.Message(Loc.App.AppearanceBehaviorSectionTitle),
                SettingCard(FluentSystemIcon.Globe24, t.Message(Loc.App.AppLanguageTitle), t.Message(Loc.App.AppLanguageDescription),
                    Combo(t,
                        [.. languageOptions.Select(option => option.DisplayName)],
                        LocalizationCatalog.IndexOfLanguage(languageOptions, Props.Settings.AppLanguage),
                        value =>
                        {
                            if (LocalizationCatalog.TryGetLanguageValue(languageOptions, value, out var language))
                                Update(services, s => s with { AppLanguage = language });
                        })),
                SettingCard(FluentSystemIcon.Color24, t.Message(Loc.App.AppThemeTitle), t.Message(Loc.App.AppThemeDescription),
                    Combo(t,
                        [t.Message(Loc.App.AppThemeDefault), t.Message(Loc.App.AppThemeLight), t.Message(Loc.App.AppThemeDark)],
                        Props.Settings.AppTheme,
                        value => Update(services, s => s with { AppTheme = value }))),
                SettingCard(FluentSystemIcon.TextFontInfo24, t.Message(Loc.App.FontFamilyTitle), t.Message(Loc.App.FontFamilyDescription),
                    TextBox(Props.Settings.FontFamily, value => Update(services, s => s with { FontFamily = value }))
                        .AutomationName(t.Message(Loc.App.FontFamilyTitle))),
                SettingCard(FluentSystemIcon.ShareScreenStart24, t.Message(Loc.App.LaunchOnStartupTitle), t.Message(Loc.App.LaunchOnStartupDescription),
                    Toggle(t, Props.Settings.Startup, value => Update(services, s => s with { Startup = value }))),
                SettingCard(FluentSystemIcon.Bug24, t.Message(Loc.App.AnonymousUsageDataTitle), t.Message(Loc.App.AnonymousUsageDataDescription),
                    Toggle(t, Props.Settings.AnonymousTelemetryAllowed, value => Update(services, s => s with { AnonymousTelemetryAllowed = value })))),
            Section(t.Message(Loc.App.TrayIconSectionTitle),
                SettingCard(FluentSystemIcon.PaintBrush24, t.Message(Loc.App.Win11TrayIconTitle), t.Message(Loc.App.Win11TrayIconDescription),
                    Toggle(t, Props.Settings.NIconSymbol, value => Update(services, s => s with { NIconSymbol = value }))),
                SettingCard(FluentSystemIcon.EyeOff24, t.Message(Loc.App.HideTrayIconTitle), t.Message(Loc.App.HideTrayIconDescription),
                    Toggle(t, Props.Settings.NIconHide, value => Update(services, s => s with { NIconHide = value })))),
            Section(t.Message(Loc.App.BackupRestoreSectionTitle),
                SettingCard(FluentSystemIcon.ArrowCounterclockwise24, t.Message(Loc.App.BackupRestoreCardTitle), t.Message(Loc.App.SettingsDataDescription), HStack(8,
                    Button(t.Message(Loc.App.ExportSettings), () =>
                    {
                        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        package.SetText(services.Settings.Export());
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    })
                        .AutomationName(t.Message(Loc.App.ExportSettings)),
                    Button(t.Message(Loc.App.SaveNow), services.Settings.SaveNow)
                        .AutomationName(t.Message(Loc.App.SaveNow))))));
    }

    private static ScrollViewerElement About(AppServices services, IntlAccessor t)
    {
        return Page(t.Message(Loc.App.AboutTitle), t.Message(Loc.App.AboutDescription),
            SettingCard(FluentSystemIcon.Code24, AppBranding.Name, t.Message(Loc.App.AboutProjectDescription), VStack(8,
                TextBlock(t.Message(Loc.App.VersionLabel, ("version", AppBranding.Version))),
                RepositoryLink(t),
                Button(t.Message(Loc.App.CheckForUpdates), () => _ = services.Updater.CheckAsync(AppBranding.Version))
                    .AutomationName(t.Message(Loc.App.CheckForUpdates)))),
            VStack(8,
                Heading(t.Message(Loc.App.AboutContributorsTitle)),
                Caption(t.Message(Loc.App.AboutContributorsDescription))
                    .TextWrapping(TextWrapping.Wrap)
                    .Opacity(0.75),
                SettingsGroup(FluentSystemIcon.Code24, t.Message(Loc.App.DevelopersTitle), string.Empty, [],
                    itemsHeader: TextBlock(string.Join(", ", AboutPageData.Developers))
                        .FontSize(14)
                        .TextWrapping(TextWrapping.Wrap)
                        .Opacity(0.75)
                        .MinWidth(0)
                        .Margin(8)),
                SettingsGroup(FluentSystemIcon.Globe24, t.Message(Loc.App.TranslatorsTitle), string.Empty, [],
                    itemsHeader: TextBlock(string.Join(", ", AboutPageData.Translators.Order()))
                        .FontSize(14)
                        .TextWrapping(TextWrapping.Wrap)
                        .Opacity(0.75)
                        .MinWidth(0)
                        .Margin(8))),
            VStack(8,
                Heading(t.Message(Loc.App.AboutOpenSourceLicensesTitle)),
                Caption(t.Message(Loc.App.AboutOpenSourceLicensesDescription))
                    .TextWrapping(TextWrapping.Wrap)
                    .Opacity(0.75),
                VStack(0, [.. AboutPageData.Packages.Select(RenderLicenseCard)])),
            HStack(8,
                Icon(SymbolIcon("SolidStar")).Width(24).Height(24),
                TextBlock(t.Message(Loc.App.EnjoyingAppText))
                    .FontSize(12)
                    .TextWrapping(TextWrapping.Wrap)
                    .Opacity(0.5)
                    .MinWidth(0),
                RepositoryLink(t))
                .Margin(top: 20, bottom: 12));
    }

    private static ButtonElement RepositoryLink(IntlAccessor t)
        => Button(HStack(4,
                TextBlock(t.Message(Loc.App.GitHubRepoLink)),
                FluentSystemIcons.CreateOpenLinkIcon()),
            () => OpenUri(AppBranding.RepositoryUrl))
            .SubtleButton()
            .AutomationName(t.Message(Loc.App.GitHubRepoLink));

    private static SettingsCardElement RenderLicenseCard(OpenSourcePackage package)
        => (SettingsCard() with
        {
            Header = package.Name,
            Description = $"{package.Version} | {package.License}",
            IsClickEnabled = true,
            IsActionIconVisible = true,
            ActionIcon = FluentSystemIcons.CreateOpenLinkIcon(),
            ActionIconToolTip = package.Name,
            OnClick = () => OpenUri(package.Url),
        })
            .MinWidth(0)
            .Margin(4)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .AutomationName($"{package.Name}, {package.Version}, {package.License}");

    private static void OpenUri(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private ScrollViewerElement AppFiltering(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.AppFilteringTitle), t.Message(Loc.App.AppFilteringRulesDescription),
            Section(t.Message(Loc.App.AppFilteringRulesSectionTitle),
                SettingCard(FluentSystemIcon.Filter24, t.Message(Loc.App.EnableAppFilteringTitle), t.Message(Loc.App.EnableAppFilteringDescription),
                    Toggle(t, Props.Settings.AppFilteringEnabled, value => Update(services, s => s with { AppFilteringEnabled = value }))),
                SettingCard(FluentSystemIcon.Wrench24, t.Message(Loc.App.AppFilteringModeTitle), t.Message(Loc.App.AppFilteringModeDescription),
                    Combo(t, [t.Message(Loc.App.AppFilteringModeBlacklist), t.Message(Loc.App.AppFilteringModeWhitelist)],
                        Props.Settings.AppFilteringMode,
                        value => Update(services, s => s with { AppFilteringMode = value })))),
            SettingsGroup(FluentSystemIcon.Info24,
                t.Message(Loc.App.AllowedAppsTitle),
                t.Message(Loc.App.AllowedAppsDescription),
                [],
                itemsHeader: AppListEditor(Props.Settings.AllowedApps, t.Message(Loc.App.AllowedAppsTitle),
                    value => Update(services, s => s with { AllowedApps = Entries(value) })),
                expanded: true,
                visible: Props.Settings.AppFilteringMode == 1,
                enabled: Props.Settings.AppFilteringEnabled),
            SettingsGroup(FluentSystemIcon.EyeOff24,
                t.Message(Loc.App.BlockedAppsTitle),
                t.Message(Loc.App.BlockedAppsDescription),
                [],
                itemsHeader: AppListEditor(Props.Settings.BlockedApps, t.Message(Loc.App.BlockedAppsTitle),
                    value => Update(services, s => s with { BlockedApps = Entries(value) })),
                expanded: true,
                visible: Props.Settings.AppFilteringMode == 0,
                enabled: Props.Settings.AppFilteringEnabled));

    private ScrollViewerElement Advanced(AppServices services, IntlAccessor t)
        => Page(t.Message(Loc.App.AdvancedSettingsTitle), t.Message(Loc.App.AdvancedDescription),
            Section(t.Message(Loc.App.AppearanceBehaviorSectionTitle),
                SettingsGroup(FluentSystemIcon.Wand24,
                    t.Message(Loc.App.AnimationSettingsTitle),
                    t.Message(Loc.App.AnimationSettingsDescription),
                    [
                        SettingCard(FluentSystemIcon.Wand24, t.Message(Loc.App.AnimationDurationScaleTitle), t.Message(Loc.App.AnimationDurationScaleDescription),
                            Combo(t,
                                [t.Message(Loc.App.AnimationSpeedOff), "0.5x", "1x", "1.5x", "2x", "3x"],
                                Props.Settings.FlyoutAnimationSpeed,
                                value => Update(services, s => s with { FlyoutAnimationSpeed = value }))),
                        SettingCard(FluentSystemIcon.BezierCurveSquare20, t.Message(Loc.App.AnimationEasingStyleTitle), t.Message(Loc.App.AnimationEasingStyleDescription),
                            Combo(t,
                                [t.Message(Loc.App.EasingLinear), t.Message(Loc.App.EasingSine), t.Message(Loc.App.EasingQuadratic), t.Message(Loc.App.EasingCubic)],
                                Props.Settings.FlyoutAnimationEasingStyle,
                                value => Update(services, s => s with { FlyoutAnimationEasingStyle = value }))),
                    ],
                    itemsHeader: Info(t.Message(Loc.App.NativeExperienceInfo), InfoBarSeverity.Informational).Margin(8)),
                SettingsGroup(FluentSystemIcon.Color24,
                    t.Message(Loc.App.AcrylicWindowTitle),
                    t.Message(Loc.App.AcrylicWindowDescription),
                    [
                        SettingCard(FluentSystemIcon.VideoPlayPause24, t.Message(Loc.App.MediaFlyoutTitle), string.Empty,
                            Toggle(t, Props.Settings.MediaFlyoutAcrylicWindowEnabled, value => Update(services, s => s with { MediaFlyoutAcrylicWindowEnabled = value }))),
                        SettingCard(FluentSystemIcon.MusicNote2Play20, t.Message(Loc.App.NextUpCustomizationTitle), string.Empty,
                            Toggle(t, Props.Settings.NextUpAcrylicWindowEnabled, value => Update(services, s => s with { NextUpAcrylicWindowEnabled = value }))),
                        SettingCard(FluentSystemIcon.LockMultiple24, t.Message(Loc.App.LockKeysCustomizationTitle), string.Empty,
                            Toggle(t, Props.Settings.LockKeysAcrylicWindowEnabled, value => Update(services, s => s with { LockKeysAcrylicWindowEnabled = value }))),
                        SettingCard(FluentSystemIcon.SpeakerSettings24, t.Message(Loc.App.VolumeFlyoutTitle), string.Empty,
                            Toggle(t, Props.Settings.VolumeMixerAcrylicWindowEnabled, value => Update(services, s => s with { VolumeMixerAcrylicWindowEnabled = value }))),
                    ]),
                SettingCard(FluentSystemIcon.ArrowCounterclockwise24, t.Message(Loc.App.LegacyTaskbarWidthTitle), t.Message(Loc.App.LegacyTaskbarWidthDescription),
                    Toggle(t, Props.Settings.LegacyTaskbarWidthEnabled, value => Update(services, s => s with { LegacyTaskbarWidthEnabled = value })))));

    private static ScrollViewerElement Page(string title, string description, params Element[] content)
        => PageContent(title,
            Caption(description).TextWrapping(TextWrapping.Wrap).Opacity(0.7),
            content);

    private static ScrollViewerElement IllustratedPage(
        string title,
        string description,
        string imageUri,
        params Element[] content)
        => PageContent(title,
            Grid(
                [GridSize.Px(164), GridSize.Star()],
                [GridSize.Auto],
                Border((Image(imageUri) with { Stretch = "UniformToFill" }).Width(164).Height(98).AutomationName(title))
                    .CornerRadius(10)
                    .Grid(row: 0, column: 0),
                TextBlock(description)
                    .FontSize(14)
                    .TextWrapping(TextWrapping.Wrap)
                    .Opacity(0.7)
                    .MinWidth(0)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Grid(row: 0, column: 1)
                    .Padding(left: 12)),
            content);

    private static ScrollViewerElement PageContent(string title, Element introduction, Element[] content)
        => ScrollViewer(VStack(24,
                Heading(title),
                introduction,
                VStack(4, content))
                .MinWidth(0)
                .HorizontalAlignment(HorizontalAlignment.Stretch))
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Set(scrollViewer =>
            {
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewer.VerticalScrollMode = ScrollMode.Auto;
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            })
            .Padding(new Thickness(24, 24, 24, 0));

    private static StackElement Section(string title, params Element[] content)
        => VStack(4,
                TextBlock(title).FontSize(14).SemiBold().Opacity(0.8),
                VStack(4, content))
            .Margin(top: 8, bottom: 8);

    private static SettingsCardElement SettingCard(
        FluentSystemIcon icon,
        string header,
        string description,
        Element content,
        bool enabled = true)
        => (SettingsCard() with
        {
            Header = header,
            Description = description,
            HeaderIcon = FluentSystemIcons.Create(icon, 24),
            Content = content,
        })
            .MinWidth(0)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Set(card => card.IsEnabled = enabled);

    private static SettingsExpanderElement SettingsGroup(
        FluentSystemIcon icon,
        string header,
        string description,
        Element[] items,
        Element? headerContent = null,
        Element? itemsHeader = null,
        bool expanded = false,
        bool visible = true,
        bool enabled = true)
    {
        var group = SettingsExpander() with
        {
            Header = header,
            Description = description,
            HeaderIcon = FluentSystemIcons.Create(icon, 24),
            IsExpanded = expanded,
            Items = items,
        };
        if (headerContent is not null)
            group = group with { Content = headerContent };
        if (itemsHeader is not null)
            group = group with { ItemsHeader = itemsHeader };

        return group.MinWidth(0)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Set(expander =>
            {
                expander.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                expander.IsEnabled = enabled;
            });
    }

    private static InfoBarElement Info(string title, InfoBarSeverity severity)
        => (InfoBar() with
        {
            Title = title,
            Severity = severity,
            IsOpen = true,
            IsClosable = false,
        })
            .HorizontalAlignment(HorizontalAlignment.Stretch);

    private static TextBoxElement AppListEditor(
        IReadOnlyList<string> apps,
        string automationName,
        Action<string> setter)
        => TextBox(string.Join(Environment.NewLine, apps), setter)
            .AutomationName(automationName)
            .Height(120)
            .Set(textBox =>
            {
                textBox.AcceptsReturn = true;
                textBox.TextWrapping = TextWrapping.Wrap;
            });

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
