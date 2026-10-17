# Startup Timing

This project measures process launch until the tray button appears in Explorer's UI Automation tree. Before timing each run it opens the hidden-icons panel, then runs samples serially, terminates the launched process, and reports the average, median, minimum, and maximum. Both applications use the same process-launch and UI Automation detection path.

## MicaFlyouts

Publish the Native AOT build:

```powershell
dotnet publish src/MicaFlyouts/MicaFlyouts.csproj -c Release -r win-arm64 -p:PublishAot=true
```

Then run 20 samples:

```powershell
dotnet run --project tests/StartupTiming/MicaFlyouts.StartupTiming.csproj -c Release -- --exe src/MicaFlyouts/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/MicaFlyouts.exe --name MicaFlyouts --runs 20
```

The harness starts the published executable with `UseShellExecute=false` and looks for a taskbar or hidden-icons UI Automation button whose name contains `MicaFlyouts`.

## FluentFlyout

Use the installed executable path supplied by the machine. The `shell` mode checks the taskbar and hidden-icons panel for a UI Automation button whose name matches the application name. This works with regular tray and overflow-area icons on Windows 11.

```powershell
dotnet run --project tests/StartupTiming/MicaFlyouts.StartupTiming.csproj -c Release -- --exe 'C:\Program Files\WindowsApps\unchihugo.FluentFlyout_2.15.0.0_x64__69b7b6qge1ahj\FluentFlyout\FluentFlyout.exe' --name FluentFlyout --runs 20
```

The FluentFlyout endpoint is the first UI Automation sample containing its `FluentFlyout` tray button. For both applications, timing begins immediately before `Process.Start` and ends at the first matching tray button in Explorer's taskbar or hidden-icons panel. The harness terminates each launched process and waits for its tray button to disappear before starting the next sample.
