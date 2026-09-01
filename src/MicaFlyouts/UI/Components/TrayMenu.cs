using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.UI.Components;

public sealed record TrayMenuProps(
    Action Settings,
    Action Repository,
    Action ViewLogs,
    Action Quit);

/// <summary>
/// A directly mountable tray menu surface. MenuItems is a flyout-slot value
/// and cannot be used as the root passed to ReactorTrayIcon.ShowFlyout.
/// </summary>
public sealed class TrayMenu : Component<TrayMenuProps>
{
    public override Element Render()
        => Border(
                VStack(4,
                    MenuButton("Settings", "Setting", Props.Settings),
                    MenuButton("Repository", "Document", Props.Repository),
                    MenuButton("View logs", "Folder", Props.ViewLogs),
                    MenuButton("Quit", "Close", Props.Quit)))
            .Width(260)
            .Padding(8)
            .Background(LayerFill)
            .WithBorder(SurfaceStroke, 1)
            .CornerRadius(8);

    private static ButtonElement MenuButton(string text, string icon, Action onClick)
        => Button(HStack(10, Icon(icon), TextBlock(text)), onClick)
            .Width(244)
            .Height(40)
            .AutomationName(text);
}
