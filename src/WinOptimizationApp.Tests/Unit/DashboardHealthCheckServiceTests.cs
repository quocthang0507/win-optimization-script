using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class DashboardHealthCheckServiceTests
{
    [Fact]
    public void Analyze_CreatesCriticalStorageRecommendation_WhenSystemDriveIsAlmostFull()
    {
        var status = CreateStatus(
            systemDriveFreeBytes: 2L * 1024 * 1024 * 1024,
            systemDriveTotalBytes: 100L * 1024 * 1024 * 1024);

        var result = DashboardHealthCheckService.Analyze(status);

        Assert.Equal("Critical", result.Status);
        Assert.Contains(result.Findings, finding => finding.Id == "disk.critical" && finding.Severity == RiskLevel.High);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Id == "storage.analyze" && recommendation.ActionTag == "storage");
    }

    [Fact]
    public void Analyze_CreatesRestartRecommendation_WhenPendingRebootExists()
    {
        var status = CreateStatus(pendingReboot: true);

        var result = DashboardHealthCheckService.Analyze(status);

        Assert.Contains(result.Findings, finding => finding.Id == "reboot.pending");
        Assert.Contains(result.Recommendations, recommendation => recommendation.Id == "reboot.restart" && recommendation.ActionTag == "updates");
    }

    [Fact]
    public void Analyze_CreatesStartupRecommendation_WhenMemoryLoadIsHigh()
    {
        var status = CreateStatus(memoryLoadPercent: 92);

        var result = DashboardHealthCheckService.Analyze(status);

        Assert.Contains(result.Findings, finding => finding.Id == "memory.critical" && finding.Severity == RiskLevel.High);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Id == "performance.review" && recommendation.ActionTag == "startup");
    }

    [Fact]
    public void Analyze_TreatsMissingWingetAsWarning_NotTotalFailure()
    {
        var status = CreateStatus(wingetAvailable: false);

        var result = DashboardHealthCheckService.Analyze(status);

        Assert.True(result.Score > 0);
        Assert.Contains(result.Findings, finding => finding.Id == "winget.missing" && finding.Severity == RiskLevel.Medium);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Id == "winget.install" && recommendation.ActionTag == "updates");
    }

    [Fact]
    public void Analyze_ReturnsGood_WhenNoActionableIssuesExist()
    {
        var status = CreateStatus(isAdministrator: true, lastReportPath: @"D:\logs\maintenance.json");

        var result = DashboardHealthCheckService.Analyze(status);

        Assert.Equal(100, result.Score);
        Assert.Equal("Good", result.Status);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void Analyze_DeepScanAddsSpaceSecurityAndSpeedRecommendations()
    {
        var status = CreateStatus(isAdministrator: true, lastReportPath: @"D:\logs\maintenance.json");
        var metrics = new HealthCheckScanMetrics(2L * 1024 * 1024 * 1024, 400, 3, 2, []);

        var result = DashboardHealthCheckService.Analyze(status, metrics);

        Assert.Contains(result.Findings, finding => finding.Id == "cleanup.available");
        Assert.Contains(result.Findings, finding => finding.Id == "updates.available");
        Assert.Contains(result.Findings, finding => finding.Id == "startup.highImpact");
        Assert.Same(metrics, result.Metrics);
        Assert.True(result.Score < 100);
    }

    private static DashboardStatus CreateStatus(
        long systemDriveFreeBytes = 80L * 1024 * 1024 * 1024,
        long systemDriveTotalBytes = 200L * 1024 * 1024 * 1024,
        bool pendingReboot = false,
        bool wingetAvailable = true,
        uint memoryLoadPercent = 42,
        bool isAdministrator = false,
        string? lastReportPath = null)
    {
        return new DashboardStatus(
            "Windows 11",
            "MACHINE",
            "User",
            isAdministrator,
            TimeSpan.FromHours(6),
            "C:\\",
            systemDriveFreeBytes,
            systemDriveTotalBytes,
            pendingReboot,
            wingetAvailable,
            lastReportPath,
            "Test CPU",
            8,
            ".NET 10",
            "X64",
            "X64",
            memoryLoadPercent,
            16UL * 1024 * 1024 * 1024,
            10UL * 1024 * 1024 * 1024,
            20UL * 1024 * 1024 * 1024,
            12UL * 1024 * 1024 * 1024,
            []);
    }
}
