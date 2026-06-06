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
        return !Directory.Exists(_paths.LogsDirectory)
            ? null
            : Directory.GetFiles(_paths.LogsDirectory, "maintenance-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public async Task<string> SaveAsync(TaskRunResult result, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.LogsDirectory);

        var stamp = result.StartedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss");
        var jsonPath = Path.Combine(_paths.LogsDirectory, $"maintenance-{stamp}-{result.TaskId}.json");
        var textPath = Path.ChangeExtension(jsonPath, ".log");

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(result, _jsonOptions), cancellationToken);

        var lines = new List<string>
        {
            $"Task: {result.TaskLabel} ({result.TaskId})",
            $"Started: {result.StartedAt.LocalDateTime}",
            $"Finished: {result.FinishedAt.LocalDateTime}",
            $"Success: {result.Success}",
            $"Freed: {Formatters.FormatBytes(result.FreedBytes)}",
            $"Removed: {result.FilesRemoved}",
            $"Skipped: {result.FilesSkipped}",
            string.Empty,
            "Messages:"
        };
        lines.AddRange(result.Messages.Select(message => $"- {message}"));
        lines.Add(string.Empty);
        lines.Add("Errors:");
        lines.AddRange(result.Errors.Select(error => $"- {error}"));

        await File.WriteAllLinesAsync(textPath, lines, cancellationToken);
        return jsonPath;
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
