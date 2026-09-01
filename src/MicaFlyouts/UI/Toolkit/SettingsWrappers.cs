using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Wrappers;

namespace MicaFlyouts.UI.Toolkit;

[GenerateReactorWrapper(
    typeof(SettingsCard),
    Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
public partial record SettingsCardElement;

[GenerateReactorWrapper(typeof(SettingsExpander))]
public partial record SettingsExpanderElement;
