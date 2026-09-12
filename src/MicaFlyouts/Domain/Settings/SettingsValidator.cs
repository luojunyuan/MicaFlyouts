namespace MicaFlyouts.Domain.Settings;

public static class SettingsValidator
{
    public static SettingsSnapshot Normalize(SettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            SchemaVersion = 1,
            FlyoutSelectedMonitor = NonNegative(settings.FlyoutSelectedMonitor),
            Position = Math.Clamp(settings.Position, 0, 5),
            FlyoutAnimationSpeed = Math.Clamp(settings.FlyoutAnimationSpeed, 0, 5),
            Duration = Duration(settings.Duration),
            NextUpDuration = Duration(settings.NextUpDuration),
            FlyoutAnimationEasingStyle = Math.Clamp(settings.FlyoutAnimationEasingStyle, 0, 3),
            LockKeysDuration = Duration(settings.LockKeysDuration),
            LockKeysMonitorPreference = Math.Clamp(settings.LockKeysMonitorPreference, 0, 2),
            AppTheme = Math.Clamp(settings.AppTheme, 0, 2),
            MediaFlyoutBackgroundBlur = Math.Clamp(settings.MediaFlyoutBackgroundBlur, 0, 3),
            TaskbarWidgetSelectedMonitor = NonNegative(settings.TaskbarWidgetSelectedMonitor),
            TaskbarWidgetPosition = Math.Clamp(settings.TaskbarWidgetPosition, 0, 2),
            TaskbarWidgetManualPadding = Math.Clamp(settings.TaskbarWidgetManualPadding, -9999, 9999),
            TaskbarWidgetControlsPosition = Math.Clamp(settings.TaskbarWidgetControlsPosition, 0, 1),
            TaskbarWidgetScrollingTextSpeed = Math.Clamp(settings.TaskbarWidgetScrollingTextSpeed, 1, 100),
            TaskbarVisualizerPosition = Math.Clamp(settings.TaskbarVisualizerPosition, 0, 1),
            TaskbarVisualizerBarCount = Math.Clamp(settings.TaskbarVisualizerBarCount, 1, 128),
            TaskbarVisualizerAudioSensitivity = Math.Clamp(settings.TaskbarVisualizerAudioSensitivity, 1, 3),
            TaskbarVisualizerAudioPeakLevel = Math.Clamp(settings.TaskbarVisualizerAudioPeakLevel, 1, 3),
            AppFilteringMode = Math.Clamp(settings.AppFilteringMode, 0, 1),
            VolumeControlDuration = Duration(settings.VolumeControlDuration),
            AcrylicBlurOpacity = Math.Min(settings.AcrylicBlurOpacity, 255u),
            AllowedApps = NormalizeEntries(settings.AllowedApps),
            BlockedApps = NormalizeEntries(settings.BlockedApps),
            AppLanguage = string.IsNullOrWhiteSpace(settings.AppLanguage) ? "system" : settings.AppLanguage,
            FontFamily = string.IsNullOrWhiteSpace(settings.FontFamily)
                ? SettingsSnapshot.CreateDefault().FontFamily
                : settings.FontFamily,
            LastKnownVersion = settings.LastKnownVersion ?? string.Empty,
            Uuid = settings.Uuid == Guid.Empty ? Guid.NewGuid() : settings.Uuid,
        };
    }

    public static SettingsSnapshot ForImport(SettingsSnapshot imported, Guid currentUuid)
    {
        return Normalize(imported) with { Uuid = currentUuid };
    }

    private static int Duration(int value) => Math.Clamp(value, 0, 10000);

    private static int NonNegative(int value) => Math.Max(0, value);

    private static string[] NormalizeEntries(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return [];

        return [.. values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
