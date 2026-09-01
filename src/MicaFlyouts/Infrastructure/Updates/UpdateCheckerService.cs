using System.Net.Http.Headers;
using System.Text.Json;
using MicaFlyouts.Domain.Updates;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.State;

namespace MicaFlyouts.Infrastructure.Updates;

public sealed class UpdateCheckerService : IDisposable
{
    private const string ReleaseEndpoint = "https://api.github.com/repos/unchihugo/FluentFlyout/releases/latest";
    private readonly UpdateStore _store;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;
    private int _disposed;

    public UpdateCheckerService(UpdateStore store, AppLogger logger)
    {
        _store = store;
        _logger = logger;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicaFlyouts", "1.0"));
    }

    public UpdateSnapshot Snapshot => _store.Snapshot;

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return null;
        _store.Set(new UpdateSnapshot(_store.Snapshot.Result, DateTimeOffset.UtcNow, true));
        try
        {
            using var response = await _client.GetAsync(ReleaseEndpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            string? tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            string? url = root.TryGetProperty("html_url", out var urlValue) ? urlValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
                return null;
            string latest = tag.TrimStart('v', 'V');
            bool available = CompareVersions(currentVersion, latest) < 0;
            var result = new UpdateInfo(currentVersion, latest, Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null, available);
            _store.Set(new UpdateSnapshot(result, DateTimeOffset.UtcNow, false));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Update check failed: {exception.Message}");
            _store.Set(new UpdateSnapshot(null, DateTimeOffset.UtcNow, false));
            return null;
        }
    }

    private static int CompareVersions(string current, string latest)
    {
        static string Clean(string value) => value.Trim().TrimStart('v', 'V');
        if (Version.TryParse(Clean(current), out var currentVersion)
            && Version.TryParse(Clean(latest), out var latestVersion))
            return currentVersion.CompareTo(latestVersion);
        return string.Compare(Clean(current), Clean(latest), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _client.Dispose();
    }
}
