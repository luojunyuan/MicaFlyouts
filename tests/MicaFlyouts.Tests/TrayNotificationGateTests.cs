using System.Diagnostics;
using MicaFlyouts.App;
using Xunit;

namespace MicaFlyouts.Tests;

public sealed class TrayNotificationGateTests
{
    [Fact]
    public void SuppressesSameTypeNotificationsWithinDuplicateWindow()
    {
        var gate = new TrayNotificationGate();
        long first = Stopwatch.Frequency;

        Assert.True(gate.TryAccept(first));
        Assert.False(gate.TryAccept(first + Stopwatch.Frequency / 20));
        Assert.True(gate.TryAccept(first + Stopwatch.Frequency / 20 + Stopwatch.Frequency / 10 + 1));
    }
}
