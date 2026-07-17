using System.Text.Json;
using System.Text;
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
        if (!Directory.Exists(LogsDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(LogsDirectory, "maintenance-*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public async Task<string> SaveAsync(TaskRunResult result, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(LogsDirectory);

        var safeTaskId = string.Concat(result.TaskId.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-'));
        var stem = $"maintenance-{result.FinishedAt:yyyyMMdd-HHmmss-fff}-{safeTaskId}";
        var jsonPath = Path.Combine(LogsDirectory, $"{stem}.json");
        var logPath = Path.Combine(LogsDirectory, $"{stem}.log");
        var temporaryJsonPath = $"{jsonPath}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryJsonPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, result, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryJsonPath, jsonPath);
            await File.WriteAllTextAsync(logPath, BuildTextLog(result), Encoding.UTF8, cancellationToken);
            return jsonPath;
        }
        finally
        {
            if (File.Exists(temporaryJsonPath))
            {
                File.Delete(temporaryJsonPath);
            }
        }
    }

    private static string BuildTextLog(TaskRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Task: {result.TaskLabel} ({result.TaskId})");
        builder.AppendLine($"Started: {result.StartedAt:O}");
        builder.AppendLine($"Finished: {result.FinishedAt:O}");
        builder.AppendLine($"Success: {result.Success}");
        builder.AppendLine($"Freed bytes: {result.FreedBytes}");
        builder.AppendLine($"Files removed: {result.FilesRemoved}");
        builder.AppendLine($"Files skipped: {result.FilesSkipped}");

        if (result.Messages.Count > 0)
        {
            builder.AppendLine("Messages:");
            foreach (var message in result.Messages)
            {
                builder.AppendLine($"- {message}");
            }
        }

        if (result.Errors.Count > 0)
        {
            builder.AppendLine("Errors:");
            foreach (var error in result.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        return builder.ToString();
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
