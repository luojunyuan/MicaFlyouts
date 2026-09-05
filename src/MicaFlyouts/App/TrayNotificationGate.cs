namespace MicaFlyouts.App;

internal sealed class TrayNotificationGate
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMilliseconds(100);
    private long _lastTimestamp;

    public bool TryAccept()
        => TryAccept(System.Diagnostics.Stopwatch.GetTimestamp());

    internal bool TryAccept(long timestamp)
    {
        long previous = Interlocked.Exchange(ref _lastTimestamp, timestamp);
        return previous == 0
            || System.Diagnostics.Stopwatch.GetElapsedTime(previous, timestamp) >= DuplicateWindow;
    }
}
