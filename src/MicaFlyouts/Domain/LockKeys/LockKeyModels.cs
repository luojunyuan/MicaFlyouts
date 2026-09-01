using System.Text;

namespace MicaFlyouts.Domain.LockKeys;

public enum LockKeyKind
{
    CapsLock,
    NumLock,
    ScrollLock,
    Insert,
}

public readonly record struct KeyboardKeyEvent(int VirtualKey, bool IsKeyUp, long Timestamp);

public sealed record LockKeySnapshot(
    bool CapsLock,
    bool NumLock,
    bool ScrollLock,
    bool Insert)
{
    public bool this[LockKeyKind key] => key switch
    {
        LockKeyKind.CapsLock => CapsLock,
        LockKeyKind.NumLock => NumLock,
        LockKeyKind.ScrollLock => ScrollLock,
        LockKeyKind.Insert => Insert,
        _ => false,
    };
}

public sealed record LockKeyVisualState(
    double Opacity,
    double IndicatorWidth,
    double ShackleAngle,
    double ShackleBounceY);

public static class LockKeyLayout
{
    public static LockKeyVisualState VisualState(bool isOn) => new(
        isOn ? 1.0 : 0.2,
        isOn ? 60.0 : 36.0,
        isOn ? 0.0 : 25.0,
        0.0);

    public static double EstimateWidth(string? text, double fontSize = 14)
    {
        if (string.IsNullOrEmpty(text))
            return 160;

        double width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += rune.Value is >= 0x2E80 and <= 0x9FFF ? fontSize : fontSize * 0.58;
        }

        return Math.Max(160, Math.Ceiling(width + 56));
    }

    public static string StatusText(LockKeyKind key, bool isOn, bool insertPressed = false)
    {
        if (key == LockKeyKind.Insert || insertPressed)
            return "Insert pressed";

        var label = key switch
        {
            LockKeyKind.CapsLock => "Caps Lock",
            LockKeyKind.NumLock => "Num Lock",
            LockKeyKind.ScrollLock => "Scroll Lock",
            _ => "Insert",
        };
        return $"{label} {(isOn ? "On" : "Off")}";
    }
}
