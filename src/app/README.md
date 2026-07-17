# WinOptimizationApp

WinUI 3 desktop app for the Windows System Maintenance Tool.

## Build

```powershell
dotnet restore .\src\app\WinOptimizationApp.csproj
dotnet build .\src\app\WinOptimizationApp.csproj
```

## Notes

- The UI process can run with the `--ui` argument, which connects to the Named Pipe server (`WinOptimizationApp_Runner`) hosted by the Runner process.
- If launched without arguments, the executable runs as the **Runner** (daemon), starting the IPC server and spawning the UI child process.
- Elevated commands/tasks trigger an Admin banner warning in the UI, and users are prompted to relaunch the application as Administrator via standard UAC elevation if they select to run administrator-required actions.
- Task reports are written as both text logs and JSON summaries to the `logs/` directory at the repository root.

## Architecture

```mermaid
graph TD
    A[User / OS] -->|Launch| B(WinOptimizationApp.exe - Runner Mode)
    B -->|Initialize| C[IpcServer]
    C -->|Listen on Pipe| D[\\.\pipe\WinOptimizationApp_Runner]
    B -->|Spawn Process| E(WinOptimizationApp.exe --ui - UI Mode)
    E -->|Connect via IpcClient| D
    E -->|JSON Requests| C
    C -->|Task Executions / Progress| E
```

## Acknowledgments

This project takes inspiration and borrows concepts/architectural patterns from the following amazing open-source projects:

- **[FluentCleaner](https://github.com/builtbybel/FluentCleaner)**: Inspired the Winapp2 cleaning database integration, transparent preview workflow, and focused WinUI experience.
- **[Winhance](https://github.com/memstechtips/Winhance)**: Inspired system optimization, reversible tweaks, software management, and reusable configuration profiles.
