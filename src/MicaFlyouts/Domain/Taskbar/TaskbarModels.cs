namespace MicaFlyouts.Domain.Taskbar;

public enum TaskbarPosition
{
    Start,
    Center,
    End,
}

public sealed record TaskbarSnapshot(
    string? TargetMonitorId,
    TaskbarOrientation Orientation,
    PixelRect TaskbarRect,
    bool IsEmbedded,
    bool IsExplorerAvailable,
    string Title,
    string Artist,
    bool IsPlaying,
    bool HasVisualizerContent);

public enum TaskbarOrientation
{
    Horizontal,
    Vertical,
}

public sealed record TaskbarLayoutInput(
    PixelRect TaskbarRect,
    double DpiScale,
    double WidgetWidth,
    double WidgetHeight,
    double VisualizerWidth,
    double VisualizerHeight,
    TaskbarPosition WidgetPosition,
    TaskbarPosition VisualizerPosition,
    bool WidgetEnabled,
    bool VisualizerEnabled,
    bool NativeWidgetsPadding,
    int ManualPadding,
    bool LegacyWidth,
    bool IsMainTaskbar,
    PixelRect? NativeWidgetsRect = null,
    PixelRect? SystemTrayRect = null,
    int NativeWidgetsFallbackPadding = 216,
    double WidgetScale = 0.9);

public sealed record TaskbarLayoutResult(
    TaskbarOrientation Orientation,
    bool IsSmallTaskbar,
    PixelRect WidgetRect,
    PixelRect VisualizerRect);
