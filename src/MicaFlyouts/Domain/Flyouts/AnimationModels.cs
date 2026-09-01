namespace MicaFlyouts.Domain.Flyouts;

public enum FlyoutEasing
{
    Linear,
    Sine,
    Quadratic,
    Cubic,
}

public readonly record struct AnimationSpec(
    TimeSpan Duration,
    FlyoutEasing Easing,
    bool EaseOut);

public static class FlyoutAnimationRules
{
    public static TimeSpan DurationForSpeed(int speed) => speed switch
    {
        <= 0 => TimeSpan.Zero,
        1 => TimeSpan.FromMilliseconds(150),
        2 => TimeSpan.FromMilliseconds(300),
        3 => TimeSpan.FromMilliseconds(450),
        4 => TimeSpan.FromMilliseconds(600),
        _ => TimeSpan.FromMilliseconds(900),
    };

    public static AnimationSpec Create(int speed, int easing, bool easeOut) => new(
        DurationForSpeed(speed),
        Enum.IsDefined((FlyoutEasing)Math.Clamp(easing, 0, 3))
            ? (FlyoutEasing)Math.Clamp(easing, 0, 3)
            : FlyoutEasing.Cubic,
        easeOut);
}
