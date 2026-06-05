# WinOptimizationApp

WinUI 3 desktop app for the Windows System Maintenance Tool.

## Build

```powershell
dotnet restore .\src\app\WinOptimizationApp.csproj
dotnet build .\src\app\WinOptimizationApp.csproj
```

## Notes

- The existing CLI script is expected at `src/cli/Utilities.ps1`.
- The GUI runs as the current user by default and blocks admin-only tasks unless the app is started elevated.
- Task reports are written to `logs/` at the repository root.
