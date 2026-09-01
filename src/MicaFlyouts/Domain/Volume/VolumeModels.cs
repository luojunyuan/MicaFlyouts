namespace MicaFlyouts.Domain.Volume;

public sealed record ApplicationVolumeSnapshot(
    string SessionId,
    string DisplayName,
    string? IconPath,
    float Volume,
    bool IsMuted,
    bool IsActive);

public sealed record VolumeSnapshot(
    string? DefaultDeviceId,
    string DefaultDeviceName,
    float MasterVolume,
    bool IsMasterMuted,
    IReadOnlyList<ApplicationVolumeSnapshot> Applications,
    bool IsNativeOsdSuppressed)
{
    public static VolumeSnapshot Empty { get; } = new(null, "", 1, false, Array.Empty<ApplicationVolumeSnapshot>(), false);
}

public enum VolumeCommand
{
    SetMasterVolume,
    ToggleMasterMute,
    SetApplicationVolume,
    ToggleApplicationMute,
}
