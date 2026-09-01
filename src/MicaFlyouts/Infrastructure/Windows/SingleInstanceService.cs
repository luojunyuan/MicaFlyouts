using MicaFlyouts.App;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;

namespace MicaFlyouts.Infrastructure.Windows;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "MicaFlyouts";
    private const string SettingsEventName = "MicaFlyouts_OpenSettings";

    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private EventWaitHandle? _settingsEvent;
    private DispatcherQueueTimer? _settingsTimer;

    private SingleInstanceService(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public bool IsPrimary => _ownsMutex;

    public static SingleInstanceService Acquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
            return new SingleInstanceService(mutex, true);

        mutex.Dispose();
        try
        {
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
            signal.Set();
        }
        catch
        {
            // A second launch is still harmless if the existing process cannot be signaled.
        }
        return new SingleInstanceService(new Mutex(false, MutexName), false);
    }

    public void StartSettingsListener(UiDispatcher dispatcher, Action openSettings)
    {
        if (!IsPrimary || _settingsTimer is not null)
            return;

        _settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
        var dispatcherQueue = ReactorApp.UIDispatcher;
        if (dispatcherQueue is null)
        {
            _settingsEvent.Dispose();
            _settingsEvent = null;
            return;
        }

        _settingsTimer = dispatcherQueue.CreateTimer();
        _settingsTimer.Interval = TimeSpan.FromMilliseconds(250);
        _settingsTimer.IsRepeating = true;
        _settingsTimer.Tick += (_, _) =>
        {
            if (_settingsEvent?.WaitOne(0) != true)
                return;
            try
            {
                openSettings();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Could not open settings from the second instance: {exception}");
            }
        };
        _settingsTimer.Start();
    }

    public void Dispose()
    {
        _settingsTimer?.Stop();
        _settingsTimer = null;
        _settingsEvent?.Dispose();
        _settingsEvent = null;
        if (_ownsMutex)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
