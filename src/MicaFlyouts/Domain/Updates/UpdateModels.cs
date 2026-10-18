namespace MicaFlyouts.Domain.Updates;

public sealed record UpdateInfo(string CurrentVersion, string? LatestVersion, Uri? ReleaseUri, bool IsAvailable);

public sealed record UpdateSnapshot(UpdateInfo? Result, DateTimeOffset? LastChecked, bool IsChecking)
{
    public bool IsDownloading { get; init; }
    public long DownloadedBytes { get; init; }
    public long? DownloadSize { get; init; }
    public bool IsReadyToInstall { get; init; }

    public static UpdateSnapshot Empty { get; } = new(null, null, false);
}
