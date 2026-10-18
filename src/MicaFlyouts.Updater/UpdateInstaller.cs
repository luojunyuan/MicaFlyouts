using System.Diagnostics;
using System.IO.Compression;

namespace MicaFlyouts.Updater;

internal static class UpdateInstaller
{
    private const string ApplicationFileName = "MicaFlyouts.exe";
    private const string UpdaterFileName = "Updater.exe";
    private const string BootstrapFileName = "Microsoft.WindowsAppRuntime.Bootstrap.dll";
    private static readonly string[] RequiredFiles = [ApplicationFileName, UpdaterFileName, BootstrapFileName];

    internal static string GetInstallDirectory()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new UpdaterException("Could not determine the updater executable path.");
        return Path.GetDirectoryName(Path.GetFullPath(executablePath))
            ?? throw new UpdaterException("The updater executable has no parent directory.");
    }

    internal static void InstallRuntime(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new UpdaterException("The Windows App Runtime installer does not exist.");

        Span<byte> header = stackalloc byte[2];
        using (FileStream stream = File.OpenRead(installerPath))
        {
            try
            {
                stream.ReadExactly(header);
            }
            catch (EndOfStreamException exception)
            {
                throw new UpdaterException("The runtime installer is not a valid Windows executable.", exception);
            }
        }

        if (!header.SequenceEqual("MZ"u8))
            throw new UpdaterException("The runtime installer is not a valid Windows executable.");

        var startInfo = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--quiet");

        using Process process = Process.Start(startInfo)
            ?? throw new UpdaterException("Could not start the Windows App Runtime installer.");
        process.WaitForExit();
        if (process.ExitCode is not (0 or 3010 or 1641))
            throw new UpdaterException($"The Windows App Runtime installer exited with code {process.ExitCode}.");
    }

    internal static void InstallUpdate(string archivePath, int parentProcessId)
    {
        if (!File.Exists(archivePath))
            throw new UpdaterException("The application update archive does not exist.");

        string installDirectory = GetInstallDirectory();
        string packageDirectory = Path.Combine(
            Path.GetTempPath(),
            "MicaFlyouts",
            "update-packages",
            $"package-{Environment.ProcessId}-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(packageDirectory);
            ExtractArchive(archivePath, packageDirectory);
            ValidatePayload(packageDirectory);
            SpawnApplyHelper(packageDirectory, installDirectory, parentProcessId);
            TryDeleteFile(archivePath);
        }
        catch
        {
            TryDeleteDirectory(packageDirectory);
            throw;
        }
    }

    internal static void ApplyUpdate(string packageDirectory, string installDirectory, int parentProcessId)
    {
        string currentHelperPath = Environment.ProcessPath
            ?? throw new UpdaterException("Could not determine the updater helper path.");

        WaitForProcess(parentProcessId);
        using Mutex updaterInstance = AcquireUpdaterInstance();

        try
        {
            ReplaceApplicationFiles(packageDirectory, installDirectory);
        }
        catch
        {
            TryLaunchApplication(installDirectory);
            throw;
        }

        try
        {
            LaunchApplication(installDirectory);
        }
        catch
        {
            TryDeleteDirectory(packageDirectory);
            ScheduleSelfDelete(currentHelperPath);
            throw;
        }

        TryDeleteDirectory(packageDirectory);
        ScheduleSelfDelete(currentHelperPath);
    }

    internal static void TryLaunchApplication(string installDirectory)
    {
        try
        {
            LaunchApplication(installDirectory);
        }
        catch
        {
        }
    }

    private static void ExtractArchive(string archivePath, string destination)
    {
        using FileStream file = File.OpenRead(archivePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string root = DirectoryPrefix(destination);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (IsSymbolicLink(entry))
                throw new UpdaterException("The update archive cannot contain symbolic links.");

            bool isDirectory = entry.FullName.EndsWith('/');
            string relativePath = ValidateArchivePath(entry.FullName, isDirectory);
            if (!extractedPaths.Add(relativePath))
                throw new UpdaterException($"The update archive contains a duplicate path: {entry.FullName}");

            string outputPath = Path.GetFullPath(Path.Combine(destination, relativePath));
            if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UpdaterException($"The update archive contains an unsafe path: {entry.FullName}");

            if (isDirectory)
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            string? parentDirectory = Path.GetDirectoryName(outputPath);
            if (parentDirectory is not null)
                Directory.CreateDirectory(parentDirectory);

            using Stream input = entry.Open();
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static string ValidateArchivePath(string entryName, bool isDirectory)
    {
        string normalized = entryName.Replace('/', '\\');
        if (isDirectory)
            normalized = normalized.TrimEnd('\\');

        if (normalized.Length == 0 || Path.IsPathRooted(normalized))
            throw new UpdaterException($"The update archive contains an unsafe path: {entryName}");

        string[] components = normalized.Split('\\');
        foreach (string component in components)
        {
            if (component.Length == 0 || component is "." or ".." || component.Contains(':')
                || component.EndsWith(' ') || component.EndsWith('.')
                || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || IsReservedWindowsName(component))
            {
                throw new UpdaterException($"The update archive contains an unsafe path: {entryName}");
            }
        }

        return normalized;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        int unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixFileType == 0xA000;
    }

    private static bool IsReservedWindowsName(string component)
    {
        string name = Path.GetFileNameWithoutExtension(component);
        if (name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Length == 4
            && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && name[3] is >= '1' and <= '9';
    }

    private static void ValidatePayload(string packageDirectory)
    {
        foreach (string requiredFile in RequiredFiles)
        {
            string? filePath = FindPackageFile(packageDirectory, requiredFile);
            if (filePath is null)
                throw new UpdaterException($"The update package is missing {requiredFile}.");
        }
    }

    private static string? FindPackageFile(string directory, string expectedName)
    {
        string exactPath = Path.Combine(directory, expectedName);
        if (File.Exists(exactPath))
            return exactPath;

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static void SpawnApplyHelper(string packageDirectory, string installDirectory, int parentProcessId)
    {
        string helperDirectory = Path.Combine(Path.GetTempPath(), "MicaFlyouts", "updater");
        Directory.CreateDirectory(helperDirectory);
        string helperPath = Path.Combine(helperDirectory, $"updater-helper-{Guid.NewGuid():N}.exe");

        try
        {
            File.Copy(Environment.ProcessPath
                ?? throw new UpdaterException("Could not determine the updater executable path."), helperPath);

            var startInfo = new ProcessStartInfo(helperPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = helperDirectory,
            };
            startInfo.ArgumentList.Add("--apply-update");
            startInfo.ArgumentList.Add(packageDirectory);
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using Process process = Process.Start(startInfo)
                ?? throw new UpdaterException("Could not start the updater helper.");
        }
        catch
        {
            TryDeleteFile(helperPath);
            throw;
        }
    }

    private static void WaitForProcess(int processId)
    {
        if (processId == 0 || processId == Environment.ProcessId)
            throw new UpdaterException("Cannot wait for the specified process.");

        try
        {
            using Process process = Process.GetProcessById(processId);
            process.WaitForExit();
        }
        catch (ArgumentException)
        {
        }
    }

    private static Mutex AcquireUpdaterInstance()
    {
        var mutex = new Mutex(initiallyOwned: false, "Local\\MicaFlyouts.Updater");
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
        throw new UpdaterException("Another MicaFlyouts update is already in progress.");
    }

    private static void ReplaceApplicationFiles(string packageDirectory, string installDirectory)
    {
        ValidatePayload(packageDirectory);
        Directory.CreateDirectory(installDirectory);

        string packageRoot = DirectoryPrefix(packageDirectory);
        string installRoot = DirectoryPrefix(installDirectory);
        string transactionId = Guid.NewGuid().ToString("N");
        var stagedFiles = new List<StagedFile>();
        var backups = new List<FileBackup>();
        var installedFiles = new List<string>();

        try
        {
            foreach (string source in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
            {
                string fullSource = Path.GetFullPath(source);
                if (!fullSource.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
                    throw new UpdaterException($"The update package contains an unsafe path: {source}");

                string relativePath = Path.GetRelativePath(packageDirectory, source);
                string destination = Path.GetFullPath(Path.Combine(installDirectory, relativePath));
                if (!destination.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
                    throw new UpdaterException($"The update package contains an unsafe path: {relativePath}");

                string destinationDirectory = Path.GetDirectoryName(destination)
                    ?? throw new UpdaterException($"The update package contains an invalid path: {relativePath}");
                Directory.CreateDirectory(destinationDirectory);

                string stagedPath = Path.Combine(
                    destinationDirectory,
                    $".{Path.GetFileName(destination)}.new-{transactionId}");
                stagedFiles.Add(new StagedFile(destination, stagedPath));
                File.Copy(source, stagedPath);
            }

            foreach (StagedFile stagedFile in stagedFiles)
            {
                if (!File.Exists(stagedFile.Destination))
                    continue;

                string backupPath = Path.Combine(
                    Path.GetDirectoryName(stagedFile.Destination)!,
                    $".{Path.GetFileName(stagedFile.Destination)}.old-{transactionId}");
                File.Move(stagedFile.Destination, backupPath);
                backups.Add(new FileBackup(stagedFile.Destination, backupPath));
            }

            foreach (StagedFile stagedFile in stagedFiles)
            {
                File.Move(stagedFile.StagedPath, stagedFile.Destination);
                installedFiles.Add(stagedFile.Destination);
            }
        }
        catch (Exception exception)
        {
            foreach (string installedFile in installedFiles.AsEnumerable().Reverse())
                TryDeleteFile(installedFile);

            foreach (FileBackup backup in backups.AsEnumerable().Reverse())
                TryMoveFile(backup.BackupPath, backup.Destination);

            foreach (StagedFile stagedFile in stagedFiles)
                TryDeleteFile(stagedFile.StagedPath);

            throw new UpdaterException("Replacing the application files failed; the previous files were restored where possible.", exception);
        }

        foreach (FileBackup backup in backups)
            TryDeleteFile(backup.BackupPath);
    }

    private static void LaunchApplication(string installDirectory)
    {
        string applicationPath = Path.Combine(installDirectory, ApplicationFileName);
        if (!File.Exists(applicationPath))
            throw new UpdaterException($"Could not find the application: {applicationPath}");

        var startInfo = new ProcessStartInfo(applicationPath)
        {
            UseShellExecute = false,
            WorkingDirectory = installDirectory,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new UpdaterException("Could not start MicaFlyouts.");
    }

    private static void ScheduleSelfDelete(string path)
    {
        string command = $"timeout /t 3 /nobreak > nul & del /f /q \"{path}\"";
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("/C");
        startInfo.ArgumentList.Add(command);

        try
        {
            using Process? process = Process.Start(startInfo);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryMoveFile(string source, string destination)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
                File.Move(source, destination);
        }
        catch
        {
        }
    }

    private static string DirectoryPrefix(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private sealed record StagedFile(string Destination, string StagedPath);
    private sealed record FileBackup(string Destination, string BackupPath);
}
