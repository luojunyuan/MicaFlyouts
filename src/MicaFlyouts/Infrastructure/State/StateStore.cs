using MicaFlyouts.Domain;

namespace MicaFlyouts.Infrastructure.State;

public sealed class StateStore<TSnapshot> : IStateStore<TSnapshot>, IDisposable
{
    private object _snapshot;
    private Action[] _listeners = Array.Empty<Action>();
    private readonly object _gate = new();
    private int _disposed;

    public StateStore(TSnapshot initialSnapshot)
    {
        _snapshot = initialSnapshot!;
    }

    public TSnapshot Snapshot => (TSnapshot)Volatile.Read(ref _snapshot);

    public Action Subscribe(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            var current = _listeners;
            var next = new Action[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[^1] = listener;
            Volatile.Write(ref _listeners, next);
        }

        int removed = 0;
        return () =>
        {
            if (Interlocked.Exchange(ref removed, 1) != 0)
                return;

            lock (_gate)
            {
                var current = _listeners;
                int index = Array.IndexOf(current, listener);
                if (index < 0)
                    return;
                var next = new Action[current.Length - 1];
                if (index > 0)
                    Array.Copy(current, 0, next, 0, index);
                if (index < current.Length - 1)
                    Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                Volatile.Write(ref _listeners, next);
            }
        };
    }

    public void SetSnapshot(TSnapshot nextSnapshot)
    {
        ArgumentNullException.ThrowIfNull(nextSnapshot);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        Interlocked.Exchange(ref _snapshot, nextSnapshot!);
        var listeners = Volatile.Read(ref _listeners);
        foreach (var listener in listeners)
        {
            try
            {
                listener();
            }
            catch
            {
                // One subscriber must not prevent the remaining stores from updating.
            }
        }
    }

    public TSnapshot Update(Func<TSnapshot, TSnapshot> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        var next = reducer(Snapshot);
        SetSnapshot(next);
        return next;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            lock (_gate)
                Volatile.Write(ref _listeners, Array.Empty<Action>());
        }
    }
}
