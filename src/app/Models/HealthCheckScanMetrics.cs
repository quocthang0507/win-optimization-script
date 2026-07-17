namespace WinOptimizationApp.Models;

public sealed record HealthCheckScanMetrics(
    long CleanupBytes,
    int CleanupFiles,
    int AvailableUpdates,
    int HighImpactStartupItems,
    IReadOnlyList<string> Errors);
