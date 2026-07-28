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
- The splash screen warms system overview, startup, update, Appx, tweak, network-adapter, and Winapp2 state into a session cache so page navigation does not trigger redundant scans.
- Curated reversible tweaks include Windows suggestion controls, local-only Search suggestions, File Explorer extensions, taskbar End Task, and an explicitly medium-risk UTC hardware-clock option for dual-boot systems.
- The Advanced Toolbox includes safe ICMP latency diagnostics without forwarding host input to a shell command.

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

- **[FluentCleaner](https://github.com/builtbybel/FluentCleaner)**: Inspired custom Winapp2 cleaning database support, the transparent preview workflow, and a focused WinUI experience.
- **[Winhance](https://github.com/memstechtips/Winhance)**: Inspired system optimization, reversible tweaks, software management, and reusable configuration profiles.
- **[Win11Debloat](https://github.com/Raphire/Win11Debloat)** by Raphire: Inspired safe, reversible Windows customization for File Explorer, Search, suggestions, and the taskbar.
- **[Optimizer](https://github.com/hellzerg/optimizer)** by hellzerg: Inspired the advanced toolbox, network diagnostics, and the optional UTC hardware-clock setting for dual-boot systems. The original project is archived and has been superseded by OptimizerNXT.
- **[QDirStat](https://github.com/shundhammer/qdirstat)** by Stefan Hundhammer: Inspired storage discovery by largest, newest, and oldest files, file-age distribution, and emphasis on dominant disk-usage items.
- **[WinMole](https://github.com/bhadraagada/winmole)** by bhadraagada: Inspired user-protected paths, project-aware developer artifact discovery, and an all-in-one maintenance workflow.

The features in this app are independently implemented and adapted to its preview, confirmation, snapshot, and rollback safety model.
