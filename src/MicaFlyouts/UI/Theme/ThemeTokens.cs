using Microsoft.UI.Reactor.Core;
using ReactorTheme = Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.UI.Theme;

public static class ThemeTokens
{
    public static readonly ThemeRef Surface = ReactorTheme.LayerFill;
    public static readonly ThemeRef SurfaceStroke = ReactorTheme.SurfaceStroke;
    public static readonly ThemeRef Card = ReactorTheme.CardBackground;
    public static readonly ThemeRef CardStroke = ReactorTheme.CardStroke;
    public static readonly ThemeRef PrimaryText = ReactorTheme.PrimaryText;
    public static readonly ThemeRef SecondaryText = ReactorTheme.SecondaryText;
    public static readonly ThemeRef Accent = ReactorTheme.Accent;
}
