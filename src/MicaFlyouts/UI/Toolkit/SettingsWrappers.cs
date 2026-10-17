using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Wrappers;

namespace MicaFlyouts.UI.Toolkit;

[GenerateReactorWrapper(
    typeof(SettingsCard),
    Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
[WrapElementSlot("ActionIcon")]
public partial record SettingsCardElement;

[GenerateReactorWrapper(typeof(SettingsExpander))]
[WrapElementSlot("HeaderIcon")]
[WrapElementSlot("Content")]
[WrapElementSlot("ItemsHeader")]
public partial record SettingsExpanderElement;
