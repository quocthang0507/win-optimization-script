namespace WinOptimizationApp.Models;

public sealed record OneClickItemDefinition(
    string TaskId,
    bool DefaultSelected,
    bool IsPerformanceAction);

public sealed record OneClickTaskPreview(
    MaintenanceTask Task,
    TaskPreview Preview,
    bool IsPerformanceAction);

public sealed record OneClickPreview(IReadOnlyList<OneClickTaskPreview> Tasks)
{
    public long EstimatedBytes => Tasks.Sum(task => task.Preview.EstimatedBytes);
    public int EstimatedFileCount => Tasks.Sum(task => task.Preview.EstimatedFileCount);
}

public sealed record OneClickProgress(
    int Current,
    int Total,
    string TaskId,
    bool IsRunning);

public sealed record OneClickRunSummary(
    IReadOnlyList<TaskRunResult> Results,
    bool Cancelled)
{
    public long FreedBytes => Results.Sum(result => result.FreedBytes);
    public int FilesRemoved => Results.Sum(result => result.FilesRemoved);
    public int FilesSkipped => Results.Sum(result => result.FilesSkipped);
}
