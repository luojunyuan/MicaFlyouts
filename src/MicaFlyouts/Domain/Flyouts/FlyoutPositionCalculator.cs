namespace MicaFlyouts.Domain.Flyouts;

public enum FlyoutPosition
{
    BottomLeft,
    BottomCenter,
    BottomRight,
    TopLeft,
    TopCenter,
    TopRight,
}

public readonly record struct WindowPosition(double Left, double Top);

public static class FlyoutPositionCalculator
{
    public static double GetBottomCenterMargin(bool reserveNativeVolumeOsdSpace, bool volumeAboveMediaFlyout)
    {
        if (!reserveNativeVolumeOsdSpace || (volumeAboveMediaFlyout && reserveNativeVolumeOsdSpace))
            return 16;

        return 80;
    }

    public static WindowPosition Calculate(
        FlyoutPosition position,
        ScreenRect window,
        ScreenRect workArea,
        bool reserveNativeVolumeOsdSpace = false,
        bool volumeAboveMediaFlyout = false)
    {
        double left = position switch
        {
            FlyoutPosition.BottomLeft or FlyoutPosition.TopLeft => workArea.Left + 16,
            FlyoutPosition.BottomRight or FlyoutPosition.TopRight => workArea.Right - window.Width - 16,
            _ => workArea.Left + (workArea.Width - window.Width) / 2,
        };

        double bottomMargin = GetBottomCenterMargin(reserveNativeVolumeOsdSpace, volumeAboveMediaFlyout);
        double top = position switch
        {
            FlyoutPosition.BottomLeft or FlyoutPosition.BottomRight => workArea.Bottom - window.Height - 16,
            FlyoutPosition.BottomCenter => workArea.Bottom - window.Height - bottomMargin,
            _ => workArea.Top + 16,
        };

        return new WindowPosition(left, top);
    }

    public static WindowPosition CalculateAboveReference(
        FlyoutPosition position,
        ScreenRect reference,
        ScreenRect window,
        ScreenRect workArea,
        bool reserveNativeVolumeOsdSpace = false,
        bool volumeAboveMediaFlyout = false)
    {
        var referencePosition = Calculate(position, reference, workArea, reserveNativeVolumeOsdSpace, volumeAboveMediaFlyout);
        bool topPlacement = position is FlyoutPosition.TopLeft or FlyoutPosition.TopCenter or FlyoutPosition.TopRight;
        double top = topPlacement
            ? referencePosition.Top + reference.Height + 8
            : referencePosition.Top - window.Height - 8;

        return new WindowPosition(
            referencePosition.Left + (reference.Width - window.Width) / 2,
            top);
    }
}
