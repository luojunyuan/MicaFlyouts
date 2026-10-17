using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;

namespace MicaFlyouts.App;

internal static class AppBranding
{
    public const string Name = "Mica Flyouts";
    public const string Version = "215.0.0";
    public const string IconPath = "Assets\\MicaFlyouts.ico";
    public const string IconUri = "ms-appx:///Assets/MicaFlyouts.ico";
    public const string RepositoryUrl = "https://github.com/luojunyuan/MicaFlyouts";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    public const string IssuesUrl = RepositoryUrl + "/issues/new/choose";
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/luojunyuan/MicaFlyouts/releases/latest";

    public static WindowIcon WindowIcon { get; } = WindowIcon.FromPath(IconPath);
    public static ImageIconData TitleBarIcon { get; } = new(new Uri(IconUri));
}
