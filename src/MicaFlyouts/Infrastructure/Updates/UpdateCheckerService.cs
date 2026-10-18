using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MicaFlyouts.App;
using MicaFlyouts.Domain.Updates;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.State;

namespace MicaFlyouts.Infrastructure.Updates;

public sealed partial class UpdateCheckerService : IDisposable
{
    private const string ReleaseEndpoint = AppBranding.LatestReleaseApiUrl;
    private const string ApplicationFileName = "MicaFlyouts.exe";
    private const string UpdaterFileName = "Updater.exe";
    private const string BootstrapFileName = "Microsoft.WindowsAppRuntime.Bootstrap.dll";
    private const long MaximumUpdateSize = 512L * 1024 * 1024;
    private const int DownloadBufferSize = 128 * 1024;
    private static readonly string[] RequiredFiles = [ApplicationFileName, UpdaterFileName, BootstrapFileName];

    private readonly UpdateStore _store;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _downloadDirectory;
    private string? _pendingUpdatePath;
    private int _disposed;

    public UpdateCheckerService(
        UpdateStore store,
        AppLogger logger,
        HttpMessageHandler? handler = null,
        string? downloadDirectory = null)
    {
        _store = store;
        _logger = logger;
        handler ??= new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) };
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(15),
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicaFlyouts", "1.0"));

        if (downloadDirectory is not null)
        {
            _downloadDirectory = Path.GetFullPath(downloadDirectory);
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string cacheRoot = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
            _downloadDirectory = Path.Combine(cacheRoot, "MicaFlyouts", "updates");
        }
    }

    public UpdateSnapshot Snapshot => _store.Snapshot;

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return null;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        CancellationToken token = linkedCancellation.Token;
        await _checkGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                return null;

            _store.Set(_store.Snapshot with
            {
                IsChecking = true,
                IsDownloading = false,
                DownloadedBytes = 0,
                DownloadSize = null,
            });

            LatestRelease release;
            try
            {
                release = await GetLatestReleaseAsync(currentVersion, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _store.Set(_store.Snapshot with { IsChecking = false });
                throw;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Update check failed: {exception.Message}");
                _store.Set(new UpdateSnapshot(null, DateTimeOffset.UtcNow, false));
                return null;
            }

            var result = new UpdateInfo(currentVersion, release.Version, release.ReleaseUri, release.IsAvailable);
            UpdateSnapshot snapshot = _store.Snapshot with
            {
                Result = result,
                LastChecked = DateTimeOffset.UtcNow,
                IsChecking = false,
                IsDownloading = false,
                DownloadedBytes = 0,
                DownloadSize = null,
                IsReadyToInstall = false,
            };
            _store.Set(snapshot);

            if (!release.IsAvailable)
            {
                Volatile.Write(ref _pendingUpdatePath, null);
                return result;
            }

            Volatile.Write(ref _pendingUpdatePath, null);
            if (release.Asset is null)
            {
                _logger.Warn($"No verified {GetRuntimeIdentifier() ?? "supported architecture"} update asset was found for version {release.Version}.");
                return result;
            }

            try
            {
                await DownloadAndPrepareUpdateAsync(release.Asset, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _store.Set(_store.Snapshot with
                {
                    IsDownloading = false,
                    DownloadedBytes = 0,
                    DownloadSize = null,
                });
                throw;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Update download failed: {exception.Message}");
                _store.Set(_store.Snapshot with
                {
                    IsDownloading = false,
                    IsReadyToInstall = false,
                });
            }

            return result;
        }
        finally
        {
            _checkGate.Release();
        }
    }

    public bool StartPendingInstallOnExit(int parentProcessId)
    {
        string? archivePath = Volatile.Read(ref _pendingUpdatePath);
        if (archivePath is null || !File.Exists(archivePath))
            return false;

        string updaterPath = Path.Combine(AppContext.BaseDirectory, UpdaterFileName);
        if (!File.Exists(updaterPath))
        {
            _logger.Warn($"{UpdaterFileName} was not found next to MicaFlyouts; leaving the downloaded update pending.");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(updaterPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add("--from-app");
            startInfo.ArgumentList.Add("--install-update");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("--wait-pid");
            startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the MicaFlyouts updater.");
            _logger.Info($"Started {UpdaterFileName} to install the downloaded update on exit.");
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not start the updater for the pending update: {exception.Message}");
            return false;
        }
    }

    private async Task<LatestRelease> GetLatestReleaseAsync(string currentVersion, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        using HttpResponseMessage response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        string? tag = GetString(root, "tag_name");
        string? releaseUrl = GetString(root, "html_url");
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidDataException("The latest GitHub release has no tag name.");

        string latestVersion = NormalizeVersion(tag);
        bool available = CompareVersions(currentVersion, latestVersion) < 0;
        Uri? releaseUri = Uri.TryCreate(releaseUrl, UriKind.Absolute, out Uri? parsedReleaseUri)
            && IsHttpsUri(parsedReleaseUri)
            && string.Equals(parsedReleaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                ? parsedReleaseUri
                : null;
        UpdateAsset? asset = available ? FindUpdateAsset(root, latestVersion) : null;
        return new LatestRelease(latestVersion, releaseUri, available, asset);
    }

    private UpdateAsset? FindUpdateAsset(JsonElement root, string version)
    {
        string? runtimeIdentifier = GetRuntimeIdentifier();
        if (runtimeIdentifier is null
            || !root.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string expectedName = $"MicaFlyouts-{version}-{runtimeIdentifier}.zip";
        foreach (JsonElement item in assets.EnumerateArray())
        {
            if (!string.Equals(GetString(item, "name"), expectedName, StringComparison.OrdinalIgnoreCase))
                continue;

            string? url = GetString(item, "browser_download_url");
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? downloadUri)
                || !IsGitHubReleaseAssetUri(downloadUri))
            {
                _logger.Warn($"The update asset {expectedName} has an invalid download URL.");
                return null;
            }

            string? digest = GetString(item, "digest");
            if (digest is null
                || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                || !TryParseSha256(digest[7..], out byte[] expectedSha256))
            {
                _logger.Warn($"The update asset {expectedName} has no valid SHA-256 digest.");
                return null;
            }

            return new UpdateAsset(expectedName, downloadUri, expectedSha256);
        }

        return null;
    }

    private async Task DownloadAndPrepareUpdateAsync(UpdateAsset asset, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_downloadDirectory);
        string archivePath = Path.Combine(_downloadDirectory, asset.Name);
        if (File.Exists(archivePath))
        {
            if (await IsValidUpdateArchiveAsync(archivePath, asset.Sha256, cancellationToken).ConfigureAwait(false))
            {
                SetPendingUpdate(archivePath, new FileInfo(archivePath).Length, new FileInfo(archivePath).Length);
                _logger.Info($"Using the cached update package {asset.Name}.");
                return;
            }

            TryDeleteFile(archivePath);
        }

        string partialPath = archivePath + ".part";
        TryDeleteFile(partialPath);
        _store.Set(_store.Snapshot with
        {
            IsDownloading = true,
            DownloadedBytes = 0,
            DownloadSize = null,
            IsReadyToInstall = false,
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUri);
            using HttpResponseMessage response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            Uri? finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null || !IsHttpsUri(finalUri))
                throw new InvalidDataException("The update download redirected to a non-HTTPS URL.");

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaximumUpdateSize)
                throw new InvalidDataException("The update package exceeds the maximum allowed size.");

            long downloaded = 0;
            long lastReported = 0;
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                DownloadBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[DownloadBufferSize];
                while (true)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    downloaded += read;
                    if (downloaded > MaximumUpdateSize)
                        throw new InvalidDataException("The update package exceeds the maximum allowed size.");

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    if (downloaded - lastReported >= 1024 * 1024)
                    {
                        _store.Set(_store.Snapshot with
                        {
                            DownloadedBytes = downloaded,
                            DownloadSize = contentLength,
                        });
                        lastReported = downloaded;
                    }
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            if (contentLength is long expectedLength && downloaded != expectedLength)
                throw new InvalidDataException($"The update download ended at {downloaded} bytes; expected {expectedLength}.");
            if (!await IsValidUpdateArchiveAsync(partialPath, asset.Sha256, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("The downloaded update failed its SHA-256 or payload validation.");

            File.Move(partialPath, archivePath, overwrite: true);
            SetPendingUpdate(archivePath, downloaded, contentLength);
            _logger.Info($"Update package {asset.Name} downloaded and verified ({downloaded} bytes).");
        }
        catch
        {
            TryDeleteFile(partialPath);
            _store.Set(_store.Snapshot with
            {
                IsDownloading = false,
                IsReadyToInstall = false,
            });
            throw;
        }
    }

    private static async Task<bool> IsValidUpdateArchiveAsync(
        string path,
        byte[] expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length is <= 0 or > MaximumUpdateSize)
                return false;

            byte[] actualSha256 = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualSha256, expectedSha256))
                return false;

            using ZipArchive archive = ZipFile.OpenRead(path);
            return RequiredFiles.All(requiredFile => archive.Entries.Any(entry =>
                string.Equals(entry.FullName, requiredFile, StringComparison.OrdinalIgnoreCase)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void SetPendingUpdate(string archivePath, long downloadedBytes, long? totalBytes)
    {
        Volatile.Write(ref _pendingUpdatePath, archivePath);
        _store.Set(_store.Snapshot with
        {
            IsDownloading = false,
            DownloadedBytes = downloadedBytes,
            DownloadSize = totalBytes,
            IsReadyToInstall = true,
        });
    }

    private static string? GetRuntimeIdentifier() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.Arm64 => "win-arm64",
        _ => null,
    };

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static string NormalizeVersion(string value) => value.Trim().TrimStart('v', 'V');

    private static int CompareVersions(string current, string latest)
    {
        if (Version.TryParse(NormalizeVersion(current), out Version? currentVersion)
            && Version.TryParse(NormalizeVersion(latest), out Version? latestVersion))
        {
            return currentVersion.CompareTo(latestVersion);
        }

        return string.Compare(
            NormalizeVersion(current),
            NormalizeVersion(latest),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSha256(string value, out byte[] sha256)
    {
        try
        {
            sha256 = Convert.FromHexString(value);
            return sha256.Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            sha256 = [];
            return false;
        }
    }

    private static bool IsGitHubReleaseAssetUri(Uri uri)
        => IsHttpsUri(uri)
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith(
                "/luojunyuan/MicaFlyouts/releases/download/",
                StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpsUri(Uri uri)
        => uri.IsAbsoluteUri
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _lifetime.Cancel();
        _client.Dispose();
        _lifetime.Dispose();
    }

    private sealed record LatestRelease(string Version, Uri? ReleaseUri, bool IsAvailable, UpdateAsset? Asset);
    private sealed record UpdateAsset(string Name, Uri DownloadUri, byte[] Sha256);
}
