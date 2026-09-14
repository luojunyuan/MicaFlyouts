using MicaFlyouts.Infrastructure.Interop;
using MicaFlyouts.Infrastructure.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace MicaFlyouts.Infrastructure.Notifications;

public sealed partial class NotificationService(AppLogger logger) : IDisposable
{
    private readonly AppLogger _logger = logger;
    private bool _registered;
    private int _disposed;

    public static bool CanNotify
    {
        get
        {
            try
            {
                return !NativeWindowApi.IsFullscreenPresentationActive();
            }
            catch
            {
                return false;
            }
        }
    }

    public void Initialize(Action<string>? onActivated = null)
    {
        if (_registered || Volatile.Read(ref _disposed) != 0 || !AppNotificationManager.IsSupported())
            return;
        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += (_, args) =>
            {
                if (onActivated is not null)
                    onActivated(args.Argument);
            };
            // 这个问题只因为我们是 WASDKSelfContained 并在将来版本已经被解决了，这个异常可忽视 https://github.com/microsoft/WindowsAppSDK/issues/6071
            manager.Register();
            _registered = true;
        }
        catch (Exception exception)
        {
            _logger.Warn($"App notifications are unavailable: {exception.Message}");
        }
    }

    public void Show(string title, string message)
    {
        if (!_registered || !CanNotify || Volatile.Read(ref _disposed) != 0)
            return;
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not show notification: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_registered)
        {
            try { AppNotificationManager.Default.Unregister(); } catch { }
            _registered = false;
        }
    }
}
