using Microsoft.Win32;

namespace MicaFlyouts.Infrastructure.Windows;

public static class StartupRegistration
{
    private const string RunKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "MicaFlyouts";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
                return;

            if (enabled && Environment.ProcessPath is { Length: > 0 } path)
                key.SetValue(ValueName, $"\"{path}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Startup integration is optional and must not prevent the app from running.
        }
    }
}
