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

        var logger = new Infrastructure.Logging.AppLogger();
        try
        {
            Infrastructure.Runtime.WindowsAppSdkRuntimeSetup.EnsureRuntime(logger);
        }
        catch (Exception exception)
        {
            try
            {
                logger.Error("Windows App SDK runtime setup failed; continuing startup.", exception);
            }
            catch (Exception loggingException)
            {
                System.Diagnostics.Debug.WriteLine($"Could not log Windows App SDK runtime setup failure: {loggingException}");
            }
        }

        // AppServices is process-scoped: ReactorApp.Run only returns once the
        // app is shutting down, so there is nothing to release afterwards.
        var services = App.Bootstrap.Create(singleInstance, logger);
        Current = services;

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
}
