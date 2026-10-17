using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Reactor.Core.V1Protocol.Descriptor;
using Microsoft.UI.Reactor.Wrappers;
using Microsoft.UI.Xaml;

namespace MicaFlyouts.UI.Toolkit;

[GenerateReactorWrapper(
    typeof(SettingsCard),
    Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
[WrapElementSlot("ActionIcon")]
public partial record SettingsCardElement;

[GenerateReactorWrapper(typeof(SettingsExpander))]
[WrapElementSlot("HeaderIcon")]
[WrapManual("Content")]
[WrapManual("ItemsHeader")]
public partial record SettingsExpanderElement
{
    public Microsoft.UI.Reactor.Core.Element? Content { get; init; }
    public Microsoft.UI.Reactor.Core.Element? ItemsHeader { get; init; }

    private static partial ControlDescriptor<SettingsExpanderElement, SettingsExpander> Customize(
        ControlDescriptor<SettingsExpanderElement, SettingsExpander> d)
    {
        return d.ImperativeBridged(
            mount: static (ctx, control, element) =>
            {
                if (element.Content is not null)
                    control.Content = ctx.MountChild(element.Content)!;
            },
            update: static (ctx, control, previous, next) =>
            {
                if (previous.Content is null && next.Content is null)
                    return;

                if (next.Content is null)
                {
                    if (previous.Content is not null)
                        ctx.ReconcileChild(previous.Content, null, control.Content as UIElement);
                    control.Content = null!;
                    return;
                }

                var existing = control.Content as UIElement;
                var mounted = ctx.ReconcileChild(previous.Content, next.Content, existing);
                if (!ReferenceEquals(existing, mounted))
                    control.Content = mounted!;
            })
        .ImperativeBridged(
            mount: static (ctx, control, element) =>
            {
                if (element.ItemsHeader is not null)
                    control.ItemsHeader = ctx.MountChild(element.ItemsHeader)!;
            },
            update: static (ctx, control, previous, next) =>
            {
                if (previous.ItemsHeader is null && next.ItemsHeader is null)
                    return;

                if (next.ItemsHeader is null)
                {
                    if (previous.ItemsHeader is not null)
                        ctx.ReconcileChild(previous.ItemsHeader, null, control.ItemsHeader as UIElement);
                    control.ItemsHeader = null!;
                    return;
                }

                var existing = control.ItemsHeader as UIElement;
                var mounted = ctx.ReconcileChild(previous.ItemsHeader, next.ItemsHeader, existing);
                if (!ReferenceEquals(existing, mounted))
                    control.ItemsHeader = mounted!;
            });
    }
}
