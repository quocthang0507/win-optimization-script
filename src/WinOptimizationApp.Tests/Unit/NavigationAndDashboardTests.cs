using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class NavigationAndDashboardTests
{
    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Vietnamese)]
    public void AllNavigationGroupKeys_AreLocalized(AppLanguage language)
    {
        var localization = new LocalizationService(language);

        string[] groupKeys =
        [
            "nav.group.cleanup",
            "nav.group.optimize",
            "nav.group.apps",
            "nav.group.tools",
            "dashboard.liveMonitor",
            "dashboard.liveMonitorSubtitle",
            "dashboard.networkActivity",
            "dashboard.historyTrend",
            "dashboard.totalFreed",
            "dashboard.totalCleanedFiles",
            "dashboard.totalRuns",
            "dashboard.noHistory"
        ];

        foreach (var key in groupKeys)
        {
            var value = localization.Get(key);
            Assert.False(string.IsNullOrWhiteSpace(value), $"Key {key} returned empty string for {language}");
            Assert.NotEqual(key, value); // Should not fall back to raw key name
        }
    }

    [Fact]
    public void OptimizationHistorySummary_HoldsValidMetrics()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var summary = new OptimizationHistorySummary(1024 * 1024 * 50, 150, 5, timestamp);

        Assert.Equal(52428800, summary.TotalFreedBytes);
        Assert.Equal(150, summary.TotalFilesRemoved);
        Assert.Equal(5, summary.TotalRuns);
        Assert.Equal(timestamp, summary.LastRunAt);
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(500, "500 B/s")]
    [InlineData(1024, "KB/s")]
    [InlineData(1024 * 500, "KB/s")]
    [InlineData(1024 * 1024 * 2, "MB/s")]
    public void SpeedFormatting_ProducesExpectedScale(long bytesPerSec, string expectedSuffix)
    {
        var result = $"{Formatters.FormatBytes(bytesPerSec)}/s";
        Assert.EndsWith(expectedSuffix, result);
    }
}
