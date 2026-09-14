namespace MicaFlyouts.Domain.Taskbar;

public static class TaskbarLayoutCalculator
{
    private const double SmallTaskbarThreshold = 40;

    public static TaskbarLayoutResult Calculate(TaskbarLayoutInput input)
    {
        if (input.DpiScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "DPI scale must be positive.");

        var orientation = input.TaskbarRect.Height > input.TaskbarRect.Width
            ? TaskbarOrientation.Vertical
            : TaskbarOrientation.Horizontal;
        int primarySize = orientation == TaskbarOrientation.Vertical
            ? input.TaskbarRect.Height
            : input.TaskbarRect.Width;
        int crossSize = orientation == TaskbarOrientation.Vertical
            ? input.TaskbarRect.Width
            : input.TaskbarRect.Height;
        bool small = crossSize / input.DpiScale < SmallTaskbarThreshold;

        int widgetWidth = Math.Max(1, (int)Math.Round(input.WidgetWidth * input.DpiScale * input.WidgetScale));
        int widgetHeight = Math.Max(1, (int)Math.Round(input.WidgetHeight * input.DpiScale));
        int widgetPrimary = orientation == TaskbarOrientation.Vertical ? widgetHeight : widgetWidth;
        int widgetCross = orientation == TaskbarOrientation.Vertical ? widgetWidth : widgetHeight;
        int crossPos = Math.Max(0, (crossSize - widgetCross) / 2);

        int visualizerWidth = Math.Max(1, (int)Math.Round(input.VisualizerWidth * input.DpiScale));
        int visualizerHeight = Math.Max(1, (int)Math.Round(input.VisualizerHeight * input.DpiScale));
        int visualizerPrimary = orientation == TaskbarOrientation.Vertical ? visualizerHeight : visualizerWidth;
        int visualizerCross = orientation == TaskbarOrientation.Vertical ? visualizerWidth : visualizerHeight;

        int widgetPrimaryPos = CalculateWidgetPrimaryPosition(input, orientation, primarySize, widgetPrimary);
        widgetPrimaryPos += input.ManualPadding;

        PixelRect widgetRect = orientation == TaskbarOrientation.Vertical
            ? new(input.TaskbarRect.Left + crossPos, input.TaskbarRect.Top + widgetPrimaryPos, widgetCross, widgetPrimary)
            : new(input.TaskbarRect.Left + widgetPrimaryPos, input.TaskbarRect.Top + crossPos, widgetPrimary, widgetCross);

        PixelRect visualizerRect = PixelRect.Empty;
        if (input.VisualizerEnabled)
        {
            int visualizerPrimaryPos = input.VisualizerPosition == TaskbarPosition.Start
                ? widgetPrimaryPos - visualizerPrimary
                : widgetPrimaryPos + widgetPrimary;
            int visualizerCrossPos = Math.Max(0, (crossSize - visualizerCross) / 2)
                + (orientation == TaskbarOrientation.Horizontal ? -1 : 0);
            visualizerRect = orientation == TaskbarOrientation.Vertical
                ? new(input.TaskbarRect.Left + visualizerCrossPos, input.TaskbarRect.Top + visualizerPrimaryPos, visualizerCross, visualizerPrimary)
                : new(input.TaskbarRect.Left + visualizerPrimaryPos, input.TaskbarRect.Top + visualizerCrossPos, visualizerPrimary, visualizerCross);
        }

        return new(orientation, small, input.WidgetEnabled ? widgetRect : PixelRect.Empty, visualizerRect);
    }

    private static int CalculateWidgetPrimaryPosition(
        TaskbarLayoutInput input,
        TaskbarOrientation orientation,
        int primarySize,
        int widgetPrimary)
    {
        int result = input.WidgetPosition switch
        {
            TaskbarPosition.Start => 20,
            TaskbarPosition.Center => (primarySize - widgetPrimary) / 2,
            _ => primarySize - widgetPrimary - 20,
        };

        if (!input.WidgetEnabled)
            return result;

        if (input.WidgetPosition == TaskbarPosition.Start)
        {
            if (input.VisualizerEnabled && input.VisualizerPosition == TaskbarPosition.Start)
                result += (int)Math.Round(input.VisualizerWidth * input.DpiScale) + 4;

            if (input.NativeWidgetsPadding)
            {
                var native = input.NativeWidgetsRect;
                bool inStartHalf = native.HasValue && (orientation == TaskbarOrientation.Vertical
                    ? native.Value.Bottom < input.TaskbarRect.Top + input.TaskbarRect.Height / 2.0
                    : native.Value.Right < input.TaskbarRect.Left + input.TaskbarRect.Width / 2.0);
                result = inStartHalf
                    ? (orientation == TaskbarOrientation.Vertical
                        ? native!.Value.Bottom - input.TaskbarRect.Top + 2
                        : native!.Value.Right - input.TaskbarRect.Left + 2)
                    : result + input.NativeWidgetsFallbackPadding + 2;
            }
        }
        else if (input.WidgetPosition == TaskbarPosition.Center)
        {
            if (input.VisualizerEnabled)
            {
                int visualizerWidth = (int)Math.Round(input.VisualizerWidth * input.DpiScale);
                result += input.VisualizerPosition == TaskbarPosition.Start
                    ? visualizerWidth / 2 + 4
                    : -visualizerWidth / 2 + 4;
            }
        }
        else
        {
            if (input.VisualizerEnabled && input.VisualizerPosition == TaskbarPosition.End)
                _ = (int)Math.Round(input.VisualizerWidth * input.DpiScale) - 4;

            var native = input.NativeWidgetsRect;
            bool nativeOnEnd = native.HasValue && (orientation == TaskbarOrientation.Vertical
                ? native.Value.Top > input.TaskbarRect.Top + input.TaskbarRect.Height / 2.0
                : native.Value.Left > input.TaskbarRect.Left + input.TaskbarRect.Width / 2.0);
            if (input.NativeWidgetsPadding && nativeOnEnd)
            {
                result = orientation == TaskbarOrientation.Vertical
                    ? native!.Value.Top - input.TaskbarRect.Top - 2 - widgetPrimary
                    : native!.Value.Left - input.TaskbarRect.Left - 1 - widgetPrimary;
            }
            else if (input.SystemTrayRect is { } tray)
            {
                int trayOffset = orientation == TaskbarOrientation.Vertical
                    ? tray.Top - input.TaskbarRect.Top
                    : tray.Left - input.TaskbarRect.Left;
                result = trayOffset - widgetPrimary - (orientation == TaskbarOrientation.Vertical ? 2 : 1);
            }
            else
            {
                result = primarySize - widgetPrimary - 20;
            }
        }

        // The legacy calculation intentionally ignores automation-derived frame bounds.
        if (input.LegacyWidth && input.WidgetPosition == TaskbarPosition.End && input.NativeWidgetsRect is null && input.SystemTrayRect is null)
            result = primarySize - widgetPrimary - 20;

        return result;
    }
}
