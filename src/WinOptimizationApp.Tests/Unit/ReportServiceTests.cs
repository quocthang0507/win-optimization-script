using System.Text.Json;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ReportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_WritesJsonAndTextLogAndReturnsLatestReport()
    {
        var service = new ReportService(new PathService(_root));
        var result = new TaskRunResult(
            "cleanup.temp",
            "Temporary files",
            DateTimeOffset.Parse("2026-07-17T10:00:00+07:00"),
            DateTimeOffset.Parse("2026-07-17T10:00:05+07:00"),
            true,
            1234,
            3,
            1,
            ["Cleanup completed."],
            []);

        var reportPath = await service.SaveAsync(result);

        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(Path.ChangeExtension(reportPath, ".log")));
        Assert.Equal(reportPath, service.GetLastReportPath());
        var saved = JsonSerializer.Deserialize<TaskRunResult>(await File.ReadAllTextAsync(reportPath));
        Assert.NotNull(saved);
        Assert.Equal(result.TaskId, saved.TaskId);
        Assert.Equal(result.FreedBytes, saved.FreedBytes);
        Assert.Equal(result.Messages, saved.Messages);
        Assert.Equal(result.Errors, saved.Errors);
        Assert.Contains("Files removed: 3", await File.ReadAllTextAsync(Path.ChangeExtension(reportPath, ".log")));
    }

    [Fact]
    public void PathService_UsesDedicatedLogsAndBackupsDirectories()
    {
        var paths = new PathService(_root);

        Assert.Equal(Path.Combine(Path.GetFullPath(_root), "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(_root), "backups"), paths.BackupsDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
