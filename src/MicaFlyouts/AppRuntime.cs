namespace MicaFlyouts;

internal static class AppRuntime
{
    internal static App.AppServices? Current { get; private set; }

    public static void Start()
    {
        var singleInstance = Infrastructure.Windows.SingleInstanceService.Acquire();
        if (!singleInstance.IsPrimary)
        {
            singleInstance.Dispose();
            return;
        }

        var services = App.Bootstrap.Create(singleInstance);
        Current = services;
        try
        {
            // Tray menus are created after startup, so register their built-in
            // flyout handlers before the first surface is mounted.
            Microsoft.UI.Reactor.ReactorApp.RegisterAllBuiltIns();
            Microsoft.UI.Reactor.ReactorApp.ShutdownPolicy = Microsoft.UI.Reactor.ShutdownPolicy.Explicit;
            Microsoft.UI.Reactor.ReactorApp.Run(_ =>
            {
                services.OpenMainWindow();
                services.Start();
            });
        }
        finally
        {
            services.Dispose();
            Current = null;
        }
    }
}
