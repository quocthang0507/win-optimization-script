namespace WinOptimizationApp.Models;

public sealed record TaskRunResult(
    string TaskId,
    string TaskLabel,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    bool Success,
    long FreedBytes,
    int FilesRemoved,
    int FilesSkipped,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Errors);
