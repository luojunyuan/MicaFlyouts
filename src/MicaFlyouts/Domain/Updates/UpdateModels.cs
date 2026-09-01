namespace MicaFlyouts.Domain.Updates;

public sealed record UpdateInfo(string CurrentVersion, string? LatestVersion, Uri? ReleaseUri, bool IsAvailable);

public sealed record UpdateSnapshot(UpdateInfo? Result, DateTimeOffset? LastChecked, bool IsChecking)
{
    public static UpdateSnapshot Empty { get; } = new(null, null, false);
}
