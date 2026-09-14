using MicaFlyouts.App;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Features.Tray;
using MicaFlyouts.Infrastructure.Localization;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using Microsoft.UI.Reactor.Core;
using Xunit;

namespace MicaFlyouts.ReactorSelfTests;

public sealed partial class TrayPresentationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MicaFlyoutsTrayTests", Guid.NewGuid().ToString("N"));
    private readonly SettingsStore _settings;
    private readonly LocalizationStore _localizationStore = new("en-US");
    private readonly LocalizationService _localization;

    public TrayPresentationTests()
    {
        _settings = new SettingsStore(new JsonSettingsRepository(Path.Combine(_directory, "settings.json")));
        _settings.Update(settings => settings with { AppLanguage = "en-US" }, save: false);
        _localization = new LocalizationService(_settings, _localizationStore);
    }

    [Theory]
    [InlineData(false, false, TrayIconAssets.ColorResource)]
    [InlineData(false, true, TrayIconAssets.ColorResource)]
    [InlineData(true, false, TrayIconAssets.WhiteResource)]
    [InlineData(true, true, TrayIconAssets.BlackResource)]
    public void TrayIcon_UsesWindowsThemeOnlyForSymbolMode(bool useSymbol, bool systemUsesLightTheme, string expectedResource)
    {
        Assert.Equal(expectedResource, TrayIconAssets.SelectResource(useSymbol, systemUsesLightTheme));
        using var icon = TrayIconAssets.Load(expectedResource);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
        Assert.NotEqual(IntPtr.Zero, icon.Handle);
    }

    [Fact]
    public void TrayMenu_MatchesUpstreamOrderIconsAndCommands()
    {
        List<string> invoked = [];
        var menu = TrayMenu.Create(_localization,
            () => invoked.Add("settings"),
            () => invoked.Add("repository"),
            () => invoked.Add("logs"),
            () => invoked.Add("report"),
            () => invoked.Add("exit"));

        Assert.Equal(7, menu.Length);
        Assert.IsType<MenuFlyoutSeparatorData>(menu[1]);
        Assert.IsType<MenuFlyoutSeparatorData>(menu[5]);
        MenuFlyoutItemData[] items = [.. menu.OfType<MenuFlyoutItemData>()];
        Assert.Equal(["Settings", "Repository", "View logs", "Report bug", "Quit Mica Flyouts"], items.Select(item => item.Text));
        Assert.Equal(5, items.Select(item => item.Icon).Distinct(StringComparer.Ordinal).Count());
        foreach (var item in items)
        {
            Assert.StartsWith("path:F1 M", item.Icon);
            Assert.IsType<PathIconData>(item.IconElement);
            Assert.NotNull(item.OnClick);
            item.OnClick();
        }
        Assert.Equal(["settings", "repository", "logs", "report", "exit"], invoked);
    }

    [Fact]
    public void TrayMenu_RefreshesFromLocalizationStoreWithoutRestart()
    {
        var menu = CreateMenu();
        Assert.Equal("Settings", Assert.IsType<MenuFlyoutItemData>(menu[0]).Text);
        var unsubscribe = _localizationStore.Subscribe(() => menu = CreateMenu());
        try
        {
            _settings.Update(settings => settings with { AppLanguage = "zh-CN" }, save: false);
            _localization.Apply();
            Assert.Equal("设置", Assert.IsType<MenuFlyoutItemData>(menu[0]).Text);
            Assert.Equal("查看日志", Assert.IsType<MenuFlyoutItemData>(menu[3]).Text);
            Assert.Equal("退出 Mica Flyouts", Assert.IsType<MenuFlyoutItemData>(menu[6]).Text);

            _settings.Update(settings => settings with { AppLanguage = "ar" }, save: false);
            _localization.Apply();
            Assert.True(_localization.Snapshot.IsRightToLeft);
            Assert.Contains("Mica Flyouts", Assert.IsType<MenuFlyoutItemData>(menu[6]).Text);
        }
        finally
        {
            unsubscribe();
        }
    }

    [Fact]
    public void TrayMenu_HasResourcesForEverySupportedLanguage()
    {
        string[] keys =
        [
            "TrayIcon_SettingsOption",
            "TrayIcon_GitHubRepositoryOption",
            "TrayIcon_ViewLogsOption",
            "TrayIcon_ReportBugOption",
            "TrayIcon_QuitOption",
        ];
        foreach (var language in LocalizationCatalog.SupportedLanguages)
        {
            _settings.Update(settings => settings with { AppLanguage = language }, save: false);
            _localization.Apply();
            var items = CreateMenu().OfType<MenuFlyoutItemData>().ToArray();
            for (int index = 0; index < keys.Length; index++)
            {
                string? resource = _localization.ResourceProvider.GetString(language, "App", keys[index]);
                Assert.False(string.IsNullOrWhiteSpace(resource), $"Missing {keys[index]} in {language}.");
                Assert.Equal(resource.Replace("{appName}", "Mica Flyouts", StringComparison.Ordinal), items[index].Text);
                Assert.DoesNotContain("FluentFlyout", items[index].Text);
                Assert.DoesNotContain("{appName}", items[index].Text);
            }
        }
    }

    [Fact]
    public void TrayMenu_FallsBackToEnglishWhenResourcesAreMissing()
    {
        var missingResources = new LocalizationService(_settings, _localizationStore, Path.Combine(_directory, "missing"));
        var items = TrayMenu.Create(missingResources, NoOp, NoOp, NoOp, NoOp, NoOp).OfType<MenuFlyoutItemData>();
        Assert.Equal(["Settings", "Repository", "View logs", "Report bug", "Quit Mica Flyouts"], items.Select(item => item.Text));
    }

    private MenuFlyoutItemBase[] CreateMenu() => TrayMenu.Create(_localization, NoOp, NoOp, NoOp, NoOp, NoOp);

    private static void NoOp() { }

    public void Dispose()
    {
        _settings.Dispose();
        _localizationStore.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
