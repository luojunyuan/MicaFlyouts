using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Settings;

namespace MicaFlyouts.Domain.Flyouts;

public static class FlyoutWindowDimensions
{
    public const double MediaFlyoutWidthDip = 310;
    public const double MediaFlyoutHeightDip = 112;
    public const double CompactMediaFlyoutWidthDip = 400;
    public const double CompactMediaFlyoutHeightDip = 60;

    public static (double WidthDip, double HeightDip) GetMediaFlyoutSizeDip(
        SettingsSnapshot settings,
        MediaTrackSnapshot? track)
    {
        bool showSeekbar = settings.SeekbarEnabled
            && track?.Capabilities.CanSeek == true
            && track.Timeline.MaxSeek > track.Timeline.MinSeek;
        if (settings.CompactLayout)
            return (CompactMediaFlyoutWidthDip, CompactMediaFlyoutHeightDip + (showSeekbar ? 36 : 0));

        double extraWidth = (settings.RepeatEnabled ? 36 : 0)
            + (settings.ShuffleEnabled ? 36 : 0)
            + (settings.PlayerInfoEnabled ? 72 : 0);
        extraWidth = Math.Max(extraWidth, 72);

        return (
            MediaFlyoutWidthDip - 72 + extraWidth,
            MediaFlyoutHeightDip + (showSeekbar ? 36 : 0));
    }
}
