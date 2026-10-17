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

        Microsoft.UI.Reactor.ReactorApp.ShutdownPolicy = Microsoft.UI.Reactor.ShutdownPolicy.Explicit;
        bool settingsControlsRegistered = Microsoft.UI.Reactor.ReactorApp.TryRegisterControlAssembly(
            typeof(CommunityToolkit.WinUI.Controls.SettingsCard).Assembly);
        logger.Info(settingsControlsRegistered
            ? "Registered the SettingsControls XAML metadata provider."
            : "The SettingsControls assembly has no XAML metadata provider.");
        Microsoft.UI.Reactor.ReactorApplication.OnUnhandledException = exception =>
        {
            logger.Error("Unhandled WinUI exception.", exception);
            return false;
        };
        Microsoft.UI.Reactor.ReactorApp.Run(_ =>
        {
            services.OpenMainWindow();
            services.Start();
        });
    }
}
