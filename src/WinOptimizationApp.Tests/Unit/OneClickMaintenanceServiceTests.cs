using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class OneClickMaintenanceServiceTests
{
    private readonly MaintenanceCatalog _catalog = new();

    [Fact]
    public void Defaults_AreSafeAndIncludeCleanupAndPerformance()
    {
        var defaults = OneClickMaintenanceService.Items
            .Where(item => item.DefaultSelected)
            .ToList();

        Assert.Contains(defaults, item => item.TaskId == "cleanup.temp");
        Assert.Contains(defaults, item => item.TaskId == "cleanup.browser");
        Assert.Contains(defaults, item => item.TaskId == "network.dns" && item.IsPerformanceAction);
        Assert.All(defaults, item => Assert.NotEqual(RiskLevel.High, _catalog.GetById(item.TaskId).RiskLevel));
    }

    [Fact]
    public void SystemCleanupItems_AreAvailableButRemainOptIn()
    {
        string[] expectedIds =
        [
            "cleanup.shaders",
            "cleanup.errorreports",
            "cleanup.prefetch",
            "cleanup.defenderlogs",
            "cleanup.systemdumps",
            "cleanup.windowsupdate"
        ];

        foreach (var taskId in expectedIds)
        {
            var item = Assert.Single(OneClickMaintenanceService.Items, item => item.TaskId == taskId);
            Assert.False(item.DefaultSelected);
            Assert.True(_catalog.GetById(taskId).CanPreview);
        }
    }

    [Fact]
    public void EveryOneClickItem_MapsToAUniqueCatalogTask()
    {
        Assert.Equal(
            OneClickMaintenanceService.Items.Count,
            OneClickMaintenanceService.Items.Select(item => item.TaskId).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(OneClickMaintenanceService.Items, item => Assert.True(_catalog.TryGetById(item.TaskId, out _)));
    }
}
