using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class RestorePointService
{
    private readonly CommandRunner _commands;

    public RestorePointService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<string> TryCreateAsync(MaintenanceTask task, CancellationToken cancellationToken = default)
    {
        if (!task.CanRollback)
        {
            return "Restore point not required for this task.";
        }

        if (!SystemStatusService.IsAdministrator())
        {
            return "Restore point skipped: app is not running as Administrator.";
        }

        var description = $"WinOptimizationApp-{task.Id}-{DateTime.Now:yyyyMMddHHmmss}";
        var command = $"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS";
        var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", cancellationToken);

        return result.ExitCode == 0 ? $"Restore point requested: {description}." : $"Restore point failed: {result.StandardError.Trim()}";
    }

    public async Task<(bool Success, string Message)> CreateManualRestorePointAsync(
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (!SystemStatusService.IsAdministrator())
        {
            return (false, "Administrator privileges required to create a System Restore Point.");
        }

        var desc = string.IsNullOrWhiteSpace(description)
            ? $"WinOptimizationApp-Manual-{DateTime.Now:yyyyMMdd-HHmmss}"
            : description.Trim().Replace("'", "''").Replace("\"", "");

        var command = $"Checkpoint-Computer -Description '{desc}' -RestorePointType MODIFY_SETTINGS";
        var result = await _commands.RunCaptureAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            cancellationToken);

        if (result.ExitCode == 0)
        {
            return (true, desc);
        }

        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return (false, string.IsNullOrWhiteSpace(error) ? $"Failed with exit code {result.ExitCode}" : error);
    }
}
