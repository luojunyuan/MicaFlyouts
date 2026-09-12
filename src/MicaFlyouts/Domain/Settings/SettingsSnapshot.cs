using System.Text.Json.Serialization;

namespace MicaFlyouts.Domain.Settings;

public sealed record SettingsSnapshot
{
    [JsonConstructor]
    public SettingsSnapshot()
    {
    }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    public bool CompactLayout { get; init; }
    public int FlyoutSelectedMonitor { get; init; }
    public int Position { get; init; }
    public int FlyoutAnimationSpeed { get; init; } = 2;
    public bool PlayerInfoEnabled { get; init; } = true;
    public bool RepeatEnabled { get; init; }
    public bool ShuffleEnabled { get; init; }
    public bool Startup { get; init; } = true;
    public bool MediaFlyoutAlwaysDisplay { get; init; }
    public int Duration { get; init; } = 3000;
    public bool NextUpEnabled { get; init; }
    public int NextUpDuration { get; init; } = 2000;
    public int NIconLeftClick { get; init; }
    public bool CenterTitleArtist { get; init; }
    public int FlyoutAnimationEasingStyle { get; init; } = 2;

    public bool LockKeysEnabled { get; init; } = true;
    public bool LockKeysCapsEnabled { get; init; } = true;
    public bool LockKeysNumEnabled { get; init; } = true;
    public bool LockKeysScrollEnabled { get; init; } = true;
    public int LockKeysDuration { get; init; } = 2000;
    public bool LockKeysBoldUi { get; init; }
    public int LockKeysMonitorPreference { get; init; }
    public bool LockKeysAnimated { get; init; } = true;
    public bool LockKeysInsertEnabled { get; init; } = true;
    public bool LockKeysAcrylicWindowEnabled { get; init; } = true;

    public int AppTheme { get; init; }
    public bool MediaFlyoutEnabled { get; init; } = true;
    public bool MediaFlyoutVolumeKeysExcluded { get; init; }
    public bool NIconSymbol { get; init; }
    public bool NIconHide { get; init; }
    public bool DisableIfFullscreen { get; init; } = true;
    public string LastKnownVersion { get; init; } = string.Empty;
    public bool SeekbarEnabled { get; init; }
    public bool PauseOtherSessionsEnabled { get; init; }
    public int MediaFlyoutBackgroundBlur { get; init; }
    public bool MediaFlyoutAcrylicWindowEnabled { get; init; } = true;
    public bool NextUpAcrylicWindowEnabled { get; init; } = true;
    public bool VolumeMixerAcrylicWindowEnabled { get; init; } = true;

    public string AppLanguage { get; init; } = "system";
    public string FontFamily { get; init; } = "Segoe UI Variable, Microsoft YaHei UI, Yu Gothic UI";

    public bool TaskbarWidgetEnabled { get; init; }
    public int TaskbarWidgetSelectedMonitor { get; init; }
    public bool TaskbarWidgetAutoHide { get; init; }
    public int TaskbarWidgetPosition { get; init; }
    public bool TaskbarWidgetPadding { get; init; } = true;
    public int TaskbarWidgetManualPadding { get; init; }
    public bool TaskbarWidgetBackgroundBlur { get; init; }
    public bool TaskbarWidgetHideCompletely { get; init; }
    public bool TaskbarWidgetFixedWidth { get; init; }
    public bool TaskbarWidgetShowPauseOverlay { get; init; } = true;
    public bool TaskbarWidgetControlsEnabled { get; init; }
    public int TaskbarWidgetControlsPosition { get; init; } = 1;
    public bool TaskbarWidgetAnimated { get; init; } = true;
    public bool TaskbarWidgetScrollingEnabled { get; init; }
    public bool TaskbarWidgetScrollingTextLoopForever { get; init; }
    public int TaskbarWidgetScrollingTextSpeed { get; init; } = 20;

    public bool TaskbarVisualizerEnabled { get; init; }
    public int TaskbarVisualizerPosition { get; init; } = 1;
    public bool TaskbarVisualizerClickable { get; init; } = true;
    public int TaskbarVisualizerBarCount { get; init; } = 10;
    public bool TaskbarVisualizerCenteredBars { get; init; }
    public bool TaskbarVisualizerBaseline { get; init; }
    public int TaskbarVisualizerAudioSensitivity { get; init; } = 2;
    public int TaskbarVisualizerAudioPeakLevel { get; init; } = 3;
    public bool TaskbarVisualizerBaselineAutoHide { get; init; }

    public bool AppFilteringEnabled { get; init; }
    public int AppFilteringMode { get; init; }

    public bool VolumeControlEnabled { get; init; }
    public bool VolumeControlAboveMediaFlyout { get; init; }
    public int VolumeControlDuration { get; init; } = 3000;
    public bool VolumeMixerEnabled { get; init; }
    public bool VolumeMixerHighlightActiveApps { get; init; }

    public uint AcrylicBlurOpacity { get; init; } = 175;
    public bool UseAlbumArtAsAccentColor { get; init; }
    public long LastUpdateNotificationUnixSeconds { get; init; }
    public bool ShowUpdateNotifications { get; init; } = true;
    public bool LegacyTaskbarWidthEnabled { get; init; }
    public bool AnonymousTelemetryAllowed { get; init; } = true;

    // The installation UUID is persisted internally, but omitted by export.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid Uuid { get; init; } = Guid.NewGuid();

    public IReadOnlyList<string> AllowedApps { get; init; } = [];
    public IReadOnlyList<string> BlockedApps { get; init; } = [];

    public static SettingsSnapshot CreateDefault(Guid? uuid = null)
    {
        return new SettingsSnapshot { Uuid = uuid ?? Guid.NewGuid() };
    }
}
