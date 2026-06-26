using System.Text.Json;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class ReportService
{
    private readonly PathService _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ReportService(PathService paths)
    {
        _paths = paths;
    }

    public string LogsDirectory => _paths.LogsDirectory;

    public string? GetLastReportPath()
    {
        return null;
    }

    public async Task<string> SaveAsync(TaskRunResult result, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return string.Empty;
    }

    public static bool TryResolveReportDeleteTargets(
        string logsDirectory,
        string reportPath,
        out IReadOnlyList<string> targets)
    {
        targets = [];
        if (string.IsNullOrWhiteSpace(logsDirectory) || string.IsNullOrWhiteSpace(reportPath))
        {
            return false;
        }

        var fullReportPath = Path.GetFullPath(reportPath);
        if (!PathSafetyService.IsPathWithinOrEqual(fullReportPath, logsDirectory) ||
            !Path.GetFileName(fullReportPath).StartsWith("maintenance-", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(fullReportPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targets =
        [
            fullReportPath,
            Path.ChangeExtension(fullReportPath, ".log")
        ];
        return true;
    }
}
