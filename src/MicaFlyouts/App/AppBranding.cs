using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;

namespace MicaFlyouts.App;

internal static class AppBranding
{
    public const string Name = "Mica Flyouts";
    public const string IconPath = "Assets\\MicaFlyouts.ico";
    public const string IconUri = "ms-appx:///Assets/MicaFlyouts.ico";

    public static WindowIcon WindowIcon { get; } = WindowIcon.FromPath(IconPath);
    public static ImageIconData TitleBarIcon { get; } = new(new Uri(IconUri));
}
