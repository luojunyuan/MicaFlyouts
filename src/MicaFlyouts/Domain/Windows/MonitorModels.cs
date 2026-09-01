namespace MicaFlyouts.Domain.Windows;

public sealed record MonitorSnapshot(
    int Index,
    string DeviceId,
    string FriendlyName,
    ScreenRect Bounds,
    ScreenRect WorkArea,
    bool IsPrimary,
    uint DpiX,
    uint DpiY)
{
    public double DpiScale => DpiX <= 0 ? 1 : DpiX / 96d;
}
