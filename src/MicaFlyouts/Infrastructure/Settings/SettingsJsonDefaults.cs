using System.Text.Json;
using MicaFlyouts.Domain.Settings;

namespace MicaFlyouts.Infrastructure.Settings;

internal static class SettingsJsonDefaults
{
    public static SettingsSnapshot ApplyMissing(string json, SettingsSnapshot parsed)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var defaults = SettingsSnapshot.CreateDefault();
        return parsed with
        {
            FlyoutAnimationSpeed = ValueOrDefault(root, "flyoutAnimationSpeed", parsed.FlyoutAnimationSpeed, defaults.FlyoutAnimationSpeed),
            PlayerInfoEnabled = ValueOrDefault(root, "playerInfoEnabled", parsed.PlayerInfoEnabled, defaults.PlayerInfoEnabled),
            Startup = ValueOrDefault(root, "startup", parsed.Startup, defaults.Startup),
            Duration = ValueOrDefault(root, "duration", parsed.Duration, defaults.Duration),
            NextUpDuration = ValueOrDefault(root, "nextUpDuration", parsed.NextUpDuration, defaults.NextUpDuration),
            FlyoutAnimationEasingStyle = ValueOrDefault(root, "flyoutAnimationEasingStyle", parsed.FlyoutAnimationEasingStyle, defaults.FlyoutAnimationEasingStyle),
            LockKeysEnabled = ValueOrDefault(root, "lockKeysEnabled", parsed.LockKeysEnabled, defaults.LockKeysEnabled),
            LockKeysCapsEnabled = ValueOrDefault(root, "lockKeysCapsEnabled", parsed.LockKeysCapsEnabled, defaults.LockKeysCapsEnabled),
            LockKeysNumEnabled = ValueOrDefault(root, "lockKeysNumEnabled", parsed.LockKeysNumEnabled, defaults.LockKeysNumEnabled),
            LockKeysScrollEnabled = ValueOrDefault(root, "lockKeysScrollEnabled", parsed.LockKeysScrollEnabled, defaults.LockKeysScrollEnabled),
            LockKeysDuration = ValueOrDefault(root, "lockKeysDuration", parsed.LockKeysDuration, defaults.LockKeysDuration),
            LockKeysAnimated = ValueOrDefault(root, "lockKeysAnimated", parsed.LockKeysAnimated, defaults.LockKeysAnimated),
            LockKeysInsertEnabled = ValueOrDefault(root, "lockKeysInsertEnabled", parsed.LockKeysInsertEnabled, defaults.LockKeysInsertEnabled),
            LockKeysAcrylicWindowEnabled = ValueOrDefault(root, "lockKeysAcrylicWindowEnabled", parsed.LockKeysAcrylicWindowEnabled, defaults.LockKeysAcrylicWindowEnabled),
            MediaFlyoutEnabled = ValueOrDefault(root, "mediaFlyoutEnabled", parsed.MediaFlyoutEnabled, defaults.MediaFlyoutEnabled),
            DisableIfFullscreen = ValueOrDefault(root, "disableIfFullscreen", parsed.DisableIfFullscreen, defaults.DisableIfFullscreen),
            MediaFlyoutAcrylicWindowEnabled = ValueOrDefault(root, "mediaFlyoutAcrylicWindowEnabled", parsed.MediaFlyoutAcrylicWindowEnabled, defaults.MediaFlyoutAcrylicWindowEnabled),
            NextUpAcrylicWindowEnabled = ValueOrDefault(root, "nextUpAcrylicWindowEnabled", parsed.NextUpAcrylicWindowEnabled, defaults.NextUpAcrylicWindowEnabled),
            VolumeMixerAcrylicWindowEnabled = ValueOrDefault(root, "volumeMixerAcrylicWindowEnabled", parsed.VolumeMixerAcrylicWindowEnabled, defaults.VolumeMixerAcrylicWindowEnabled),
            AppLanguage = ValueOrDefault(root, "appLanguage", parsed.AppLanguage, defaults.AppLanguage),
            FontFamily = ValueOrDefault(root, "fontFamily", parsed.FontFamily, defaults.FontFamily),
            TaskbarWidgetPadding = ValueOrDefault(root, "taskbarWidgetPadding", parsed.TaskbarWidgetPadding, defaults.TaskbarWidgetPadding),
            TaskbarWidgetShowPauseOverlay = ValueOrDefault(root, "taskbarWidgetShowPauseOverlay", parsed.TaskbarWidgetShowPauseOverlay, defaults.TaskbarWidgetShowPauseOverlay),
            TaskbarWidgetControlsPosition = ValueOrDefault(root, "taskbarWidgetControlsPosition", parsed.TaskbarWidgetControlsPosition, defaults.TaskbarWidgetControlsPosition),
            TaskbarWidgetAnimated = ValueOrDefault(root, "taskbarWidgetAnimated", parsed.TaskbarWidgetAnimated, defaults.TaskbarWidgetAnimated),
            TaskbarWidgetScrollingTextSpeed = ValueOrDefault(root, "taskbarWidgetScrollingTextSpeed", parsed.TaskbarWidgetScrollingTextSpeed, defaults.TaskbarWidgetScrollingTextSpeed),
            TaskbarVisualizerEnabled = ValueOrDefault(root, "taskbarVisualizerEnabled", parsed.TaskbarVisualizerEnabled, defaults.TaskbarVisualizerEnabled),
            TaskbarVisualizerPosition = ValueOrDefault(root, "taskbarVisualizerPosition", parsed.TaskbarVisualizerPosition, defaults.TaskbarVisualizerPosition),
            TaskbarVisualizerClickable = ValueOrDefault(root, "taskbarVisualizerClickable", parsed.TaskbarVisualizerClickable, defaults.TaskbarVisualizerClickable),
            TaskbarVisualizerBarCount = ValueOrDefault(root, "taskbarVisualizerBarCount", parsed.TaskbarVisualizerBarCount, defaults.TaskbarVisualizerBarCount),
            TaskbarVisualizerAudioSensitivity = ValueOrDefault(root, "taskbarVisualizerAudioSensitivity", parsed.TaskbarVisualizerAudioSensitivity, defaults.TaskbarVisualizerAudioSensitivity),
            TaskbarVisualizerAudioPeakLevel = ValueOrDefault(root, "taskbarVisualizerAudioPeakLevel", parsed.TaskbarVisualizerAudioPeakLevel, defaults.TaskbarVisualizerAudioPeakLevel),
            VolumeControlDuration = ValueOrDefault(root, "volumeControlDuration", parsed.VolumeControlDuration, defaults.VolumeControlDuration),
            AcrylicBlurOpacity = ValueOrDefault(root, "acrylicBlurOpacity", parsed.AcrylicBlurOpacity, defaults.AcrylicBlurOpacity),
            ShowUpdateNotifications = ValueOrDefault(root, "showUpdateNotifications", parsed.ShowUpdateNotifications, defaults.ShowUpdateNotifications),
            AnonymousTelemetryAllowed = ValueOrDefault(root, "anonymousTelemetryAllowed", parsed.AnonymousTelemetryAllowed, defaults.AnonymousTelemetryAllowed),
            AllowedApps = root.TryGetProperty("allowedApps", out _) ? parsed.AllowedApps : defaults.AllowedApps,
            BlockedApps = root.TryGetProperty("blockedApps", out _) ? parsed.BlockedApps : defaults.BlockedApps,
        };
    }

    private static bool ValueOrDefault(JsonElement root, string name, bool value, bool fallback)
        => root.TryGetProperty(name, out _) ? value : fallback;

    private static int ValueOrDefault(JsonElement root, string name, int value, int fallback)
        => root.TryGetProperty(name, out _) ? value : fallback;

    private static uint ValueOrDefault(JsonElement root, string name, uint value, uint fallback)
        => root.TryGetProperty(name, out _) ? value : fallback;

    private static string ValueOrDefault(JsonElement root, string name, string value, string fallback)
        => root.TryGetProperty(name, out _) ? value : fallback;
}
