using MicaFlyouts.App;
using MicaFlyouts.Features.LockKeys;
using MicaFlyouts.Features.Media;
using MicaFlyouts.Features.Onboarding;
using MicaFlyouts.Features.Settings;
using MicaFlyouts.Features.Taskbar;
using MicaFlyouts.Features.Tray;
using MicaFlyouts.Features.Volume;
using MicaFlyouts.UI.Toolkit;
using Kumo.H.NotifyIcon.Reactor;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static MicaFlyouts.UI.Toolkit.SettingsCardElement;
using static MicaFlyouts.UI.Toolkit.SettingsExpanderElement;
using static Microsoft.UI.Reactor.Factories;
using Xunit;

namespace MicaFlyouts.ReactorSelfTests;

public sealed class ReactorElementTreeTests
{
    [Fact]
    public void SettingsCardWrapper_ExposesPrimaryAndSecondarySlots()
    {
        var element = SettingsCard(
            header: "Media",
            description: "Playback",
            headerIcon: Icon("MusicInfo"),
            content: ToggleSwitch(true));

        Assert.Equal("Media", element.Header.Value);
        Assert.Equal("Playback", element.Description.Value);
        Assert.NotNull(element.HeaderIcon);
        Assert.NotNull(element.Content);
        Assert.IsType<SettingsCardElement>(element);
    }

    [Fact]
    public void SettingsExpanderWrapper_UsesStableObjectItems()
    {
        var first = SettingsCard(header: "One", content: ToggleSwitch(true));
        var second = SettingsCard(header: "Two", content: ToggleSwitch(false));
        var expander = SettingsExpander(header: "Group", items: [first, second]);
        Assert.Equal(2, expander.Items.Count);
        Assert.Same(first, expander.Items[0]);
        Assert.Same(second, expander.Items[1]);
    }

    [Fact]
    public void SettingsSearchIndex_MapsEveryRequiredRoute()
    {
        foreach (var page in Enum.GetValues<SettingsPage>())
        {
            Assert.True(SettingsSearchIndex.TryParse(SettingsSearchIndex.Tag(page), out var parsed));
            Assert.Equal(page, parsed);
        }
        Assert.Contains(SettingsSearchIndex.Query("keyboard"), entry => entry.Page == SettingsPage.LockKeys);
        Assert.Contains(SettingsSearchIndex.Query("audio"), entry => entry.Page == SettingsPage.Volume);
    }

    [Fact]
    public void FeatureComponents_AreReactorComponents()
    {
        Assert.IsType<Component>(new MediaFlyoutComponent(), exactMatch: false);
        Assert.IsType<Component>(new LockKeysComponent(), exactMatch: false);
        Assert.IsType<Component>(new SettingsWindowComponent(), exactMatch: false);
    }

    [Fact]
    public void FeatureComponents_RenderWithoutRuntime_Throws()
    {
        Component[] components =
        [
            new LockKeysComponent(),
            new MediaFlyoutComponent(),
            new OnboardingComponent(),
            new SettingsWindowComponent(),
            new SettingsPageComponent(),
            new TaskbarWidgetComponent(),
            new TaskbarVisualizerComponent(),
            new TrayIconComponent(),
            new VolumeFlyoutComponent(),
            new VolumeMixerComponent(),
        ];

        foreach (var component in components)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => component.Render());
            Assert.Equal("AppRuntime is not running.", exception.Message);
        }
    }

    [Fact]
    public void TrayIcon_IsMountedInAHiddenHostWindowByTheFeature()
    {
        var assembly = typeof(AppServices).Assembly;
        var featureType = assembly.GetType("MicaFlyouts.Features.Tray.TrayIconFeature");
        var componentType = assembly.GetType("MicaFlyouts.Features.Tray.TrayIconComponent");

        Assert.NotNull(featureType);
        Assert.NotNull(componentType);
        Assert.True(typeof(IDisposable).IsAssignableFrom(featureType));
        Assert.True(typeof(HNotifyComponent).IsAssignableFrom(componentType));

        // The tray unit follows the Kumo component sample: a never-shown host
        // window owned by the feature (no ReactorHostControl mount).
        var hostField = featureType.GetField(
            "_hostWindow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(hostField);
        Assert.Equal(typeof(ReactorWindow), hostField.FieldType);
        Assert.Null(assembly.GetType("MicaFlyouts.Features.Tray.TrayIconHost"));
        Assert.Contains(
            typeof(HNotifyComponent).GetMethods(
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic),
            method => method.Name == "UseTrayIcon");
    }

    [Fact]
    public void TrayIcon_IsLoadedFromAnEmbeddedResource()
    {
        const string resourceName = "MicaFlyouts.Assets.MicaFlyouts.ico";
        var assembly = typeof(AppServices).Assembly;

        Assert.Contains(resourceName, assembly.GetManifestResourceNames());
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }
}
