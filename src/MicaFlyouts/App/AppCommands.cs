namespace MicaFlyouts.App;

public sealed class AppCommands
{
    public required Action ShowSettings { get; init; }
    public required Action ShowMediaFlyout { get; init; }
    public required Action ToggleMediaFlyout { get; init; }
    public required Action Exit { get; init; }
}
