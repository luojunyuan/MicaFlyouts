using System.Text.Json;
using MicaFlyouts.Domain.Settings;
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
    public void JsonRepository_UsesDefaultsForMissingAndIgnoresUnknownFields()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"schemaVersion\":1,\"duration\":123,\"unknownField\":true}");
        var loaded = new JsonSettingsRepository(path).Load();
        Assert.Equal(123, loaded.Duration);
        Assert.Equal(2, loaded.FlyoutAnimationSpeed);
        Assert.True(loaded.PlayerInfoEnabled);
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
