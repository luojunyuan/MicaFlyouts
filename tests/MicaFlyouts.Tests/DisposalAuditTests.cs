using MicaFlyouts.App;
using MicaFlyouts.Features.Tray;
using MicaFlyouts.Infrastructure.Audio;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Media;
using MicaFlyouts.Infrastructure.Notifications;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using MicaFlyouts.Infrastructure.Updates;
using MicaFlyouts.Infrastructure.Windows;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed class DisposalAuditTests
{
    [Fact]
    public void NonResourceOwnersDoNotExposeDisposableContract()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(AppLogger)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(TrayIconFeature)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(WindowRegistry)));
    }

    [Fact]
    public void ResourceOwnersKeepDisposableContract()
    {
        Type[] resourceOwners =
        [
            typeof(AppServices),
            typeof(TaskbarHostService),
            typeof(SingleInstanceService),
            typeof(KeyboardHookService),
            typeof(UpdateCheckerService),
            typeof(NotificationService),
            typeof(MediaSessionService),
            typeof(AudioService),
            typeof(VisualizerService),
            typeof(WasapiLoopbackCaptureAdapter),
            typeof(IAudioLoopbackCapture),
            typeof(SettingsStore),
            typeof(StateStore<int>),
            typeof(MediaStore),
            typeof(VolumeStore),
            typeof(LockKeyStore),
            typeof(TaskbarStore),
            typeof(VisualizerStore),
            typeof(OnboardingStore),
            typeof(LocalizationStore),
            typeof(UpdateStore),
        ];

        Assert.All(resourceOwners, type =>
            Assert.True(typeof(IDisposable).IsAssignableFrom(type), type.FullName));
    }
}
