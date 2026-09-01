using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.UI.Components;

public static class IconButton
{
    public static ButtonElement Create(string glyph, string accessibleName, Action? onClick = null)
        => Button(Icon(FontIcon(glyph, fontSize: 16)), onClick)
            .Width(28)
            .Height(28)
            .AutomationName(accessibleName);
}
