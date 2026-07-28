using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class HealthCheckScanService
{
    private static readonly string[] SafeCleanupTaskIds =
    [
        "cleanup.temp",
        "cleanup.shaders",
        "cleanup.recyclebin"
    ];

    public static async Task<HealthCheckScanMetrics> ScanAsync(
        CleanupService cleanup,
        MaintenanceCatalog catalog,
        WingetService winget,
        StartupService startup,
        CancellationToken cancellationToken = default,
        IReadOnlyList<WingetPackage>? knownUpdates = null,
        IReadOnlyList<StartupEntry>? knownStartupEntries = null,
        IReadOnlyList<string>? knownErrors = null)
    {
        long cleanupBytes = 0;
        var cleanupFiles = 0;
        var errors = knownErrors?.ToList() ?? [];

        foreach (var taskId in SafeCleanupTaskIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var preview = await cleanup.PreviewAsync(catalog.GetById(taskId), cancellationToken);
                cleanupBytes += preview.EstimatedBytes;
                cleanupFiles += preview.EstimatedFileCount;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{taskId}: {ex.Message}");
            }
        }

        IReadOnlyList<WingetPackage> updates = knownUpdates ?? [];
        if (knownUpdates is null)
        {
            try
            {
                updates = await winget.ScanAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"updates: {ex.Message}");
            }
        }

        IReadOnlyList<StartupEntry> startupEntries = knownStartupEntries ?? [];
        if (knownStartupEntries is null)
        {
            try
            {
                startupEntries = await startup.ScanAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"startup: {ex.Message}");
            }
        }

        return new HealthCheckScanMetrics(
            cleanupBytes,
            cleanupFiles,
            updates.Count,
            startupEntries.Count(entry => entry.Enabled && entry.Impact == StartupImpactLevel.High),
            errors);
    }
}
