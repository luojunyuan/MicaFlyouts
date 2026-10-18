using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Updates;
using MicaFlyouts.Infrastructure.State;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed class UpdateCheckerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MicaFlyoutsUpdateTests", Guid.NewGuid().ToString("N"));

    public UpdateCheckerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task CheckAsync_DownloadsAndVerifiesTheMatchingArchitectureAsset()
    {
        byte[] archive = CreateUpdateArchive();
        string digest = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        int downloadRequests = 0;
        using var store = new UpdateStore();
        using var service = CreateService(store, CreateHandler(archive, digest, () => downloadRequests++));

        var result = await service.CheckAsync("215.0.0");

        Assert.True(result?.IsAvailable);
        Assert.Equal("215.0.1", result?.LatestVersion);
        Assert.Equal(1, downloadRequests);
        Assert.True(store.Snapshot.IsReadyToInstall);
        Assert.False(store.Snapshot.IsDownloading);
        Assert.Equal(archive.Length, store.Snapshot.DownloadedBytes);
        Assert.Equal(archive.Length, store.Snapshot.DownloadSize);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "cache"), "*.zip"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "cache"), "*.part"));
    }

    [Fact]
    public async Task CheckAsync_RejectsAnAssetWithTheWrongDigest()
    {
        byte[] archive = CreateUpdateArchive();
        string wrongDigest = new('0', SHA256.HashSizeInBytes * 2);
        int downloadRequests = 0;
        using var store = new UpdateStore();
        using var service = CreateService(store, CreateHandler(archive, wrongDigest, () => downloadRequests++));

        var result = await service.CheckAsync("215.0.0");

        Assert.True(result?.IsAvailable);
        Assert.Equal(1, downloadRequests);
        Assert.False(store.Snapshot.IsReadyToInstall);
        Assert.False(store.Snapshot.IsDownloading);
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "cache"), "*.zip"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "cache"), "*.part"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private UpdateCheckerService CreateService(UpdateStore store, HttpMessageHandler handler)
        => new(
            store,
            new AppLogger(Path.Combine(_directory, "logs")),
            handler,
            Path.Combine(_directory, "cache"));

    private static DelegateHttpMessageHandler CreateHandler(byte[] archive, string digest, Action onDownload)
    {
        string runtimeIdentifier = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            var architecture => throw new PlatformNotSupportedException($"Test host architecture {architecture} is not supported."),
        };
        string assetName = $"MicaFlyouts-215.0.1-{runtimeIdentifier}.zip";
        string assetUrl = $"https://github.com/luojunyuan/MicaFlyouts/releases/download/v215.0.1/{assetName}";
        string releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v215.0.1",
            html_url = "https://github.com/luojunyuan/MicaFlyouts/releases/tag/v215.0.1",
            assets = new[]
            {
                new
                {
                    name = assetName,
                    browser_download_url = assetUrl,
                    digest = $"sha256:{digest}",
                },
            },
        });

        return new DelegateHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/releases/latest", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(releaseJson, Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                };
            }

            onDownload();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(archive),
                RequestMessage = request,
            };
        });
    }

    private static byte[] CreateUpdateArchive()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string fileName in new[]
            {
                "MicaFlyouts.exe",
                "Updater.exe",
                "Microsoft.WindowsAppRuntime.Bootstrap.dll",
            })
            {
                using Stream entry = archive.CreateEntry(fileName).Open();
                entry.Write("payload"u8);
            }
        }

        return stream.ToArray();
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request, cancellationToken));
    }
}
