using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class HealthCheckScanServiceTests
{
    [Fact]
    public async Task ScanAsync_UsesPreloadedStartupAndUpdateState()
    {
        var commands = new CommandRunner();
        var updates = new[]
        {
            new WingetPackage("App One", "Vendor.AppOne", "1.0", "2.0", "winget"),
            new WingetPackage("App Two", "Vendor.AppTwo", "3.0", "4.0", "winget")
        };
        var startupEntries = new[]
        {
            Entry("High enabled", enabled: true, StartupImpactLevel.High),
            Entry("High disabled", enabled: false, StartupImpactLevel.High),
            Entry("Low enabled", enabled: true, StartupImpactLevel.Low)
        };

        var metrics = await HealthCheckScanService.ScanAsync(
            new CleanupService(commands),
            new MaintenanceCatalog(),
            new WingetService(commands),
            new StartupService(),
            knownUpdates: updates,
            knownStartupEntries: startupEntries,
            knownErrors: ["updates: cached warning"]);

        Assert.Equal(2, metrics.AvailableUpdates);
        Assert.Equal(1, metrics.HighImpactStartupItems);
        Assert.Contains("updates: cached warning", metrics.Errors);
    }

    private static StartupEntry Entry(string name, bool enabled, StartupImpactLevel impact)
    {
        return new StartupEntry(
            name,
            "HKCU Run",
            $@"C:\Apps\{name}.exe",
            enabled,
            "Standard",
            impact,
            "Review",
            CanDisable: true,
            CanRollback: true);
    }
}
