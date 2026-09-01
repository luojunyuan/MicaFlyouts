using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;

namespace MicaFlyouts.App;

public sealed class WindowRegistry : IDisposable
{
    private readonly Dictionary<WindowKey, ReactorWindow> _windows = [];
    private int _disposed;

    public IReadOnlyCollection<ReactorWindow> Windows => _windows.Values;

    public ReactorWindow OpenOrActivate(
        WindowKey key,
        WindowSpec specification,
        Func<Component> rootFactory)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(rootFactory);

        if (_windows.TryGetValue(key, out var existing))
        {
            existing.Show();
            existing.Activate();
            return existing;
        }

        var window = ReactorApp.OpenWindow(specification with { Key = key }, rootFactory);
        _windows[key] = window;
        window.Closed += (_, _) => _windows.Remove(key);
        return window;
    }

    public bool TryGet(WindowKey key, out ReactorWindow? window)
    {
        if (_windows.TryGetValue(key, out var found))
        {
            window = found;
            return true;
        }

        window = null;
        return false;
    }

    public void Close(WindowKey key)
    {
        if (_windows.Remove(key, out var window))
            window.Close();
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToArray())
            window.Close();
        _windows.Clear();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        CloseAll();
    }
}
