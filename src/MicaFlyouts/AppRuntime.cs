namespace MicaFlyouts;

internal static class AppRuntime
{
    internal static App.AppServices? Current { get; private set; }

    internal static App.AppServices Services =>
        Current ?? throw new InvalidOperationException("AppRuntime is not running.");

    public static void Start()
    {
        var singleInstance = Infrastructure.Windows.SingleInstanceService.Acquire();
        if (!singleInstance.IsPrimary)
        {
            singleInstance.Dispose();
            return;
        }

        // AppServices is process-scoped: ReactorApp.Run only returns once the
        // app is shutting down, so there is nothing to release afterwards.
        var services = App.Bootstrap.Create(singleInstance);
        Current = services;

        // Tray menus are created after startup, so register their built-in
        // flyout handlers before the first surface is mounted.
        Microsoft.UI.Reactor.ReactorApp.RegisterAllBuiltIns();
        Microsoft.UI.Reactor.ReactorApp.ShutdownPolicy = Microsoft.UI.Reactor.ShutdownPolicy.Explicit;
        Microsoft.UI.Reactor.ReactorApp.Run(_ =>
        {
            // Do NOT enable: with OnExplicitShutdown, Application.Exit() skips
            // WinUI's normal window-close path and the process randomly
            // fail-fasts in Microsoft.UI.Input.dll (0xc0000602) when windows
            // are open (measured: 3 of 5 runs).
            //Application.Current.DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
            services.OpenMainWindow();
            services.Start();
        });
    }
}
