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
using MicaFlyouts.UI.Components;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MicaFlyouts.UI.Animation;
using static Microsoft.UI.Reactor.Factories;
using MicaFlyouts.Features.LockKeys;
using MicaFlyouts.Features.Media;
using MicaFlyouts.Features.NextUp;
using MicaFlyouts.Features.Onboarding;
using MicaFlyouts.Features.Settings;
using MicaFlyouts.Features.Taskbar;
using MicaFlyouts.Features.Volume;

namespace MicaFlyouts.App;

public sealed class AppServices : IDisposable
{
    private readonly SingleInstanceService _singleInstance;
    private readonly UiDispatcher _dispatcher;
    private readonly AppLogger _logger;
    private readonly WindowRegistry _windows = new();
    private readonly KeyboardHookService _keyboard;
    private Action? _mediaUnsubscribe;
    private Action? _settingsUnsubscribe;
    private ReactorTrayIcon? _tray;
    private ReactorWindow? _mainWindow;
    private string _lastTrack = string.Empty;
    private readonly TrayNotificationGate _trayClickGate = new();
    private readonly TrayNotificationGate _trayRightClickGate = new();
    private int _started;
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
        NotificationService notifications)
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

    public void OpenMainWindow()
    {
        if (_mainWindow is not null)
            return;
        _mainWindow = _windows.OpenOrActivate(
            new WindowKey("main-media"),
            new WindowSpec
            {
                Title = "Mica Flyouts",
                Width = 310,
                Height = 116,
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
                Backdrop = BackdropChoice.Of(BackdropKind.DesktopAcrylic),
            },
            static () => new MediaFlyoutWindowComponent());
        _mainWindow.Hide();
    }

    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0
            || Interlocked.Exchange(ref _started, 1) != 0)
            return;

        ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;
        _ = ReactorApp.TryRegisterControlAssembly(typeof(SettingsCard).Assembly);
        StartupRegistration.Apply(Settings.Snapshot.Startup);
        _singleInstance.StartSettingsListener(_dispatcher, () =>
        {
            _logger.Info("Received a settings request from a second instance.");
            OpenSettings();
            _logger.Info("Settings window opened for the second instance.");
        });
        Notifications.Initialize();
        _mediaUnsubscribe = MediaStore.Subscribe(OnMediaChanged);
        _settingsUnsubscribe = Settings.Subscribe(OnSettingsChanged);
        Media.Start();
        Audio.Start();
        Visualizer.Start();
        Taskbar.Start();
        _keyboard.Start();

        if (!Settings.Snapshot.NIconHide)
        {
            try
            {
                var trayIconPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "MicaFlyouts.ico");
                if (!File.Exists(trayIconPath))
                    _logger.Warn($"Tray icon file was not found: {trayIconPath}");

                _tray = ReactorApp.OpenTrayIcon(new TrayIconSpec(
                    WindowIcon.FromPath(trayIconPath),
                    "Mica Flyouts",
                    new WindowKey("tray")));
                _tray.Click += Tray_Click;
                _tray.RightClick += Tray_RightClick;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Tray icon unavailable: {exception.Message}");
            }
        }

        SyncTaskbarWindows();
        bool firstRun = string.IsNullOrWhiteSpace(Settings.Snapshot.LastKnownVersion);
        if (firstRun)
        {
            Settings.Update(settings => settings with { LastKnownVersion = "v1.0.0" }, save: false);
            OpenOnboarding();
        }
    }

    private void Tray_Click(object? sender, EventArgs args)
    {
        // Shell can deliver multiple notification aliases for one gesture;
        // keep the user command idempotent at the application boundary.
        if (!_trayClickGate.TryAccept())
            return;

        if (Settings.Snapshot.NIconLeftClick == 1)
            ShowMediaFlyout();
        else
            OpenSettings();
    }

    private void Tray_RightClick(object? sender, EventArgs args)
    {
        if (!_trayRightClickGate.TryAccept())
            return;

        if (_tray is null)
            return;
        _tray.ShowFlyout(Component<TrayMenu, TrayMenuProps>(new(
            OpenSettings,
            () => OpenUrl("https://github.com/unchihugo/FluentFlyout"),
            () => OpenUrl($"file:///{_logger.LogDirectory.Replace('\\', '/') }"),
            Exit)));
    }

    private static void OpenUrl(string url)
    {
        try
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private void OnSettingsChanged()
    {
        UiDispatcher.EnqueueOrRun(() =>
        {
            Localization.Apply();
            StartupRegistration.Apply(Settings.Snapshot.Startup);
            SyncTaskbarWindows();
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
        else if (current is not null && current.Track.PlaybackStatus == MediaPlaybackStatus.Playing
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
        if (!keyEvent.IsKeyUp || !settings.LockKeysEnabled || FullscreenService.IsFullscreenApplicationRunning())
            return;

        bool enabled = keyEvent.VirtualKey switch
        {
            KeyboardHookService.CapsLock => settings.LockKeysCapsEnabled,
            KeyboardHookService.NumLock => settings.LockKeysNumEnabled,
            KeyboardHookService.ScrollLock => settings.LockKeysScrollEnabled,
            KeyboardHookService.Insert => settings.LockKeysInsertEnabled,
            _ => false,
        };
        if (!enabled)
            return;
        UiDispatcher.EnqueueOrRun(() =>
        {
            LockKeyStore.Set(KeyboardHookService.ReadSnapshot());
            OpenLockKeys();
        });
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
                MinWidth = 750,
                MinHeight = 300,
                StartPosition = WindowStartPosition.CenterOnCurrent,
                CornerStyle = WindowCornerStyle.Rounded,
                Backdrop = BackdropChoice.Of(BackdropKind.Mica),
            },
            static () => new SettingsWindowComponent());
        _logger.Info("Settings window request completed.");
    }

    public void ShowMediaFlyout()
    {
        if (_mainWindow is null || MediaStore.Snapshot.ActiveSession is null)
            return;
        _mainWindow.Show();
        WindowAnimationCoordinator.Show(_mainWindow, Settings.Snapshot);
    }

    public void ToggleMediaFlyout()
    {
        if (_mainWindow?.IsVisible == true)
        {
            WindowAnimationCoordinator.Hide(_mainWindow, Settings.Snapshot);
            return;
        }
        ShowMediaFlyout();
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

    private void OpenLockKeys()
        => _windows.OpenOrActivate(
            new WindowKey("lock-keys"),
            new WindowSpec
            {
                Title = "Lock Keys",
                Width = 160,
                Height = 50,
                Style = WindowStyle.None,
                Level = WindowLevel.AlwaysOnTop,
                ResizeMode = WindowResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowInSwitcher = false,
                NoActivate = true,
                CornerStyle = WindowCornerStyle.RoundedSmall,
                Backdrop = BackdropChoice.Of(BackdropKind.DesktopAcrylic),
            },
            static () => new LockKeysWindowComponent());

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
                StartPosition = WindowStartPosition.CenterOnCurrent,
                CornerStyle = WindowCornerStyle.Rounded,
                Backdrop = BackdropChoice.Of(BackdropKind.Mica),
            },
            static () => new OnboardingWindowComponent());

    private void SyncTaskbarWindows()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        var settings = Settings.Snapshot;
        if (settings.TaskbarWidgetEnabled)
        {
            var widget = _windows.OpenOrActivate(
                new WindowKey("taskbar-widget"),
                new WindowSpec
                {
                    Title = "Mica Flyouts Taskbar Widget",
                    Width = 100,
                    Height = 40,
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
            widget.Hide();
            Taskbar.AttachWidgetWindow(widget);
        }
        else
        {
            _windows.Close(new WindowKey("taskbar-widget"));
        }

        if (settings.TaskbarVisualizerEnabled)
        {
            var visualizer = _windows.OpenOrActivate(
                new WindowKey("taskbar-visualizer"),
                new WindowSpec
                {
                    Title = "Mica Flyouts Visualizer",
                    Width = 84,
                    Height = 40,
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
            visualizer.Hide();
            Taskbar.AttachVisualizerWindow(visualizer);
        }
        else
        {
            _windows.Close(new WindowKey("taskbar-visualizer"));
        }
    }

    private void Exit() => ReactorApp.Exit();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _mediaUnsubscribe?.Invoke();
        _settingsUnsubscribe?.Invoke();
        _keyboard.Dispose();
        Taskbar.Dispose();
        Visualizer.Dispose();
        Audio.Dispose();
        Media.Dispose();
        Updater.Dispose();
        Notifications.Dispose();
        _tray?.Dispose();
        _windows.Dispose();
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
        _logger.Dispose();
    }
}
