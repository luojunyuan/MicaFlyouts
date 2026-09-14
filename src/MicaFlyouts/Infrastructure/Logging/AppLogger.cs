using System.Diagnostics;
using System.Globalization;

namespace MicaFlyouts.Infrastructure.Logging;

public sealed partial class AppLogger
{
    private readonly object _gate = new();
    private readonly string _logFile;
    private int _shutdown;

    public AppLogger(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MicaFlyouts",
            "logs");
        Directory.CreateDirectory(directory);
        _logFile = Path.Combine(directory, $"mica-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public string LogDirectory => Path.GetDirectoryName(_logFile) ?? string.Empty;

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (Volatile.Read(ref _shutdown) != 0)
            return;

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0:O} [{1}] {2}{3}",
            DateTimeOffset.UtcNow,
            level,
            message,
            exception is null ? string.Empty : $" {exception}");
        Debug.WriteLine(line);
        lock (_gate)
        {
            if (Volatile.Read(ref _shutdown) == 0)
                File.AppendAllText(_logFile, line + Environment.NewLine);
        }
    }

    public void Shutdown() => Interlocked.Exchange(ref _shutdown, 1);
}
