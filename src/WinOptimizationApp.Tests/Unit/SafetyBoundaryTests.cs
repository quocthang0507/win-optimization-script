using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class SafetyBoundaryTests
{
    [Fact]
    public void MaintenanceCatalog_TryGetByIdRejectsUnknownTaskIds()
    {
        var catalog = new MaintenanceCatalog();

        var found = catalog.TryGetById("cleanup.temp && powershell", out var task);

        Assert.False(found);
        Assert.Null(task);
    }

    [Fact]
    public void MaintenanceCatalog_NewCleanupTasksStayPreviewableAndNonAdministrative()
    {
        var catalog = new MaintenanceCatalog();

        var shaderCache = catalog.GetById("cleanup.shaders");
        var crashDumps = catalog.GetById("cleanup.crashdumps");

        Assert.Equal(RiskLevel.Safe, shaderCache.RiskLevel);
        Assert.True(shaderCache.CanPreview);
        Assert.False(shaderCache.RequiresAdmin);
        Assert.Equal(RiskLevel.Medium, crashDumps.RiskLevel);
        Assert.True(crashDumps.CanPreview);
        Assert.True(crashDumps.RequiresConfirmation);
        Assert.False(crashDumps.RequiresAdmin);
    }

    [Fact]
    public void ReportService_TryResolveReportDeleteTargetsRejectsPathsOutsideLogsDirectory()
    {
        var logsDirectory = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"), "logs");
        var outsideReport = Path.Combine(Path.GetTempPath(), "maintenance-20260606-000000-cleanup.temp.json");

        var resolved = ReportService.TryResolveReportDeleteTargets(logsDirectory, outsideReport, out var targets);

        Assert.False(resolved);
        Assert.Empty(targets);
    }

    [Fact]
    public void ReportService_TryResolveReportDeleteTargetsReturnsJsonAndMatchingLogInsideLogsDirectory()
    {
        var logsDirectory = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"), "logs");
        var report = Path.Combine(logsDirectory, "maintenance-20260606-000000-cleanup.temp.json");

        var resolved = ReportService.TryResolveReportDeleteTargets(logsDirectory, report, out var targets);

        Assert.True(resolved);
        Assert.Equal(
            [
                Path.GetFullPath(report),
                Path.ChangeExtension(Path.GetFullPath(report), ".log")
            ],
            targets);
    }

    [Fact]
    public async Task CommandRunner_RunCaptureAsyncPropagatesCancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new CommandRunner().RunCaptureAsync("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 5\"", cts.Token));
    }
}
