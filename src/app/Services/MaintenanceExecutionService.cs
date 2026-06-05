using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class MaintenanceExecutionService(
    CleanupService cleanup,
    CommandRunner commands,
    PathService paths,
    ReportService reports,
    RestorePointService restorePoints)
{
    private readonly CleanupService _cleanup = cleanup;
    private readonly CommandRunner _commands = commands;
    private readonly PathService _paths = paths;
    private readonly ReportService _reports = reports;
    private readonly RestorePointService _restorePoints = restorePoints;

    public IpcClient? Client { get; set; }

    public async Task<TaskRunResult> RunAsync(MaintenanceTask task, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new RunTaskRequestPayload
            {
                TaskId = task.Id,
                TaskLabel = task.Label,
                TaskGroup = task.Group,
                CanRollback = task.CanRollback
            });
            var response = await Client.SendRequestAsync("RunTask", payload);
            return System.Text.Json.JsonSerializer.Deserialize<TaskRunResult>(response) ?? throw new InvalidOperationException("Failed to deserialize TaskRunResult");
        }
        var started = DateTimeOffset.Now;
        var preMessages = new List<string>();
        var preErrors = new List<string>();

        if (task.CanRollback)
        {
            preMessages.Add(await _restorePoints.TryCreateAsync(task, cancellationToken));
        }

        TaskRunResult result = task.Group switch
        {
            "Cleanup" or "Privacy" => await _cleanup.RunAsync(task, cancellationToken),
            "Repair" => await RunRepairAsync(task, started, [], [], cancellationToken),
            "Optimization" => await RunOptimizationAsync(task, started, [], [], cancellationToken),
            "Settings" => await RunSettingsAsync(task, started, [], []),
            "Updates" => await RunWingetUpgradeAsync(task, started, [], [], cancellationToken),
            _ => new TaskRunResult(task.Id, task.Label, started, DateTimeOffset.Now, false, 0, 0, 0, [], ["No runner is registered for this task."])
        };

        var merged = result with
        {
            Messages = [..preMessages, ..result.Messages],
            Errors = [..preErrors, ..result.Errors]
        };

        await _reports.SaveAsync(merged, cancellationToken);
        return merged;
    }

    private async Task<TaskRunResult> RunRepairAsync(MaintenanceTask task, DateTimeOffset started, List<string> messages, List<string> errors, CancellationToken cancellationToken)
    {
        (string File, string Args) command = task.Id switch
        {
            "network.dns" => ("ipconfig.exe", "/flushdns"),
            "repair.dism" => ("dism.exe", "/online /cleanup-image /restorehealth"),
            "repair.sfc" => ("sfc.exe", "/scannow"),
            "repair.explorer" => ("powershell.exe", "-NoProfile -Command \"Stop-Process -Name explorer -Force; Start-Process explorer.exe\""),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrEmpty(command.File))
        {
            errors.Add("Unknown repair task.");
        }
        else
        {
            var result = await _commands.RunCaptureAsync(command.File, command.Args, cancellationToken);
            messages.Add(result.StandardOutput.Trim());
            if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StandardError))
            {
                errors.Add(result.StandardError.Trim());
            }
        }

        return Finish(task, started, messages, errors);
    }

    private async Task<TaskRunResult> RunOptimizationAsync(MaintenanceTask task, DateTimeOffset started, List<string> messages, List<string> errors, CancellationToken cancellationToken)
    {
        (string File, string Args) command = task.Id switch
        {
            "optimization.hibernate" => ("powercfg.exe", "-h off"),
            "optimization.drives" => ("powershell.exe", "-NoProfile -Command \"Get-Volume | Where-Object { $_.DriveLetter -and $_.DriveType -eq 'Fixed' } | ForEach-Object { Optimize-Volume -DriveLetter $_.DriveLetter -Verbose }\""),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrEmpty(command.File))
        {
            errors.Add("Unknown optimization task.");
        }
        else
        {
            var result = await _commands.RunCaptureAsync(command.File, command.Args, cancellationToken);
            messages.Add(result.StandardOutput.Trim());
            if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StandardError))
            {
                errors.Add(result.StandardError.Trim());
            }
        }

        return Finish(task, started, messages, errors);
    }

    private async Task<TaskRunResult> RunSettingsAsync(MaintenanceTask task, DateTimeOffset started, List<string> messages, List<string> errors)
    {
        if (task.Id == "settings.storage")
        {
            await CommandRunner.StartShellAsync("ms-settings:storagesense", string.Empty);
            messages.Add("Opened Storage Sense settings.");
        }
        else if (task.Id == "cli.launch")
        {
            if (!File.Exists(_paths.CliScriptPath))
            {
                errors.Add($"CLI script not found: {_paths.CliScriptPath}");
            }
            else
            {
                await CommandRunner.StartShellAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{_paths.CliScriptPath}\"");
                messages.Add($"Started CLI script: {_paths.CliScriptPath}");
            }
        }
        else
        {
            errors.Add("Unknown settings task.");
        }

        return Finish(task, started, messages, errors);
    }

    private async Task<TaskRunResult> RunWingetUpgradeAsync(MaintenanceTask task, DateTimeOffset started, List<string> messages, List<string> errors, CancellationToken cancellationToken)
    {
        if (!_commands.Exists("winget"))
        {
            errors.Add("winget is not available.");
        }
        else
        {
            var result = await _commands.RunCaptureAsync("winget.exe", "upgrade --all", cancellationToken);
            messages.Add(result.StandardOutput.Trim());
            if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StandardError))
            {
                errors.Add(result.StandardError.Trim());
            }
        }

        return Finish(task, started, messages, errors);
    }

    private static TaskRunResult Finish(MaintenanceTask task, DateTimeOffset started, List<string> messages, List<string> errors)
    {
        return new TaskRunResult(task.Id, task.Label, started, DateTimeOffset.Now, errors.Count == 0, 0, 0, 0, messages, errors);
    }
}
