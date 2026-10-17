using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Notifications;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("This scenario can only run on Windows.");
            return 1;
        }

        if (args.Length > 1 || (args.Length == 1 && args[0] is not "toast-demo" and not "--help" and not "-h"))
        {
            PrintUsage();
            return 2;
        }

        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        var logger = new AppLogger();
        Console.WriteLine("Showing MicaFlyouts runtime toast sequence.");
        Console.WriteLine($"Diagnostic log directory: {logger.LogDirectory}");

        using (var notifier = new RuntimeToastNotifier(logger))
        {
            notifier.Downloading(0, 100);
            foreach (long percent in new long[] { 15, 42, 73, 100 })
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(900));
                notifier.Downloading(percent, 100);
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(700));
            Console.WriteLine("Updating the progress toast to installation for five seconds.");
            notifier.Installing();
            Thread.Sleep(TimeSpan.FromSeconds(5));
            Console.WriteLine("Showing completion toast.");
            notifier.Completed();
        }

        Console.WriteLine("Toast sequence completed.");
        return 0;
    }

    private static void PrintUsage()
        => Console.WriteLine("Usage: MicaFlyouts.WasdkToastScenario [toast-demo]");
}
