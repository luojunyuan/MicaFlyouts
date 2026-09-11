using System.Drawing;

namespace MicaFlyouts.App;

public static class TrayIconAssets
{
    public const string ColorResource = "MicaFlyouts.Assets.MicaFlyouts.ico";
    public const string BlackResource = "MicaFlyouts.Assets.TrayIcons.FluentFlyoutBlack.ico";
    public const string WhiteResource = "MicaFlyouts.Assets.TrayIcons.FluentFlyoutWhite.ico";

    public static string SelectResource(bool useSymbol, bool systemUsesLightTheme) =>
        !useSymbol ? ColorResource : systemUsesLightTheme ? BlackResource : WhiteResource;

    public static Icon Load(string resourceName)
    {
        using var stream = typeof(TrayIconAssets).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded tray icon resource was not found: {resourceName}");
        return new Icon(stream);
    }
}
