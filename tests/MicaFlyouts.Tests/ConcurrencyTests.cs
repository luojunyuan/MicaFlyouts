using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MicaFlyoutsConcurrencyTests", Guid.NewGuid().ToString("N"));

    public ConcurrencyTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task StateStore_SerializesConcurrentSubscriptionChanges()
    {
        var store = new StateStore<int>(0);
        int notifications = 0;
        const int subscriberCount = 32;

        var unsubscriptions = await Task.WhenAll(
            Enumerable.Range(0, subscriberCount).Select(_ => Task.Run(() =>
            {
                Action listener = () => Interlocked.Increment(ref notifications);
                return store.Subscribe(listener);
            })));

        store.SetSnapshot(1);
        Assert.Equal(subscriberCount, notifications);

        await Task.WhenAll(unsubscriptions.Select(unsubscribe => Task.Run(unsubscribe)));
        store.SetSnapshot(2);
        Assert.Equal(subscriberCount, notifications);

        store.Dispose();
    }

    [Fact]
    public async Task JsonRepository_SerializesConcurrentAtomicSavesAndLoads()
    {
        var repository = new JsonSettingsRepository(Path.Combine(_directory, "settings.json"));
        var writers = Enumerable.Range(0, 4).Select(writer => Task.Run(async () =>
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                await repository.SaveAsync(
                    SettingsSnapshot.CreateDefault(Guid.NewGuid()) with { Duration = writer * 100 + iteration });
            }
        }));
        var readers = Enumerable.Range(0, 4).Select(reader => Task.Run(() =>
        {
            for (int iteration = 0; iteration < 20; iteration++)
                repository.Load();
        }));

        await Task.WhenAll(writers.Concat(readers));

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(repository.SettingsPath));
        Assert.True(document.RootElement.TryGetProperty("duration", out _));
        Assert.False(File.Exists(repository.SettingsPath + ".tmp"));
    }

    [Fact]
    public async Task AppLogger_SerializesConcurrentFileAppends()
    {
        using var logger = new AppLogger(_directory);
        const int writerCount = 8;
        const int messagesPerWriter = 25;

        await Task.WhenAll(Enumerable.Range(0, writerCount).Select(writer => Task.Run(() =>
        {
            for (int message = 0; message < messagesPerWriter; message++)
                logger.Info($"writer={writer};message={message}");
        })));

        string logPath = Directory.GetFiles(_directory, "mica-*.log").Single();
        var lines = File.ReadAllLines(logPath);
        Assert.Equal(writerCount * messagesPerWriter, lines.Length);
        Assert.All(lines, line => Assert.Contains("[INFO]", line, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
