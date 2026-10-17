using MicaFlyouts.UI.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.UI.Components;

public static class IconButton
{
    public static Brush TransparentBackground { get; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public static ButtonElement Create(string glyph, string accessibleName, Action? onClick = null)
        => Button(Icon(FontIcon(glyph, fontSize: 16)), onClick)
            .Width(28)
            .Height(28)
            .AutomationName(accessibleName);

    public static ButtonElement Create(
        FluentSystemIcon icon,
        string accessibleName,
        Action? onClick = null,
        double size = 28,
        bool accented = false,
        bool isEnabled = true,
        double opacity = 1)
    {
        var button = Button(FluentSystemIcons.Create(icon, 18), onClick)
            .Width(size)
            .Height(size)
            .AutomationName(accessibleName)
            .Padding(new Thickness(0))
            .IsEnabled(isEnabled)
            .Opacity(opacity);
        return accented
            ? button.AccentButton()
            : button
                .Background(TransparentBackground)
                .BorderThickness(new Thickness(0))
                .CornerRadius(new CornerRadius(6));
    }
}
