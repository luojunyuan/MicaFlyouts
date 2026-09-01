using MicaFlyouts.Features.LockKeys;
using MicaFlyouts.Features.Media;
using MicaFlyouts.Features.Settings;
using MicaFlyouts.UI.Toolkit;
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
        var expander = SettingsExpander(header: "Group", items: new object[] { first, second });
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
    public void FeatureComponents_AreReactorComponentsWithSafeEmptyState()
    {
        Assert.IsAssignableFrom<Component>(new MediaFlyoutComponent());
        Assert.IsAssignableFrom<Component>(new LockKeysComponent());
        Assert.IsAssignableFrom<Component>(new SettingsWindowComponent());
        Assert.IsType<EmptyElement>(new MediaFlyoutComponent().Render());
    }
}
