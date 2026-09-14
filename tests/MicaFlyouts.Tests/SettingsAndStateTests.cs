using System.Globalization;
using System.Text.Json;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Features.Settings;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed partial class SettingsAndStateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MicaFlyoutsTests", Guid.NewGuid().ToString("N"));

    public SettingsAndStateTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Defaults_MatchBaselineValues()
    {
        var settings = SettingsSnapshot.CreateDefault(Guid.Empty);
        Assert.False(settings.CompactLayout);
        Assert.Equal(2, settings.FlyoutAnimationSpeed);
        Assert.Equal(3000, settings.Duration);
        Assert.Equal(2000, settings.NextUpDuration);
        Assert.True(settings.LockKeysEnabled);
        Assert.True(settings.TaskbarWidgetPadding);
        Assert.Equal(10, settings.TaskbarVisualizerBarCount);
        Assert.Equal(175u, settings.AcrylicBlurOpacity);
        Assert.True(settings.AnonymousTelemetryAllowed);
        Assert.Equal(LocalizationCatalog.SystemLanguage, settings.AppLanguage);
    }

    [Fact]
    public void Normalize_ClampsRangesAndCleansLists()
    {
        var normalized = SettingsValidator.Normalize(new SettingsSnapshot
        {
            Position = 99,
            Duration = -1,
            TaskbarWidgetManualPadding = 50000,
            TaskbarWidgetScrollingTextSpeed = 0,
            TaskbarVisualizerAudioSensitivity = 10,
            TaskbarVisualizerAudioPeakLevel = 0,
            AppFilteringMode = 99,
            AllowedApps = ["  Music  ", "music", "", "  "],
        });

        Assert.Equal(5, normalized.Position);
        Assert.Equal(0, normalized.Duration);
        Assert.Equal(9999, normalized.TaskbarWidgetManualPadding);
        Assert.Equal(1, normalized.TaskbarWidgetScrollingTextSpeed);
        Assert.Equal(3, normalized.TaskbarVisualizerAudioSensitivity);
        Assert.Equal(1, normalized.TaskbarVisualizerAudioPeakLevel);
        Assert.Equal(1, normalized.AppFilteringMode);
        Assert.Single(normalized.AllowedApps);
        Assert.Equal("Music", normalized.AllowedApps[0]);
    }

    [Fact]
    public void Normalize_LanguageSelectionsAreCanonicalAndInvalidValuesUseEnglish()
    {
        Assert.Equal(
            LocalizationCatalog.SystemLanguage,
            SettingsValidator.Normalize(new SettingsSnapshot { AppLanguage = " SYSTEM " }).AppLanguage);
        Assert.Equal(
            "en-US",
            SettingsValidator.Normalize(new SettingsSnapshot { AppLanguage = "EN-us" }).AppLanguage);
        Assert.Equal(
            "ru",
            SettingsValidator.Normalize(new SettingsSnapshot { AppLanguage = "ru-RU" }).AppLanguage);
        Assert.Equal(
            LocalizationCatalog.DefaultLanguage,
            SettingsValidator.Normalize(new SettingsSnapshot { AppLanguage = "not-a-language" }).AppLanguage);
        Assert.Equal(
            LocalizationCatalog.DefaultLanguage,
            LocalizationCatalog.Resolve("not-a-language", "ru-RU"));
    }

    [Fact]
    public void LanguageOptions_PreferNativeNamesAndKeepValuesSeparate()
    {
        var options = LocalizationCatalog.CreateLanguageOptions("System");
        var english = Assert.Single(options, option => option.Value == "en-US");
        var englishCulture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("System", options[0].DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(englishCulture.NativeName));
        Assert.Equal(englishCulture.NativeName, english.DisplayName);
        Assert.NotEqual(english.Value, english.DisplayName);
        Assert.Equal(LocalizationCatalog.SystemLanguage, options[0].Value);
        Assert.Contains(options, option => option.Value == "zh-CN");

        foreach (var language in LocalizationCatalog.SupportedLanguages)
        {
            var culture = CultureInfo.GetCultureInfo(language);
            var expected = string.IsNullOrWhiteSpace(culture.NativeName)
                ? culture.DisplayName
                : culture.NativeName;
            Assert.Equal(expected, Assert.Single(options, option => option.Value == language).DisplayName);
        }
    }

    [Fact]
    public void LanguageOptions_IgnoreTransientSelectionIndices()
    {
        var options = LocalizationCatalog.CreateLanguageOptions("System");

        Assert.False(LocalizationCatalog.TryGetLanguageValue(options, -1, out _));
        Assert.False(LocalizationCatalog.TryGetLanguageValue(options, options.Count, out _));

        int index = LocalizationCatalog.IndexOfLanguage(options, "zh-CN");
        Assert.True(LocalizationCatalog.TryGetLanguageValue(options, index, out var language));
        Assert.Equal("zh-CN", language);
    }

    [Fact]
    public void Resolve_SystemUsesTheProvidedCurrentUiCultureWithRegionFallback()
    {
        Assert.Equal("ru", LocalizationCatalog.Resolve("system", CultureInfo.GetCultureInfo("ru-RU").Name));
        Assert.Equal("zh-CN", LocalizationCatalog.Resolve("SYSTEM", CultureInfo.GetCultureInfo("zh-CN").Name));
        Assert.Equal(LocalizationCatalog.DefaultLanguage, LocalizationCatalog.Resolve("system", "not-a-language"));
    }

    [Fact]
    public void SettingsContentProps_CarrySelectedPageAcrossLocalizedRootUpdates()
    {
        var setPage = static (SettingsPage _) => { };
        var firstRender = new SettingsContentProps(SettingsPage.System, setPage);
        var localizedRerender = new SettingsContentProps(firstRender.Page, firstRender.SetPage);

        Assert.Equal(SettingsPage.System, localizedRerender.Page);
        Assert.Same(setPage, localizedRerender.SetPage);
    }

    [Fact]
    public void JsonRepository_UsesDefaultsForMissingAndIgnoresUnknownFields()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"schemaVersion\":1,\"duration\":123,\"unknownField\":true}");
        var loaded = new JsonSettingsRepository(path).Load();
        Assert.Equal(123, loaded.Duration);
        Assert.Equal(2, loaded.FlyoutAnimationSpeed);
        Assert.True(loaded.PlayerInfoEnabled);
        Assert.Equal(LocalizationCatalog.SystemLanguage, loaded.AppLanguage);
    }

    [Fact]
    public void JsonRepository_NormalizesLegacyLanguageCasingAndPersistsSystemValue()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"AppLanguage\":\"EN-us\"}");

        var repository = new JsonSettingsRepository(path);
        Assert.Equal("en-US", repository.Load().AppLanguage);

        repository.Save(SettingsSnapshot.CreateDefault(Guid.NewGuid()) with { AppLanguage = "SYSTEM" });
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(LocalizationCatalog.SystemLanguage, document.RootElement.GetProperty("appLanguage").GetString());
        Assert.Equal(LocalizationCatalog.SystemLanguage, repository.Load().AppLanguage);
    }

    [Fact]
    public void JsonRepository_SaveKeepsBackupAndRemovesTemporaryFile()
    {
        var path = Path.Combine(_directory, "settings.json");
        var repository = new JsonSettingsRepository(path);
        repository.Save(SettingsSnapshot.CreateDefault(Guid.NewGuid()) with { Duration = 100 });
        repository.Save(SettingsSnapshot.CreateDefault(Guid.NewGuid()) with { Duration = 200 });
        Assert.Equal(200, repository.Load().Duration);
        Assert.True(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void ExportAndImport_OmitUuidAndPreserveCurrentUuid()
    {
        var currentUuid = Guid.NewGuid();
        var repository = new JsonSettingsRepository(Path.Combine(_directory, "settings.json"));
        var original = SettingsSnapshot.CreateDefault(Guid.NewGuid()) with { Duration = 456 };
        var exported = repository.Export(original);
        Assert.DoesNotContain("uuid", exported, StringComparison.OrdinalIgnoreCase);
        Assert.True(repository.TryImport(exported, currentUuid, out var imported));
        Assert.Equal(currentUuid, imported.Uuid);
        Assert.Equal(456, imported.Duration);
    }

    [Fact]
    public void StateStore_UsesCopyOnWriteSubscriptions()
    {
        var store = new StateStore<int>(1);
        int notifications = 0;
        var unsubscribe = store.Subscribe(() => notifications++);
        store.SetSnapshot(2);
        unsubscribe();
        unsubscribe();
        store.SetSnapshot(3);
        Assert.Equal(3, store.Snapshot);
        Assert.Equal(1, notifications);
        store.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store.SetSnapshot(4));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
