using System.Globalization;
using System.Runtime.InteropServices;

namespace MicaFlyouts.Updater;

internal static partial class Program
{
    private const string InstanceName = "Local\\MicaFlyouts.Updater";

    private static int Main(string[] args)
    {
        try
        {
            UpdaterCommand command = ParseCommandLine(args);
            switch (command)
            {
                case UpdaterCommand.Ignore:
                    return 0;
                case UpdaterCommand.InstallRuntime installRuntime:
                    using (AcquireUpdaterInstance())
                        UpdateInstaller.InstallRuntime(installRuntime.InstallerPath);
                    return 0;
                case UpdaterCommand.InstallUpdate installUpdate:
                    using (AcquireUpdaterInstance())
                    {
                        try
                        {
                            UpdateInstaller.InstallUpdate(installUpdate.ArchivePath, installUpdate.ParentProcessId);
                        }
                        catch
                        {
                            UpdateInstaller.TryLaunchApplication(UpdateInstaller.GetInstallDirectory());
                            throw;
                        }
                    }
                    return 0;
                case UpdaterCommand.ApplyUpdate applyUpdate:
                    UpdateInstaller.ApplyUpdate(
                        applyUpdate.PackageDirectory,
                        applyUpdate.InstallDirectory,
                        applyUpdate.ParentProcessId);
                    return 0;
                default:
                    throw new UpdaterException("Unsupported updater command.");
            }
        }
        catch (Exception exception)
        {
            ShowFatalError(exception.Message);
            return 1;
        }
    }

    private static UpdaterCommand ParseCommandLine(string[] args)
    {
        if (args.Length == 0)
            return new UpdaterCommand.Ignore();

        if (args[0] == "--apply-update")
        {
            if (args.Length != 4)
                throw new UpdaterException("--apply-update requires a package directory, install directory, and parent process ID.");

            return new UpdaterCommand.ApplyUpdate(
                args[1],
                args[2],
                ParseProcessId(args[3]));
        }

        bool fromApp = false;
        string? installerPath = null;
        string? archivePath = null;
        int? waitProcessId = null;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--from-app":
                    if (fromApp)
                        throw new UpdaterException("--from-app may only be specified once.");
                    fromApp = true;
                    break;
                case "--install-runtime":
                    if (installerPath is not null)
                        throw new UpdaterException("--install-runtime may only be specified once.");
                    installerPath = ReadValue(args, ref index, "--install-runtime");
                    break;
                case "--install-update":
                    if (archivePath is not null)
                        throw new UpdaterException("--install-update may only be specified once.");
                    archivePath = ReadValue(args, ref index, "--install-update");
                    break;
                case "--wait-pid":
                    if (waitProcessId is not null)
                        throw new UpdaterException("--wait-pid may only be specified once.");
                    waitProcessId = ParseProcessId(ReadValue(args, ref index, "--wait-pid"));
                    break;
                default:
                    throw new UpdaterException($"Unknown argument: {args[index]}");
            }
        }

        if (installerPath is not null && archivePath is not null)
            throw new UpdaterException("--install-runtime and --install-update cannot be used together.");

        if (archivePath is not null)
        {
            if (!fromApp || waitProcessId is null || installerPath is not null)
                throw new UpdaterException("--install-update requires --from-app and --wait-pid, and cannot be combined with --install-runtime.");
            return new UpdaterCommand.InstallUpdate(archivePath, waitProcessId.Value);
        }

        if (installerPath is not null)
        {
            if (!fromApp || waitProcessId is not null)
                throw new UpdaterException("--install-runtime requires --from-app and cannot be combined with --wait-pid.");
            return new UpdaterCommand.InstallRuntime(installerPath);
        }

        throw new UpdaterException("--from-app and --wait-pid must be used with an updater operation.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new UpdaterException($"{option} requires a value.");
        return args[++index];
    }

    private static int ParseProcessId(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int processId) || processId <= 0)
            throw new UpdaterException($"Invalid process ID: {value}");
        return processId;
    }

    private static Mutex AcquireUpdaterInstance()
    {
        var mutex = new Mutex(initiallyOwned: false, InstanceName);
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (acquired)
            return mutex;

        mutex.Dispose();
        throw new UpdaterException("MicaFlyouts updater is already running.");
    }

    private static void ShowFatalError(string message)
        => _ = MessageBox(0, $"MicaFlyouts could not be updated.\n\n{message}", "MicaFlyouts Updater", 0x10);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint window, string text, string caption, uint type);
}

internal abstract record UpdaterCommand
{
    internal sealed record Ignore : UpdaterCommand;
    internal sealed record InstallRuntime(string InstallerPath) : UpdaterCommand;
    internal sealed record InstallUpdate(string ArchivePath, int ParentProcessId) : UpdaterCommand;
    internal sealed record ApplyUpdate(string PackageDirectory, string InstallDirectory, int ParentProcessId) : UpdaterCommand;
}

internal sealed class UpdaterException(string message, Exception? innerException = null)
    : Exception(message, innerException);
