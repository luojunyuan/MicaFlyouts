# MicaFlyouts WASDK Toast Scenario

This is a manually run Windows scenario for diagnosing the Windows App SDK runtime toasts. It compiles the same `RuntimeToastNotifier.cs` and `AppLogger.cs` used by MicaFlyouts, without starting WinUI or initializing the Windows App SDK.

Run the simulated runtime notification sequence:

```powershell
dotnet run --project tests/scenario/wasdk-toast -- toast-demo
```

The scenario sends a numeric download progress toast, updates it through 15%, 42%, 73%, and 100%, then sends the indeterminate installing toast and the completion toast. It does not download or install the runtime, so it isolates toast creation and progress updates from the network and installer.

To build a standalone ARM64 executable for another Windows machine:

```powershell
dotnet publish tests/scenario/wasdk-toast/MicaFlyouts.WasdkToastScenario.csproj -r win-arm64 -p:PublishAot=true
```

The notifier writes failures to the directory printed by the scenario, normally `%AppData%\MicaFlyouts\logs`.
