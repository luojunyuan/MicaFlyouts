using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

if (args is ["--inspect-shell"])
{
    ShellTrayIconLocator.PrepareForDetection();
    ShellTrayIconLocator.Dump();
    return 0;
}

if (args is ["--close-overflow"])
{
    ShellTrayIconLocator.CloseOverflow();
    return 0;
}

var options = Options.Parse(args);
if (options is null)
    return 0;

if (!File.Exists(options.Executable))
    throw new FileNotFoundException("Executable not found.", options.Executable);

string processName = Path.GetFileNameWithoutExtension(options.Executable);
bool overflowWasOpen = ShellTrayIconLocator.IsOverflowOpen();
ShellTrayIconLocator.PrepareForDetection();
if (ShellTrayIconLocator.HasTrayButton(options.Name))
    throw new InvalidOperationException($"A {options.Name} tray button is already present. Close the existing app before measuring it.");

Process[] existingProcesses = Process.GetProcessesByName(processName);
if (existingProcesses.Length > 0)
{
    foreach (Process existingProcess in existingProcesses)
        existingProcess.Dispose();
    throw new InvalidOperationException($"Close the existing {options.Name} process before measuring it.");
}

var samples = new List<double>(options.Runs);
Console.WriteLine($"Measuring {options.Name}: {options.Runs} runs, Explorer UI Automation endpoint");
Console.WriteLine(options.Executable);

for (int run = 1; run <= options.Runs; run++)
{
    if (!ShellTrayIconLocator.WaitUntilNoTrayButton(options.Name, 5_000))
        throw new InvalidOperationException($"The {options.Name} tray button remained before the next sample.");
    ShellTrayIconLocator.PrepareForDetection();
    var startInfo = new ProcessStartInfo(options.Executable)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(options.Executable)!,
    };
    Process? process = null;
    var stopwatch = Stopwatch.StartNew();
    try
    {
        process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Process.Start returned no process.");

        bool ready = WaitForTrayButton(process, options.Name, options.TimeoutMilliseconds);

        if (!ready)
        {
            throw new TimeoutException(
                $"{options.Name} did not reach its tray-ready endpoint within {options.TimeoutMilliseconds} ms.");
        }

        stopwatch.Stop();
        samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        Console.WriteLine($"{run,2}: {samples[^1],8:F1} ms");
    }
    finally
    {
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
            catch (InvalidOperationException)
            {
            }
            process.Dispose();
        }

        Thread.Sleep(250);
    }
}

double[] ordered = samples.Order().ToArray();
double median = ordered.Length % 2 == 0
    ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2
    : ordered[ordered.Length / 2];
Console.WriteLine($"Average: {samples.Average():F1} ms");
Console.WriteLine($"Median:  {median:F1} ms");
Console.WriteLine($"Min/Max: {samples.Min():F1} / {samples.Max():F1} ms");
if (!overflowWasOpen)
    ShellTrayIconLocator.CloseOverflow();
return 0;

static bool WaitForTrayButton(Process process, string appName, int timeoutMilliseconds)
{
    var stopwatch = Stopwatch.StartNew();
    while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
    {
        if (process.HasExited)
            throw new InvalidOperationException($"Target process exited with code {process.ExitCode}.");
        if (ShellTrayIconLocator.HasTrayButton(appName))
            return true;
        Thread.Sleep(20);
    }
    return false;
}

internal sealed record Options(
    string Executable,
    string Name,
    int Runs,
    int TimeoutMilliseconds)
{
    private const int DefaultRuns = 20;
    private const int DefaultTimeoutMilliseconds = 30_000;

    public static Options? Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: dotnet run --project tests/StartupTiming -- --exe <path> --name <label> [--runs 20] [--timeout-ms 30000]");
            Console.WriteLine("The tray button is detected through Explorer UI Automation.");
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new ArgumentException($"Unexpected argument: {key}");
            values[key] = args[++index];
        }

        string executable = Required("--exe");
        string name = values.GetValueOrDefault("--name", Path.GetFileNameWithoutExtension(executable));
        int runs = ParsePositive("--runs", DefaultRuns);
        int timeout = ParsePositive("--timeout-ms", DefaultTimeoutMilliseconds);
        return new Options(Path.GetFullPath(executable), name, runs, timeout);

        string Required(string key) => values.TryGetValue(key, out string? value)
            ? value
            : throw new ArgumentException($"Missing required option {key}.");

        int ParsePositive(string key, int defaultValue)
        {
            if (!values.TryGetValue(key, out string? value))
                return defaultValue;
            return int.TryParse(value, out int parsed) && parsed > 0
                ? parsed
                : throw new ArgumentException($"{key} must be a positive integer.");
        }
    }
}

internal static class ShellTrayIconLocator
{
    private static readonly HashSet<string> TrayWindowClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "SystemTray_Main",
        "TopLevelWindowForOverflowXamlIsland",
        "NotifyIconOverflowWindow",
    };

    public static bool HasTrayButton(string appName)
    {
        string expectedName = Normalize(appName);
        foreach (IntPtr window in FindTrayWindows())
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(window);
                AutomationElementCollection buttons = root.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                foreach (AutomationElement button in buttons)
                {
                    string actualName = Normalize(button.Current.Name);
                    if (actualName.Contains(expectedName, StringComparison.Ordinal))
                        return true;
                }
            }
            catch (ElementNotAvailableException)
            {
            }
        }
        return false;
    }

    public static bool WaitUntilNoTrayButton(string appName, int timeoutMilliseconds)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (!HasTrayButton(appName))
                return true;
            Thread.Sleep(50);
        }
        return !HasTrayButton(appName);
    }

    public static bool PrepareForDetection()
    {
        if (IsOverflowOpen())
            return true;

        IntPtr taskbar = FindTopLevelWindow("Shell_TrayWnd");
        if (taskbar == IntPtr.Zero)
            return false;

        AutomationElement root = AutomationElement.FromHandle(taskbar);
        AutomationElementCollection buttons = root.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement button in buttons)
        {
            string name = button.Current.Name;
            if (!IsOverflowButtonName(name))
                continue;
            if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
                continue;

            ((InvokePattern)pattern).Invoke();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 1_500)
            {
                if (IsOverflowOpen())
                    return true;
                Thread.Sleep(20);
            }
            return false;
        }
        return false;
    }

    public static bool IsOverflowOpen() =>
        FindTopLevelWindow("TopLevelWindowForOverflowXamlIsland") != IntPtr.Zero
        || FindTopLevelWindow("NotifyIconOverflowWindow") != IntPtr.Zero;

    public static void CloseOverflow()
    {
        if (!IsOverflowOpen())
            return;

        IntPtr taskbar = FindTopLevelWindow("Shell_TrayWnd");
        if (taskbar == IntPtr.Zero)
            return;

        AutomationElement root = AutomationElement.FromHandle(taskbar);
        AutomationElementCollection buttons = root.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement button in buttons)
        {
            if (!IsOverflowButtonName(button.Current.Name)
                || !button.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
                continue;
            ((InvokePattern)pattern).Invoke();
            return;
        }
    }

    private static bool IsOverflowButtonName(string name) =>
        name.Contains("hidden icons", StringComparison.OrdinalIgnoreCase)
        || name.Contains("\u9690\u85cf\u7684\u56fe\u6807", StringComparison.OrdinalIgnoreCase);

    public static void Dump()
    {
        foreach (IntPtr window in FindTrayWindows())
        {
            Console.WriteLine($"{GetClassName(window)} ({window})");
            try
            {
                AutomationElement root = AutomationElement.FromHandle(window);
                AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement element in elements)
                {
                    AutomationElement.AutomationElementInformation info = element.Current;
                    Console.WriteLine($"  {info.ControlType.ProgrammaticName} '{info.Name}'");
                }
            }
            catch (ElementNotAvailableException)
            {
                Console.WriteLine("  UI Automation element is unavailable.");
            }
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static List<IntPtr> FindTrayWindows()
    {
        var windows = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            if (TrayWindowClasses.Contains(GetClassName(window)))
                windows.Add(window);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static IntPtr FindTopLevelWindow(string className)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (GetClassName(window) == className)
            {
                found = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static string GetClassName(IntPtr window)
    {
        var buffer = new char[128];
        int length = GetClassName(window, buffer, buffer.Length);
        return length == 0 ? string.Empty : new string(buffer, 0, length);
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr window, [Out] char[] className, int maxCount);
}
