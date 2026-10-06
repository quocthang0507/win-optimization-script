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

    [Fact]
    public void PruneReports_RemovesOnlyExpiredMaintenanceReportsAndTheirLogs()
    {
        var service = new ReportService(new PathService(_root));
        Directory.CreateDirectory(service.LogsDirectory);
        var now = DateTimeOffset.Parse("2026-10-06T12:00:00Z");
        var oldReport = WriteFile(service.LogsDirectory, "maintenance-old.json", now.AddDays(-40));
        var oldLog = WriteFile(service.LogsDirectory, "maintenance-old.log", now.AddDays(-40));
        var recentReport = WriteFile(service.LogsDirectory, "maintenance-recent.json", now.AddDays(-5));
        var unrelated = WriteFile(service.LogsDirectory, "app-crash-old.json", now.AddDays(-400));

        var removed = service.PruneReports(30, now);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(oldReport));
        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(recentReport));
        Assert.True(File.Exists(unrelated));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PruneReports_KeepsEverythingWhenRetentionIsDisabled(int retentionDays)
    {
        var service = new ReportService(new PathService(_root));
        Directory.CreateDirectory(service.LogsDirectory);
        var report = WriteFile(service.LogsDirectory, "maintenance-ancient.json", DateTimeOffset.Now.AddYears(-5));

        Assert.Equal(0, service.PruneReports(retentionDays));
        Assert.True(File.Exists(report));
    }

    [Fact]
    public void PruneReports_ToleratesMissingLogsDirectory()
    {
        Assert.Equal(0, new ReportService(new PathService(_root)).PruneReports(30));
    }

    private static string WriteFile(string directory, string name, DateTimeOffset lastWrite)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "{}");
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
