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

    [Fact]
    public async Task GetRecentReports_ReturnsDeserializedReportsInDescendingOrder()
    {
        var service = new ReportService(new PathService(_root));
        var result1 = new TaskRunResult(
            "cleanup.temp",
            "Temporary files",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-9),
            true,
            1000,
            5,
            0,
            ["Success"],
            []);
        var result2 = new TaskRunResult(
            "cleanup.cache",
            "Cache files",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(-4),
            true,
            2500,
            10,
            0,
            ["Success"],
            []);

        await service.SaveAsync(result1);
        await Task.Delay(50);
        await service.SaveAsync(result2);

        var recent = service.GetRecentReports(10);
        Assert.NotNull(recent);
        Assert.True(recent.Count >= 2);
        Assert.Equal("cleanup.cache", recent[0].TaskId);
        Assert.Equal("cleanup.temp", recent[1].TaskId);
    }

    [Fact]
    public void GetRecentReports_SkipsCorruptedFilesAndToleratesMissingDirectory()
    {
        var emptyService = new ReportService(new PathService(Path.Combine(_root, "nonexistent")));
        Assert.Empty(emptyService.GetRecentReports(10));
        Assert.Empty(emptyService.GetRecentReports(0));

        var service = new ReportService(new PathService(_root));
        Directory.CreateDirectory(service.LogsDirectory);
        File.WriteAllText(Path.Combine(service.LogsDirectory, "maintenance-corrupted.json"), "{ invalid json");

        var reports = service.GetRecentReports(10);
        Assert.NotNull(reports);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task GetHistorySummary_AggregatesFreedBytesAndFilesRemoved()
    {
        var service = new ReportService(new PathService(_root));
        var result1 = new TaskRunResult(
            "cleanup.temp",
            "Temporary files",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-9),
            true,
            3000,
            7,
            0,
            ["Success"],
            []);
        var result2 = new TaskRunResult(
            "cleanup.logs",
            "Log files",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(-4),
            true,
            5000,
            12,
            0,
            ["Success"],
            []);

        await service.SaveAsync(result1);
        await service.SaveAsync(result2);

        var summary = service.GetHistorySummary();
        Assert.NotNull(summary);
        Assert.Equal(8000, summary.TotalFreedBytes);
        Assert.Equal(19, summary.TotalFilesRemoved);
        Assert.Equal(2, summary.TotalRuns);
        Assert.NotNull(summary.LastRunAt);
    }

    [Fact]
    public void GetHistorySummary_ReturnsZeroWhenNoReports()
    {
        var service = new ReportService(new PathService(_root));
        var summary = service.GetHistorySummary();
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalFreedBytes);
        Assert.Equal(0, summary.TotalFilesRemoved);
        Assert.Equal(0, summary.TotalRuns);
        Assert.Null(summary.LastRunAt);
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
