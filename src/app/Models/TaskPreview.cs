namespace WinOptimizationApp.Models;

public sealed record TaskPreview(
    string TaskId,
    string Summary,
    long EstimatedBytes,
    int EstimatedFileCount,
    IReadOnlyList<CleanupTargetPreview> Targets,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PlannedCommands);
