using MicaFlyouts.Infrastructure.Audio;
using MicaFlyouts.Infrastructure.Localization;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Media;
using MicaFlyouts.Infrastructure.Notifications;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using MicaFlyouts.Infrastructure.Updates;
using MicaFlyouts.Infrastructure.Windows;

namespace MicaFlyouts.App;

public static class Bootstrap
{
    public static AppServices Create(SingleInstanceService singleInstance)
    {
        var logger = new AppLogger();
        var settings = new SettingsStore(new JsonSettingsRepository(), logger);
        var mediaStore = new MediaStore();
        var volumeStore = new VolumeStore();
        var lockKeyStore = new LockKeyStore();
        var taskbarStore = new TaskbarStore();
        var visualizerStore = new VisualizerStore();
        var onboardingStore = new OnboardingStore();
        var updateStore = new UpdateStore();
        var localizationStore = new LocalizationStore("en-US");
        var dispatcher = new UiDispatcher();
        var monitors = new MonitorService();
        var fullscreen = new FullscreenService();
        var localization = new LocalizationService(settings, localizationStore);
        var resolver = new MediaPlayerResolver(logger);
        var media = new MediaSessionService(settings, mediaStore, dispatcher, fullscreen, resolver, logger);
        var audio = new AudioService(settings, volumeStore, dispatcher, logger);
        var visualizer = new VisualizerService(settings, visualizerStore, dispatcher, logger);
        var taskbar = new TaskbarHostService(settings, mediaStore, taskbarStore, dispatcher, monitors, logger);
        var updater = new UpdateCheckerService(updateStore, logger);

        return new AppServices(
            singleInstance,
            dispatcher,
            logger,
            settings,
            mediaStore,
            volumeStore,
            lockKeyStore,
            taskbarStore,
            visualizerStore,
            onboardingStore,
            localizationStore,
            updateStore,
            localization,
            media,
            audio,
            visualizer,
            taskbar,
            updater,
            monitors,
            fullscreen,
            new NotificationService(logger));
    }
}
