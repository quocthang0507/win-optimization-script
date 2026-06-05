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
}
