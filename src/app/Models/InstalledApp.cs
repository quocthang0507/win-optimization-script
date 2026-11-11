namespace WinOptimizationApp.Models;

public sealed class InstalledApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Publisher { get; init; }
    public required string UninstallString { get; init; }
    public required string InstallLocation { get; init; }
    public required string Source { get; init; }
}

public enum UninstallOutcomeStatus
{
    Succeeded,
    Failed,
    TimedOut,
    Cancelled
}

public sealed record UninstallOperationResult(
    InstalledApp App,
    UninstallOutcomeStatus Status,
    int ExitCode,
    string Message)
{
    public bool Success => Status == UninstallOutcomeStatus.Succeeded;
    public bool IsTimedOut => Status == UninstallOutcomeStatus.TimedOut;
}

public sealed record BatchUninstallReport(
    IReadOnlyList<UninstallOperationResult> Results,
    bool WasCancelled,
    int TotalRequested)
{
    public int SucceededCount => Results.Count(r => r.Status == UninstallOutcomeStatus.Succeeded);
    public int FailedCount => Results.Count(r => r.Status == UninstallOutcomeStatus.Failed);
    public int TimedOutCount => Results.Count(r => r.Status == UninstallOutcomeStatus.TimedOut);
    public int NotProcessedCount => Math.Max(0, TotalRequested - Results.Count);

    public TaskRunResult ToTaskRunResult(DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        var messages = Results
            .Where(r => r.Status == UninstallOutcomeStatus.Succeeded)
            .Select(r => $"{r.App.Name} ({r.App.Id}): Uninstalled successfully.")
            .ToList();
        if (WasCancelled)
        {
            messages.Add($"Batch uninstall cancelled with {NotProcessedCount} application(s) not processed.");
        }

        var errors = Results
            .Where(r => r.Status is UninstallOutcomeStatus.Failed or UninstallOutcomeStatus.TimedOut)
            .Select(r => $"{r.App.Name} ({r.App.Id}) [{r.Status}]: {r.Message}")
            .ToList();

        return new TaskRunResult(
            "software.uninstall.batch",
            "Application Uninstall",
            startedAt,
            finishedAt,
            errors.Count == 0 && !WasCancelled,
            0,
            SucceededCount,
            FailedCount + TimedOutCount + NotProcessedCount,
            messages,
            errors);
    }
}
