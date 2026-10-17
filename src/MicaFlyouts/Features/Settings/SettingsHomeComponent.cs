using System.Diagnostics;
using System.Globalization;
using MicaFlyouts.App;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Domain.Updates;
using MicaFlyouts.UI.Toolkit;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;
using static MicaFlyouts.UI.Toolkit.SettingsCardElement;

namespace MicaFlyouts.Features.Settings;

public sealed record SettingsHomeProps(SettingsSnapshot Settings, Action<SettingsPage> SetPage);

internal sealed class SettingsHomeComponent : Component<SettingsHomeProps>
{
    private static readonly Uri ReleasesUri = new(AppBranding.ReleasesUrl);

    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var update = UseExternalStore(services.UpdateStore.Subscribe, () => services.UpdateStore.Snapshot);
        var version = AppBranding.Version;

        return ScrollViewer(VStack(20,
                HStack(10, Heading(AppBranding.Name), Caption(version).Opacity(0.6)),
                Heading(t.Message(Loc.App.HomeTitle)),
                HStack(12,
                    RenderUpdatesButton(t),
                    RenderUpdateStatus(services, Props.Settings, update, t)),
                VStack(8,
                    TextBlock(t.Message(Loc.App.HomeDashboardTitle)).FontSize(14).SemiBold(),
                    RenderDashboard(Props.Settings, Props.SetPage, t)),
                RenderFooter(services, t)))
            .Set(scrollViewer =>
            {
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            })
            .Padding(24);
    }

    private static ButtonElement RenderUpdatesButton(IntlAccessor t)
        => Button(HStack(16,
                Border((Image("ms-appx:///Assets/FluentFlyoutHero-NoTitle.png") with
                    { Stretch = "UniformToFill" })
                    .Width(200)
                    .Height(98))
                    .CornerRadius(8),
                VStack(4,
                    TextBlock(t.Message(Loc.App.UpdateToastButton)).SemiBold(),
                    Caption(t.Message(Loc.App.HomeUpdateDescription)).Opacity(0.6))),
                () => OpenUri(ReleasesUri))
            .SubtleButton()
            .Set(button =>
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.VerticalContentAlignment = VerticalAlignment.Center;
            })
            .AutomationName(t.Message(Loc.App.UpdateToastButton));

    private static ButtonElement RenderUpdateStatus(
        AppServices services,
        SettingsSnapshot settings,
        UpdateSnapshot update,
        IntlAccessor t)
    {
        bool isAvailable = update.Result?.IsAvailable == true;
        string title = update.IsChecking
            ? t.Message(Loc.App.CheckingForUpdates)
            : isAvailable
                ? t.Message(Loc.App.UpdateAvailable)
                : update.Result is null
                    ? t.Message(Loc.App.CheckForUpdates)
                    : t.Message(Loc.App.UpToDate);
        var culture = CultureInfo.GetCultureInfo(t.Locale);
        string subtitle = update.LastChecked is { } checkedAt
            ? string.Format(culture, t.Message(Loc.App.LastChecked), checkedAt.ToLocalTime().ToString("g", culture))
            : string.Empty;
        string icon = update.IsChecking
            ? "Refresh"
            : isAvailable
                ? "Download"
                : update.Result is null
                    ? "Refresh"
                    : "Accept";
        Action onClick = isAvailable && update.Result?.ReleaseUri is { } releaseUri
            ? () => OpenUri(releaseUri)
            : () => _ = services.Updater.CheckAsync(AppBranding.Version);

        return Button(HStack(12,
                    Icon(SymbolIcon(icon))
                        .Width(24)
                        .Height(24),
                    VStack(4,
                        TextBlock(title)
                            .SemiBold()
                            .Foreground(isAvailable ? Accent : PrimaryText),
                        string.IsNullOrEmpty(subtitle)
                            ? Empty()
                            : Caption(subtitle).Opacity(0.6))),
                onClick)
            .SubtleButton()
            .Set(button =>
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.VerticalContentAlignment = VerticalAlignment.Center;
            })
            .AutomationName(title);
    }

    private static GridElement RenderDashboard(
        SettingsSnapshot settings,
        Action<SettingsPage> setPage,
        IntlAccessor t)
        => Grid(
            columns: [GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Auto],
            DashboardCard(FluentSystemIcon.VideoPlayPause24, t.Message(Loc.App.MediaFlyoutTitle), settings.MediaFlyoutEnabled,
                SettingsPage.Media, setPage, t).Margin(4).Grid(row: 0, column: 0),
            DashboardCard(FluentSystemIcon.TextboxAlignBottom24, t.Message(Loc.App.TaskbarWidgetCustomizationTitle), settings.TaskbarWidgetEnabled,
                SettingsPage.Taskbar, setPage, t).Margin(4).Grid(row: 0, column: 1),
            DashboardCard(FluentSystemIcon.Speaker224, t.Message(Loc.App.VolumeFlyoutTitle), settings.VolumeControlEnabled,
                SettingsPage.Volume, setPage, t).Margin(4).Grid(row: 1, column: 0),
            DashboardCard(FluentSystemIcon.SoundWaveCircle24, t.Message(Loc.App.TaskbarVisualizerTitle), settings.TaskbarVisualizerEnabled,
                SettingsPage.Visualizer, setPage, t).Margin(4).Grid(row: 1, column: 1),
            DashboardCard(FluentSystemIcon.MusicNote2Play20, t.Message(Loc.App.NextUpCustomizationTitle), settings.NextUpEnabled,
                SettingsPage.NextUp, setPage, t).Margin(4).Grid(row: 2, column: 0),
            DashboardCard(FluentSystemIcon.LockMultiple24, t.Message(Loc.App.LockKeysCustomizationTitle), settings.LockKeysEnabled,
                SettingsPage.LockKeys, setPage, t).Margin(4).Grid(row: 2, column: 1),
            DashboardCard(FluentSystemIcon.Settings24, t.Message(Loc.App.SystemSettingsTitle), t.Message(Loc.App.SystemConfigure),
                SettingsPage.System, setPage).Margin(4).Grid(row: 3, column: 0))
            .Margin(-4);

    private static SettingsCardElement DashboardCard(
        FluentSystemIcon icon,
        string title,
        bool enabled,
        SettingsPage page,
        Action<SettingsPage> setPage,
        IntlAccessor t)
        => DashboardCard(icon, title,
            t.Message(enabled ? Loc.App.Enabled : Loc.App.Disabled),
            page,
            setPage);

    private static SettingsCardElement DashboardCard(
        FluentSystemIcon icon,
        string title,
        string status,
        SettingsPage page,
        Action<SettingsPage> setPage)
        => (SettingsCard() with
        {
            Header = title,
            Description = status,
            HeaderIcon = FluentSystemIcons.Create(icon, 24),
            IsClickEnabled = true,
            OnClick = () => setPage(page),
        })
            .MinWidth(0)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .AutomationName(title);

    private static StackElement RenderFooter(AppServices services, IntlAccessor t)
        => HStack(12,
            HyperlinkButton(
                t.Message(Loc.App.GitHubRepoLink),
                new Uri(AppBranding.RepositoryUrl)),
            HyperlinkButton(
                t.Message(Loc.App.TrayIcon_ReportBugOption),
                new Uri(AppBranding.IssuesUrl)),
            Button(HStack(6,
                    Icon(SymbolIcon("OpenFile")).Width(16).Height(16),
                    TextBlock(t.Message(Loc.App.TrayIcon_ViewLogsOption))),
                () => OpenFolder(services.Logger.LogDirectory))
                .SubtleButton()
                .AutomationName(t.Message(Loc.App.TrayIcon_ViewLogsOption)));

    private static void OpenUri(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }
        catch
        {
        }
    }
}
