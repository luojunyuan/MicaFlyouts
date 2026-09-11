using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;

namespace MicaFlyouts.App;

public sealed class UiDispatcher
{
    public static void EnqueueOrRun(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        // Never invoke a UI callback on a background thread when the queue is
        // shutting down; Reactor windows are UI-thread-only.
        _ = dispatcher.TryEnqueue(new DispatcherQueueHandler(action));
    }
}
