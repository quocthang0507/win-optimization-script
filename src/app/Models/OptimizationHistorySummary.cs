namespace WinOptimizationApp.Models;

public sealed record OptimizationHistorySummary(
    long TotalFreedBytes,
    int TotalFilesRemoved,
    int TotalRuns,
    DateTimeOffset? LastRunAt
);
