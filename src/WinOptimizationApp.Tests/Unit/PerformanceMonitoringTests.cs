using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class PerformanceMonitoringTests
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsValidSystemMetrics()
    {
        var service = new PerformanceMonitoringService();
        var metrics = await service.GetMetricsAsync();

        Assert.NotNull(metrics);
        Assert.True(metrics.CpuUsagePercent is >= 0 and <= 100);
        Assert.True(metrics.RamUsagePercent is >= 0 and <= 100);
        Assert.True(metrics.RamTotalBytes > 0);
        Assert.True(metrics.RamUsedBytes >= 0 && metrics.RamUsedBytes <= metrics.RamTotalBytes);
        Assert.True(metrics.DiskTotalBytes > 0);
        Assert.True(metrics.DiskFreeBytes >= 0 && metrics.DiskFreeBytes <= metrics.DiskTotalBytes);
        Assert.True(metrics.DiskUsagePercent is >= 0 and <= 100);
        Assert.True(metrics.DownloadBytesPerSecond >= 0);
        Assert.True(metrics.UploadBytesPerSecond >= 0);

        var nextSample = await service.GetMetricsAsync();
        Assert.True(double.IsFinite(nextSample.DownloadBytesPerSecond));
        Assert.True(double.IsFinite(nextSample.UploadBytesPerSecond));
        Assert.True(nextSample.DownloadBytesPerSecond >= 0);
        Assert.True(nextSample.UploadBytesPerSecond >= 0);
    }
}
