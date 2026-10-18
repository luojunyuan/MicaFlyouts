using CommunityToolkit.WinUI.Controls;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Infrastructure.Audio;
using MicaFlyouts.Infrastructure.Localization;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Media;
using MicaFlyouts.Infrastructure.Notifications;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using MicaFlyouts.Infrastructure.Updates;
using MicaFlyouts.Infrastructure.Windows;
using MicaFlyouts.Infrastructure.Interop;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using MicaFlyouts.Domain.Flyouts;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Domain.Windows;
using MicaFlyouts.UI.Animation;
using static Microsoft.UI.Reactor.Factories;
using MicaFlyouts.Features.LockKeys;
using MicaFlyouts.Features.Media;
using MicaFlyouts.Features.NextUp;
using MicaFlyouts.Features.Onboarding;
using MicaFlyouts.Features.Settings;
using MicaFlyouts.Features.Taskbar;
using MicaFlyouts.Features.Tray;
using MicaFlyouts.Features.Volume;

namespace MicaFlyouts.App;

public sealed partial class AppServices : IDisposable
{
    private readonly SingleInstanceService _singleInstance;
    private readonly UiDispatcher _dispatcher;
    private readonly AppLogger _logger;
    private readonly WindowRegistry _windows = new();
    private readonly KeyboardHookService _keyboard;
    private readonly TrayIconFeature _tray;
    private Action? _mediaUnsubscribe;
    private Action? _settingsUnsubscribe;
    private ReactorWindow? _mainWindow;
    private DispatcherQueueTimer? _mediaFlyoutDismissTimer;
    private DispatcherQueueTimer? _lockKeysDismissTimer;
    private bool? _lockKeysAcrylicBackdrop;
    private bool? _mediaFlyoutAcrylicBackdrop;
    private long _mediaFlyoutDismissAtTick;
    private bool _mediaFlyoutPointerWasOver;
    private string _lastTrack = string.Empty;
    private int _started;
    private int _exitRequested;
    private int _disposed;

    public AppServices(
        SingleInstanceService singleInstance,
        UiDispatcher dispatcher,
        AppLogger logger,
        SettingsStore settings,
        MediaStore mediaStore,
        VolumeStore volumeStore,
        LockKeyStore lockKeyStore,
        TaskbarStore taskbarStore,
        VisualizerStore visualizerStore,
        OnboardingStore onboardingStore,
        LocalizationStore localizationStore,
        UpdateStore updateStore,
        LocalizationService localization,
        MediaSessionService media,
        AudioService audio,
        VisualizerService visualizer,
        TaskbarHostService taskbar,
        UpdateCheckerService updater,
        MonitorService monitors,
        FullscreenService fullscreen,
        NotificationService notifications,
        TrayIconFeature tray)
    {
        _singleInstance = singleInstance;
        _dispatcher = dispatcher;
        _logger = logger;
        Settings = settings;
        MediaStore = mediaStore;
        VolumeStore = volumeStore;
        LockKeyStore = lockKeyStore;
        TaskbarStore = taskbarStore;
        VisualizerStore = visualizerStore;
        OnboardingStore = onboardingStore;
        LocalizationStore = localizationStore;
        UpdateStore = updateStore;
        Localization = localization;
        Media = media;
        Audio = audio;
        Visualizer = visualizer;
        Taskbar = taskbar;
        Updater = updater;
        Monitors = monitors;
        Fullscreen = fullscreen;
        Notifications = notifications;
        _tray = tray;
        _keyboard = new KeyboardHookService(OnKeyboardEvent);
        Commands = new AppCommands
        {
            ShowSettings = OpenSettings,
            ShowMediaFlyout = ShowMediaFlyout,
            ToggleMediaFlyout = ToggleMediaFlyout,
            Exit = Exit,
        };
    }

    public SettingsStore Settings { get; }
    public MediaStore MediaStore { get; }
    public VolumeStore VolumeStore { get; }
    public LockKeyStore LockKeyStore { get; }
    public TaskbarStore TaskbarStore { get; }
    public VisualizerStore VisualizerStore { get; }
    public OnboardingStore OnboardingStore { get; }
    public LocalizationStore LocalizationStore { get; }
    public UpdateStore UpdateStore { get; }
    public LocalizationService Localization { get; }
    public MediaSessionService Media { get; }
    public AudioService Audio { get; }
    public VisualizerService Visualizer { get; }
    public TaskbarHostService Taskbar { get; }
    public UpdateCheckerService Updater { get; }
    public MonitorService Monitors { get; }
    public FullscreenService Fullscreen { get; }
    public NotificationService Notifications { get; }
    public AppCommands Commands { get; }
    public WindowRegistry Windows => _windows;
    internal AppLogger Logger => _logger;

    public void OpenMainWindow()
    {
        if (_mainWindow is not null)
            return;
        bool acrylicEnabled = Settings.Snapshot.MediaFlyoutAcrylicWindowEnabled;
        _mainWindow = _windows.OpenOrActivate(
            new WindowKey("main-media"),
            new WindowSpec
            {
                Title = "Mica Flyouts",
                Width = FlyoutWindowDimensions.MediaFlyoutWidthDip,
                Height = FlyoutWindowDimensions.MediaFlyoutHeightDip,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                Level = WindowLevel.AlwaysOnTop,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                CornerStyle = WindowCornerStyle.RoundedSmall,
                StartPosition = WindowStartPosition.Manual,
                ManualPosition = (-10000, -10000),
                Opacity = 0,
                Backdrop = BackdropChoice.Of(acrylicEnabled
                    ? BackdropKind.DesktopAcrylic
                    : BackdropKind.Mica),
            },
            static () => new MediaFlyoutWindowComponent());
        _mediaFlyoutAcrylicBackdrop = acrylicEnabled;
        _mainWindow.Hide();
    }

    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0
            || Interlocked.Exchange(ref _started, 1) != 0)
            return;

        ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;
        StartupRegistration.Apply(Settings.Snapshot.Startup);
        _singleInstance.StartSettingsListener(() =>
        {
            _logger.Info("Received a settings request from a second instance.");
            OpenSettings();
            _logger.Info("Settings window opened for the second instance.");
        });
        Notifications.Initialize();
        _mediaUnsubscribe = MediaStore.Subscribe(OnMediaChanged);
        _settingsUnsubscribe = Settings.Subscribe(OnSettingsChanged);
        InitializeMediaFlyoutDismissTimer();
        InitializeLockKeysDismissTimer();
        Media.Start();
        Audio.Start();
        Visualizer.Start();
        Taskbar.Start();
        _keyboard.Start();

        _tray.Start();

        SyncTaskbarWindows();
        bool firstRun = string.IsNullOrWhiteSpace(Settings.Snapshot.LastKnownVersion);
        if (firstRun)
        {
            Settings.Update(settings => settings with { LastKnownVersion = AppBranding.Version }, save: false);
            Settings.SaveNow();
            OpenOnboarding();
        }
    }

    private void OnSettingsChanged()
    {
        UiDispatcher.EnqueueOrRun(() =>
        {
            Localization.Apply();
            StartupRegistration.Apply(Settings.Snapshot.Startup);
            SyncTaskbarWindows();

            var settings = Settings.Snapshot;
            if (_mainWindow is { } mediaWindow
                && _mediaFlyoutAcrylicBackdrop != settings.MediaFlyoutAcrylicWindowEnabled)
            {
                mediaWindow.Update(mediaWindow.Spec with
                {
                    Backdrop = BackdropChoice.Of(settings.MediaFlyoutAcrylicWindowEnabled
                        ? BackdropKind.DesktopAcrylic
                        : BackdropKind.Mica),
                });
                _mediaFlyoutAcrylicBackdrop = settings.MediaFlyoutAcrylicWindowEnabled;
            }

            var lockKeysWindowKey = new WindowKey("lock-keys");
            if (_windows.TryGet(lockKeysWindowKey, out var lockWindow) && lockWindow is not null)
            {
                if (_lockKeysAcrylicBackdrop != settings.LockKeysAcrylicWindowEnabled)
                {
                    lockWindow.Update(lockWindow.Spec with
                    {
                        Backdrop = BackdropChoice.Of(settings.LockKeysAcrylicWindowEnabled
                            ? BackdropKind.DesktopAcrylic
                            : BackdropKind.None),
                    });
                    _lockKeysAcrylicBackdrop = settings.LockKeysAcrylicWindowEnabled;
                }

                if (!settings.LockKeysEnabled)
                {
                    _lockKeysDismissTimer?.Stop();
                    if (lockWindow.IsVisible)
                        LockKeysFlyoutAnimationCoordinator.Hide(lockWindow, settings);
                }
                else if (lockWindow.IsVisible)
                {
                    RestartLockKeysDismissTimer(settings);
                }
            }
            else if (!settings.LockKeysEnabled)
            {
                _lockKeysDismissTimer?.Stop();
            }

            if (!settings.MediaFlyoutEnabled || MediaStore.Snapshot.ActiveSession is null)
                HideMediaFlyout(settings);
            else if (settings.MediaFlyoutAlwaysDisplay)
            {
                _mediaFlyoutDismissTimer?.Stop();
                if (_mainWindow?.IsVisible == true
                    || MediaStore.Snapshot.ActiveSession?.Track.PlaybackStatus == MediaPlaybackStatus.Playing)
                    ShowMediaFlyout();
            }
            else if (_mainWindow?.IsVisible == true)
                ShowMediaFlyout();
        });
    }

    private void OnMediaChanged()
    {
        var snapshot = MediaStore.Snapshot;
        var current = snapshot.ActiveSession;
        string title = current?.Track.Title ?? string.Empty;
        if (current is not null
            && current.Track.PlaybackStatus == MediaPlaybackStatus.Playing
            && !string.IsNullOrWhiteSpace(_lastTrack)
            && !string.Equals(_lastTrack, title, StringComparison.OrdinalIgnoreCase)
            && Settings.Snapshot.NextUpEnabled
            && !FullscreenService.IsFullscreenApplicationRunning())
        {
            UiDispatcher.EnqueueOrRun(() => OpenNextUp(current.Track));
        }
        else if (current is null)
        {
            UiDispatcher.EnqueueOrRun(() => HideMediaFlyout(Settings.Snapshot));
        }
        else if (current.Track.PlaybackStatus == MediaPlaybackStatus.Playing
            && Settings.Snapshot.MediaFlyoutEnabled
            && Settings.Snapshot.MediaFlyoutAlwaysDisplay)
        {
            UiDispatcher.EnqueueOrRun(ShowMediaFlyout);
        }
        _lastTrack = title;
    }

    private void OnKeyboardEvent(KeyboardKeyEvent keyEvent)
    {
        var settings = Settings.Snapshot;
        if (keyEvent.VirtualKey is 0xB3 or 0xB0 or 0xB1 or 0xB2)
        {
            if (keyEvent.IsKeyUp && !settings.MediaFlyoutVolumeKeysExcluded || !keyEvent.IsKeyUp)
                UiDispatcher.EnqueueOrRun(ShowMediaFlyout);
            return;
        }
        if (keyEvent.VirtualKey is 0xAD or 0xAE or 0xAF)
        {
            if (keyEvent.IsKeyUp && settings.VolumeControlEnabled)
                UiDispatcher.EnqueueOrRun(OpenVolumeFlyout);
            else if (keyEvent.IsKeyUp && !settings.MediaFlyoutVolumeKeysExcluded)
                UiDispatcher.EnqueueOrRun(ShowMediaFlyout);
            return;
        }
        if (!keyEvent.IsKeyUp || !settings.LockKeysEnabled)
            return;

        LockKeyKind? activeKey = keyEvent.VirtualKey switch
        {
            KeyboardHookService.CapsLock => LockKeyKind.CapsLock,
            KeyboardHookService.NumLock => LockKeyKind.NumLock,
            KeyboardHookService.ScrollLock => LockKeyKind.ScrollLock,
            KeyboardHookService.Insert => LockKeyKind.Insert,
            _ => null,
        };
        if (activeKey is not { } key || !IsLockKeyEnabled(settings, key))
            return;
        // Read the toggle state after the low-level hook has returned to Windows.
        UiDispatcher.Enqueue(() => ShowLockKey(key));
    }

    private void ShowLockKey(LockKeyKind key)
    {
        var settings = Settings.Snapshot;
        if (!settings.LockKeysEnabled || !IsLockKeyEnabled(settings, key)
            || settings.DisableIfFullscreen && FullscreenService.IsFullscreenApplicationRunning())
            return;

        LockKeyStore.Set(KeyboardHookService.ReadSnapshot() with { ActiveKey = key });
        var window = GetOrCreateLockKeysWindow(settings);
        var monitor = GetLockKeysMonitor(settings);
        LockKeysFlyoutAnimationCoordinator.Show(window, settings, monitor);
        RestartLockKeysDismissTimer(settings);
    }

    private ReactorWindow GetOrCreateLockKeysWindow(SettingsSnapshot settings)
    {
        var key = new WindowKey("lock-keys");
        if (_windows.TryGet(key, out var existing) && existing is not null)
            return existing;

        var window = _windows.OpenOrActivate(
            key,
            new WindowSpec
            {
                Title = "Lock Keys",
                Width = 220,
                Height = 50,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                Level = WindowLevel.AlwaysOnTop,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                CornerStyle = WindowCornerStyle.RoundedSmall,
                StartPosition = WindowStartPosition.Manual,
                ManualPosition = (-10000, -10000),
                Opacity = 0,
                Backdrop = BackdropChoice.Of(settings.LockKeysAcrylicWindowEnabled
                    ? BackdropKind.DesktopAcrylic
                    : BackdropKind.None),
            },
            static () => new LockKeysWindowComponent());
        window.Hide();
        _lockKeysAcrylicBackdrop = settings.LockKeysAcrylicWindowEnabled;
        return window;
    }

    private static MonitorSnapshot GetLockKeysMonitor(SettingsSnapshot settings)
        => settings.LockKeysMonitorPreference switch
        {
            1 => MonitorService.ForWindow(NativeWindowApi.GetForegroundWindow()),
            2 => MonitorService.ForCursor(),
            _ => MonitorService.Select(settings.FlyoutSelectedMonitor),
        };

    private static bool IsLockKeyEnabled(SettingsSnapshot settings, LockKeyKind key)
        => key switch
        {
            LockKeyKind.CapsLock => settings.LockKeysCapsEnabled,
            LockKeyKind.NumLock => settings.LockKeysNumEnabled,
            LockKeyKind.ScrollLock => settings.LockKeysScrollEnabled,
            LockKeyKind.Insert => settings.LockKeysInsertEnabled,
            _ => false,
        };

    private void InitializeLockKeysDismissTimer()
    {
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null)
            return;

        _lockKeysDismissTimer = dispatcher.CreateTimer();
        _lockKeysDismissTimer.IsRepeating = false;
        _lockKeysDismissTimer.Tick += (_, _) =>
        {
            _lockKeysDismissTimer?.Stop();
            var settings = Settings.Snapshot;
            if (_windows.TryGet(new WindowKey("lock-keys"), out var window) && window?.IsVisible == true)
                LockKeysFlyoutAnimationCoordinator.Hide(window, settings);
        };
    }

    private void RestartLockKeysDismissTimer(SettingsSnapshot settings)
    {
        if (_lockKeysDismissTimer is null)
            return;

        _lockKeysDismissTimer.Stop();
        _lockKeysDismissTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, settings.LockKeysDuration));
        _lockKeysDismissTimer.Start();
    }

    public void OpenSettings()
    {
        _logger.Info("Opening settings window.");
        _ = _windows.OpenOrActivate(
            new WindowKey("settings"),
            new WindowSpec
            {
                Title = "Mica Flyouts Settings",
                Width = 900,
                Height = 700,
                MinWidth = 800,
                MinHeight = 600,
                Icon = AppBranding.WindowIcon,
                StartPosition = WindowStartPosition.CenterOnCurrent,
                CornerStyle = WindowCornerStyle.Rounded,
                Backdrop = BackdropChoice.Of(BackdropKind.Mica),
                ExtendsContentIntoTitleBar = true,
            },
            static () => new SettingsWindowComponent());
        _logger.Info("Settings window request completed.");
    }

    public void ShowMediaFlyout()
    {
        var settings = Settings.Snapshot;
        var active = MediaStore.Snapshot.ActiveSession;
        if (_mainWindow is null || active is null || !settings.MediaFlyoutEnabled)
            return;
        WindowAnimationCoordinator.Show(_mainWindow, settings, active.Track);
        RestartMediaFlyoutDismissTimer(settings);
    }

    public void ToggleMediaFlyout()
    {
        if (_mainWindow?.IsVisible == true)
        {
            HideMediaFlyout(Settings.Snapshot);
            return;
        }
        ShowMediaFlyout();
    }

    private void InitializeMediaFlyoutDismissTimer()
    {
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null)
            return;

        _mediaFlyoutDismissTimer = dispatcher.CreateTimer();
        _mediaFlyoutDismissTimer.Interval = TimeSpan.FromMilliseconds(100);
        _mediaFlyoutDismissTimer.IsRepeating = true;
        _mediaFlyoutDismissTimer.Tick += (_, _) =>
        {
            var settings = Settings.Snapshot;
            if (settings.MediaFlyoutAlwaysDisplay
                || !settings.MediaFlyoutEnabled
                || _mainWindow is not { IsVisible: true } window)
            {
                _mediaFlyoutDismissTimer?.Stop();
                _mediaFlyoutPointerWasOver = false;
                return;
            }

            bool pointerOverMedia = IsPointerOverWindow(window);
            bool pointerOverVolume = settings.VolumeControlEnabled
                && settings.VolumeControlAboveMediaFlyout
                && _windows.TryGet(new WindowKey("volume"), out var volumeWindow)
                && volumeWindow is { IsVisible: true }
                && IsPointerOverWindow(volumeWindow);
            if (pointerOverMedia || pointerOverVolume)
            {
                _mediaFlyoutPointerWasOver = true;
                return;
            }

            if (_mediaFlyoutPointerWasOver)
            {
                _mediaFlyoutPointerWasOver = false;
                _mediaFlyoutDismissAtTick = Environment.TickCount64 + Math.Max(1, settings.Duration);
                return;
            }

            if (Environment.TickCount64 >= _mediaFlyoutDismissAtTick)
                HideMediaFlyout(settings);
        };
    }

    private void RestartMediaFlyoutDismissTimer(SettingsSnapshot settings)
    {
        if (settings.MediaFlyoutAlwaysDisplay || _mediaFlyoutDismissTimer is null)
        {
            _mediaFlyoutDismissTimer?.Stop();
            return;
        }

        _mediaFlyoutPointerWasOver = false;
        _mediaFlyoutDismissAtTick = Environment.TickCount64 + Math.Max(1, settings.Duration);
        _mediaFlyoutDismissTimer.Interval = TimeSpan.FromMilliseconds(100);
        _mediaFlyoutDismissTimer.IsRepeating = true;
        _mediaFlyoutDismissTimer.Start();
    }

    private void HideMediaFlyout(SettingsSnapshot settings)
    {
        _mediaFlyoutDismissTimer?.Stop();
        _mediaFlyoutPointerWasOver = false;
        if (_mainWindow?.IsVisible == true)
            WindowAnimationCoordinator.Hide(_mainWindow, settings, MediaStore.Snapshot.ActiveSession?.Track);
    }

    private static bool IsPointerOverWindow(ReactorWindow window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window.NativeWindow);
        if (!NativeWindowApi.TryGetWindowRect(handle, out var bounds)
            || !NativeWindowApi.TryGetCursorPosition(out var cursor))
            return false;

        return cursor.X >= bounds.Left
            && cursor.X < bounds.Right
            && cursor.Y >= bounds.Top
            && cursor.Y < bounds.Bottom;
    }

    private void OpenVolumeFlyout()
    {
        if (!Settings.Snapshot.VolumeControlEnabled)
            return;
        _windows.OpenOrActivate(
            new WindowKey("volume"),
            new WindowSpec
            {
                Title = "Volume",
                Width = 240,
                Height = 50,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                Level = WindowLevel.AlwaysOnTop,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                CornerStyle = WindowCornerStyle.RoundedSmall,
                Backdrop = BackdropChoice.Of(BackdropKind.DesktopAcrylic),
            },
            static () => new VolumeFlyoutWindowComponent());
    }

    private void OpenNextUp(MediaTrackSnapshot track)
        => _windows.OpenOrActivate(
            new WindowKey("next-up"),
            new WindowSpec
            {
                Title = "Next Up",
                Width = 310,
                Height = 50,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                Level = WindowLevel.AlwaysOnTop,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                CornerStyle = WindowCornerStyle.RoundedSmall,
                Backdrop = BackdropChoice.Of(BackdropKind.DesktopAcrylic),
            },
            () => new NextUpWindowComponent(track));

    private void OpenOnboarding()
        => _windows.OpenOrActivate(
            new WindowKey("onboarding"),
            new WindowSpec
            {
                Title = "Mica Flyouts",
                Width = 900,
                Height = 600,
                MinWidth = 870,
                MinHeight = 500,
                Icon = AppBranding.WindowIcon,
                IsMovableByBackground = true,
                StartPosition = WindowStartPosition.CenterOnCurrent,
                CornerStyle = WindowCornerStyle.Rounded,
                Backdrop = BackdropChoice.Of(BackdropKind.Mica),
            },
            static () => new OnboardingWindowComponent(),
            static host =>
            {
                if (host.OwningWindow?.AppWindow.Presenter is OverlappedPresenter presenter)
                    presenter.SetBorderAndTitleBar(true, false);
            });

    private void SyncTaskbarWindows()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        var settings = Settings.Snapshot;
        var widgetKey = new WindowKey("taskbar-widget");
        var widget = GetOrCreateTaskbarWindow(
            widgetKey,
            new WindowSpec
            {
                Title = "Mica Flyouts Taskbar Widget",
                Width = 100,
                Height = 40,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                StartPosition = WindowStartPosition.Manual,
                ManualPosition = (-10000, -10000),
                Opacity = 0,
            },
            static () => new TaskbarWidgetWindowComponent());
        if (settings.TaskbarWidgetEnabled)
        {
            widget.Hide();
            Taskbar.AttachWidgetWindow(widget);
        }
        else
        {
            HideTaskbarWindow(widgetKey);
        }

        var visualizerKey = new WindowKey("taskbar-visualizer");
        var visualizer = GetOrCreateTaskbarWindow(
            visualizerKey,
            new WindowSpec
            {
                Title = "Mica Flyouts Visualizer",
                Width = 84,
                Height = 40,
                Icon = AppBranding.WindowIcon,
                Style = WindowStyle.None,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                StartPosition = WindowStartPosition.Manual,
                ManualPosition = (-10000, -10000),
                Opacity = 0,
            },
            static () => new TaskbarVisualizerWindowComponent());
        if (settings.TaskbarVisualizerEnabled)
        {
            visualizer.Hide();
            Taskbar.AttachVisualizerWindow(visualizer);
        }
        else
        {
            HideTaskbarWindow(visualizerKey);
        }
    }

    private ReactorWindow GetOrCreateTaskbarWindow(WindowKey key, WindowSpec specification, Func<Component> rootFactory)
    {
        if (_windows.TryGet(key, out var existing) && existing is not null)
            return existing;
        return _windows.OpenOrActivate(key, specification, rootFactory);
    }

    private void HideTaskbarWindow(WindowKey key)
    {
        if (_windows.TryGet(key, out var window) && window is not null)
            window.Hide();
    }

    private void Exit()
    {
        if (Interlocked.Exchange(ref _exitRequested, 1) != 0)
            return;

        Environment.Exit(0);
    }

    /// <summary>
    /// Full ordered teardown, kept for explicit dispose paths. The normal exit
    /// flow intentionally skips it because <see cref="Exit"/> terminates the
    /// process directly.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _mediaUnsubscribe?.Invoke();
        _settingsUnsubscribe?.Invoke();
        _mediaFlyoutDismissTimer?.Stop();
        _mediaFlyoutDismissTimer = null;
        _lockKeysDismissTimer?.Stop();
        _lockKeysDismissTimer = null;
        _tray.Close();
        _keyboard.Dispose();
        Taskbar.Dispose();
        Visualizer.Dispose();
        Audio.Dispose();
        Media.Dispose();
        Updater.Dispose();
        Notifications.Dispose();
        _windows.Shutdown();
        Settings.Dispose();
        MediaStore.Dispose();
        VolumeStore.Dispose();
        LockKeyStore.Dispose();
        TaskbarStore.Dispose();
        VisualizerStore.Dispose();
        OnboardingStore.Dispose();
        LocalizationStore.Dispose();
        UpdateStore.Dispose();
        _singleInstance.Dispose();
        _logger.Shutdown();
    }
}
