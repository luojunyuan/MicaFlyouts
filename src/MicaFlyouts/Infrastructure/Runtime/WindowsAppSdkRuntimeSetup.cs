using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.Notifications;
using Win32 = Windows.Win32;
using Win32Foundation = Windows.Win32.Foundation;

namespace MicaFlyouts.Infrastructure.Runtime;

internal static class WindowsAppSdkRuntimeSetup
{
    private const string UpdaterFileName = "Updater.exe";
    private const string RuntimeVersion = "2.5.1";
    private const string FrameworkPackageName = "Microsoft.WindowsAppRuntime.2";
    private const string PublisherId = "8wekyb3d8bbwe";
    private const string DownloadUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0.0.0 Safari/537.36 Edg/154.0.0.0";
    private const int DownloadBufferSize = 128 * 1024;
    private const long MinimumInstallerSize = 1_048_576;
    private const long DownloadProgressBytes = 5 * 1024 * 1024;

    private static readonly Version MinimumRuntimeVersion = new(2, 5, 1, 0);

    internal static void EnsureRuntime(AppLogger logger)
    {
        string updaterPath = Path.Combine(AppContext.BaseDirectory, UpdaterFileName);
        if (!File.Exists(updaterPath))
        {
            logger.Info($"{UpdaterFileName} was not found next to MicaFlyouts; skipping Windows App SDK runtime setup.");
            return;
        }

        string architecture = GetProcessArchitecture();
        if (RuntimeIsInstalled(architecture))
            return;

        using var notifier = new RuntimeToastNotifier(logger);
        try
        {
            RuntimeInstallerArtifact artifact = GetInstallerArtifact(architecture);
            string installerPath = GetInstallerCachePath(architecture);
            byte[] expectedHash = Convert.FromHexString(artifact.Sha256);

            if (IsValidInstaller(installerPath, expectedHash))
            {
                logger.Info($"Using the cached Windows App SDK {RuntimeVersion} installer for {architecture}.");
            }
            else
            {
                logger.Info($"Downloading the Windows App SDK {RuntimeVersion} installer for {architecture}.");
                DownloadInstaller(artifact.Uri, installerPath, expectedHash, logger, notifier);
            }

            logger.Info("Installing the Windows App SDK runtime.");
            notifier.Installing();
            var startInfo = new ProcessStartInfo(updaterPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--from-app");
            startInfo.ArgumentList.Add("--install-runtime");
            startInfo.ArgumentList.Add(installerPath);

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the MicaFlyouts updater.");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"The MicaFlyouts updater exited with code {process.ExitCode} while installing the Windows App SDK runtime.");

            TryDeleteFile(installerPath, logger);
            logger.Info("Windows App SDK runtime installation completed.");
            notifier.Completed();
        }
        catch (Exception exception)
        {
            notifier.Failed(exception.Message);
            throw;
        }
    }

    private static string GetProcessArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.Arm => "arm",
        var architecture => throw new PlatformNotSupportedException(
            $"Windows App SDK runtime setup does not support process architecture {architecture}."),
    };

    private static RuntimeInstallerArtifact GetInstallerArtifact(string architecture) => architecture switch
    {
        "x86" => new RuntimeInstallerArtifact(
            new Uri("https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-x86.exe"),
            "76dbd7c272cee0bf18f0b7228255b353d669cd55dc530209c45646f47acb89d4"),
        "x64" => new RuntimeInstallerArtifact(
            new Uri("https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-x64.exe"),
            "931a421e8dc3e6e67724806cb67fecdbb88dfe323f0170842eb4a4b4b149f1e2"),
        "arm64" => new RuntimeInstallerArtifact(
            new Uri("https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-arm64.exe"),
            "d5e4d34547eb4e31c64bf1532415b3019c0d92b750e72eb18d3d95bd00feacbb"),
        _ => throw new PlatformNotSupportedException($"No Windows App SDK runtime installer is available for {architecture}."),
    };

    private static bool RuntimeIsInstalled(string architecture)
    {
        string familyName = $"{FrameworkPackageName}_{PublisherId}";
        return GetPackageFamilyFullNames(familyName).Any(fullName =>
            PackageFullNameMatches(fullName, architecture, MinimumRuntimeVersion));
    }

    private static string[] GetPackageFamilyFullNames(string familyName)
    {
        uint count = 0;
        uint bufferLength = 0;
        Win32Foundation.WIN32_ERROR status = Win32.PInvoke.GetPackagesByPackageFamily(
            familyName,
            ref count,
            default,
            ref bufferLength,
            default);

        if (IsMissingPackageStatus(status) || count == 0)
            return [];
        if (status is not (Win32Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER or Win32Foundation.WIN32_ERROR.NO_ERROR))
            throw PackageQueryError(familyName, status);

        var packageNames = new Win32Foundation.PWSTR[checked((int)count)];
        var buffer = new char[checked((int)bufferLength)];
        status = Win32.PInvoke.GetPackagesByPackageFamily(
            familyName,
            ref count,
            packageNames,
            ref bufferLength,
            buffer);

        if (IsMissingPackageStatus(status))
            return [];
        if (status != Win32Foundation.WIN32_ERROR.NO_ERROR)
            throw PackageQueryError(familyName, status);

        return packageNames
            .Take(checked((int)count))
            .Where(static packageName => packageName != default)
            .Select(static packageName => packageName.ToString())
            .ToArray();
    }

    private static bool PackageFullNameMatches(string fullName, string architecture, Version minimumVersion)
    {
        string prefix = $"{FrameworkPackageName}_";
        if (!fullName.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string[] components = fullName[prefix.Length..].Split('_');
        return components.Length == 4
            && Version.TryParse(components[0], out Version? version)
            && version.Build >= 0
            && version.Revision >= 0
            && version.Major == minimumVersion.Major
            && version >= minimumVersion
            && string.Equals(components[1], architecture, StringComparison.Ordinal)
            && components[2].Length == 0
            && string.Equals(components[3], PublisherId, StringComparison.Ordinal);
    }

    private static bool IsMissingPackageStatus(Win32Foundation.WIN32_ERROR status)
        => status is Win32Foundation.WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE
            or Win32Foundation.WIN32_ERROR.ERROR_FILE_NOT_FOUND
            or Win32Foundation.WIN32_ERROR.ERROR_NOT_FOUND;

    private static Win32Exception PackageQueryError(string familyName, Win32Foundation.WIN32_ERROR status)
        => new(unchecked((int)(uint)status), $"Could not query Windows package family {familyName}.");

    private static string GetInstallerCachePath(string architecture)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string cacheRoot = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
        string directory = Path.Combine(cacheRoot, "MicaFlyouts", "WindowsAppSDK", RuntimeVersion, architecture);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"WindowsAppRuntimeInstall-{RuntimeVersion}-{architecture}.exe");
    }

    private static bool IsValidInstaller(string path, byte[] expectedHash)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length <= MinimumInstallerSize)
                return false;

            Span<byte> header = stackalloc byte[2];
            stream.ReadExactly(header);
            if (!header.SequenceEqual("MZ"u8))
                return false;

            stream.Position = 0;
            byte[] actualHash = SHA256.HashData(stream);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DownloadInstaller(
        Uri downloadUri,
        string destinationPath,
        byte[] expectedHash,
        AppLogger logger,
        RuntimeToastNotifier notifier)
    {
        if (!IsHttpsUri(downloadUri))
            throw new InvalidOperationException("The Windows App SDK installer URL must use HTTPS.");

        string partialPath = destinationPath + ".part";
        try
        {
            using var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(DownloadUserAgent);

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);
            using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            Uri? finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null || !IsHttpsUri(finalUri))
                throw new InvalidOperationException("The Windows App SDK installer download redirected to an invalid URL.");

            long? contentLength = response.Content.Headers.ContentLength;
            long downloaded = 0;
            long lastReported = 0;
            long lastToastReported = 0;
            var toastTimer = Stopwatch.StartNew();
            notifier.Downloading(downloaded, contentLength);
            using Stream input = response.Content.ReadAsStream();
            using (var output = new FileStream(
                partialPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                DownloadBufferSize,
                FileOptions.SequentialScan))
            {
                var buffer = new byte[DownloadBufferSize];
                while (true)
                {
                    int read = input.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;

                    output.Write(buffer, 0, read);
                    downloaded += read;
                    if (downloaded - lastReported >= DownloadProgressBytes)
                    {
                        logger.Info($"Windows App SDK installer download: {downloaded} bytes received.");
                        lastReported = downloaded;
                    }
                    if (downloaded - lastToastReported >= 512 * 1024 || toastTimer.Elapsed >= TimeSpan.FromMilliseconds(500))
                    {
                        notifier.Downloading(downloaded, contentLength);
                        lastToastReported = downloaded;
                        toastTimer.Restart();
                    }
                }

                output.Flush(flushToDisk: true);
            }

            if (contentLength is long expectedLength && downloaded != expectedLength)
                throw new InvalidDataException($"The runtime download ended at {downloaded} bytes; expected {expectedLength}.");
            if (!IsValidInstaller(partialPath, expectedHash))
                throw new InvalidDataException("The Windows App SDK installer failed its SHA-256 or executable header check.");

            notifier.Downloading(downloaded, contentLength);

            File.Move(partialPath, destinationPath, overwrite: true);
            logger.Info($"Windows App SDK installer download completed ({downloaded} bytes).");
        }
        catch
        {
            TryDeleteFile(partialPath, logger);
            throw;
        }
    }

    private static bool IsHttpsUri(Uri uri)
        => uri.IsAbsoluteUri
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);

    private static void TryDeleteFile(string path, AppLogger logger)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.Warn($"Could not remove Windows App SDK installer cache {path}: {exception.Message}");
        }
    }

    private sealed record RuntimeInstallerArtifact(Uri Uri, string Sha256);
}
