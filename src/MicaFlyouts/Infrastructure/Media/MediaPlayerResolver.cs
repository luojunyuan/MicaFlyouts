using System.Collections.Concurrent;
using System.Diagnostics;
using MicaFlyouts.Infrastructure.Interop;
using MicaFlyouts.Infrastructure.Logging;

namespace MicaFlyouts.Infrastructure.Media;

public sealed record MediaPlayerInfo(
    string DisplayName,
    string? ExecutablePath,
    int ProcessId);

public sealed class MediaPlayerResolver
{
    private readonly ConcurrentDictionary<string, MediaPlayerInfo> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppLogger _logger;
    private DateTimeOffset _lastRefresh;
    private Process[] _processes = [];

    public MediaPlayerResolver(AppLogger logger)
    {
        _logger = logger;
    }

    public MediaPlayerInfo Resolve(string? appUserModelId)
    {
        string id = string.IsNullOrWhiteSpace(appUserModelId) ? "Media player" : appUserModelId;
        if (_cache.TryGetValue(id, out var cached))
            return cached;

        RefreshProcessCache();
        var variants = BuildVariants(id);
        var match = _processes
            .Select(process => TryDescribe(process, variants))
            .Where(static value => value is not null)
            .Select(static value => value!)
            .OrderByDescending(value => variants.Contains(value.DisplayName, StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault();

        var result = match ?? new MediaPlayerInfo(id, null, -1);
        _cache[id] = result;
        return result;
    }

    public bool TryActivate(string? appUserModelId, string? mediaTitle = null)
    {
        var player = Resolve(appUserModelId);
        try
        {
            if (player.ProcessId > 0)
            {
                using var process = Process.GetProcessById(player.ProcessId);
                var hwnd = process.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    if (NativeWindowApi.IsMinimized(hwnd))
                        NativeWindowApi.SetVisible(hwnd, true, activate: false);
                    return NativeWindowApi.SetForeground(hwnd);
                }
            }

            if (!string.IsNullOrWhiteSpace(player.ExecutablePath))
            {
                _ = Process.Start(new ProcessStartInfo(player.ExecutablePath) { UseShellExecute = true });
                return true;
            }
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not activate media player '{player.DisplayName}': {exception.Message}");
        }
        return false;
    }

    private void RefreshProcessCache()
    {
        if (DateTimeOffset.UtcNow - _lastRefresh < TimeSpan.FromSeconds(5))
            return;
        try
        {
            _processes = Process.GetProcesses();
            _lastRefresh = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not enumerate media players: {exception.Message}");
        }
    }

    private static string[] BuildVariants(string id)
        => id.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value
                .Replace("com", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("github", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("exe", string.Empty, StringComparison.OrdinalIgnoreCase))
            .Append(id)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static MediaPlayerInfo? TryDescribe(Process process, IReadOnlyList<string> variants)
    {
        try
        {
            string processName = process.ProcessName;
            string? path = process.MainModule?.FileName;
            if (path is null || (!variants.Any(variant => processName.Contains(variant, StringComparison.OrdinalIgnoreCase))
                && !variants.Any(variant => path.Contains(variant, StringComparison.OrdinalIgnoreCase))))
                return null;

            string description = process.MainModule?.FileVersionInfo.FileDescription ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description))
                description = process.MainWindowTitle;
            if (string.IsNullOrWhiteSpace(description))
                description = processName;
            return new MediaPlayerInfo(description, path, process.Id);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
