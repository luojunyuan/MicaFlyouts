using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using MicaFlyouts.Infrastructure.Logging;

namespace MicaFlyouts.Infrastructure.Notifications;

internal sealed partial class RuntimeToastNotifier : IDisposable
{
    private const string AppUserModelId = "MicaFlyouts.MicaFlyouts";
    private const string ToastTag = "windows-app-sdk";
    private const string ToastGroup = "runtime";
    private const uint CoInitializeMultithreaded = 0;

    private readonly AppLogger _logger;
    private readonly BlockingCollection<RuntimeToast> _queue = new();
    private readonly Thread _worker;
    private int _disposed;

    internal RuntimeToastNotifier(AppLogger logger)
    {
        _logger = logger;
        _worker = new Thread(NotifyWorker)
        {
            IsBackground = true,
            Name = "MicaFlyouts runtime notifications",
        };
        _worker.Start();
    }

    internal void Downloading(long bytesDownloaded, long? totalBytes)
    {
        bool isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        double? progress = totalBytes is > 0
            ? Math.Clamp(bytesDownloaded / (double)totalBytes.Value, 0, 1)
            : null;
        string progressText = progress is double value
            ? $"{Math.Round(value * 100).ToString(CultureInfo.InvariantCulture)}%"
            : string.Empty;
        Send(new RuntimeToast(
            isChinese
                ? "\u6b63\u5728\u4e0b\u8f7d Windows App SDK \u8fd0\u884c\u65f6"
                : "Downloading the Windows App SDK runtime",
            true,
            progress,
            progressText,
            isChinese ? "\u6b63\u5728\u4e0b\u8f7d" : "Downloading"));
    }

    internal void Installing()
    {
        bool isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        Send(new RuntimeToast(
            isChinese
                ? "\u6b63\u5728\u5b89\u88c5 Windows App SDK \u8fd0\u884c\u65f6"
                : "Installing the Windows App SDK runtime",
            true,
            null,
            string.Empty,
            isChinese ? "\u6b63\u5728\u5b89\u88c5" : "Installing"));
    }

    internal void Completed()
    {
        bool isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        Send(new RuntimeToast(
            isChinese
                ? "Windows App SDK \u8fd0\u884c\u65f6\u5b89\u88c5\u5b8c\u6210"
                : "Windows App SDK runtime installation completed",
            false,
            null,
            string.Empty,
            string.Empty));
    }

    internal void Failed(string message)
    {
        bool isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        Send(new RuntimeToast(
            $"{(isChinese ? "Windows App SDK \u8fd0\u884c\u65f6\u5b89\u88c5\u5931\u8d25" : "Windows App SDK runtime setup failed")}: {message}",
            false,
            null,
            string.Empty,
            string.Empty));
    }

    private void Send(RuntimeToast toast)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        try
        {
            _queue.Add(toast);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void NotifyWorker()
    {
        int initializeResult = CoInitializeEx(0, CoInitializeMultithreaded);
        if (initializeResult < 0)
        {
            _logger.Warn($"Could not initialize the runtime toast COM worker: 0x{initializeResult:X8}.");
            return;
        }

        try
        {
            ToastNotifier notifier = CreateToastNotifier();
            bool progressToastActive = false;
            uint sequence = 0;

            foreach (RuntimeToast toast in _queue.GetConsumingEnumerable())
            {
                try
                {
                    sequence = sequence == uint.MaxValue ? 1 : sequence + 1;
                    if (toast.HasProgress)
                    {
                        NotificationData data = CreateProgressData(toast, sequence);
                        if (progressToastActive)
                        {
                            NotificationUpdateResult result = notifier.Update(data, ToastTag, ToastGroup);
                            if (result == NotificationUpdateResult.Succeeded)
                                continue;
                        }

                        notifier.Show(CreateNotification(toast, data));
                        progressToastActive = true;
                        continue;
                    }

                    notifier.Show(CreateNotification(toast));
                    progressToastActive = false;
                }
                catch (Exception exception)
                {
                    _logger.Warn($"Could not show Windows App SDK runtime toast: {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not initialize Windows App SDK runtime toasts: {exception.Message}");
        }
        finally
        {
            CoUninitialize();
        }
    }

    private static ToastNotifier CreateToastNotifier()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            $"Software\\Classes\\AppUserModelId\\{AppUserModelId}")
            ?? throw new InvalidOperationException("Could not register the runtime toast application ID.");
        key.SetValue("DisplayName", "Mica Flyouts", RegistryValueKind.String);
        return ToastNotificationManager.CreateToastNotifier(AppUserModelId);
    }

    private static NotificationData CreateProgressData(
        RuntimeToast toast,
        uint sequence)
    {
        var data = new NotificationData { SequenceNumber = sequence };
        data.Values["1"] = toast.Message;
        data.Values["progressValue"] = toast.Progress is double progress
            ? progress.ToString("0.0000", CultureInfo.InvariantCulture)
            : "indeterminate";
        data.Values["progressValueString"] = toast.ProgressText;
        data.Values["progressStatus"] = toast.ProgressStatus;
        return data;
    }

    private static ToastNotification CreateNotification(RuntimeToast toast, NotificationData? data = null)
    {
        string progressElement = toast.HasProgress
            ? "<progress value=\"{progressValue}\" status=\"{progressStatus}\" valueStringOverride=\"{progressValueString}\" />"
            : string.Empty;
        string message = SecurityElement.Escape(toast.Message) ?? string.Empty;
        string xml = $"<toast><visual><binding template=\"ToastGeneric\"><text>Mica Flyouts</text><text id=\"1\">{message}</text>{progressElement}</binding></visual></toast>";
        var document = new XmlDocument();
        document.LoadXml(xml);

        var notification = new ToastNotification(document)
        {
            Tag = ToastTag,
            Group = ToastGroup,
        };
        if (data is not null)
            notification.Data = data;
        return notification;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _queue.CompleteAdding();
        _worker.Join();
        _queue.Dispose();
    }

    [LibraryImport("ole32.dll", EntryPoint = "CoInitializeEx")]
    private static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll", EntryPoint = "CoUninitialize")]
    private static partial void CoUninitialize();

    private sealed record RuntimeToast(
        string Message,
        bool HasProgress,
        double? Progress,
        string ProgressText,
        string ProgressStatus);
}
