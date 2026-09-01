namespace MicaFlyouts.Domain;

public readonly record struct ScreenRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public static ScreenRect Empty => new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect Empty => new(0, 0, 0, 0);
}
