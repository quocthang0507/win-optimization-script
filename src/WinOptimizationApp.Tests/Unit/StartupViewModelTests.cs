using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.ViewModels;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class StartupViewModelTests
{
    private static StartupEntry CreateEntry(
        string name,
        string command,
        string source,
        StartupImpactLevel impact = StartupImpactLevel.Low,
        bool enabled = true,
        bool canDisable = true,
        bool canRollback = false)
    {
        return new StartupEntry(
            name,
            source,
            command,
            enabled,
            "Safe",
            impact,
            "Keep enabled",
            canDisable,
            canRollback);
    }

    [Fact]
    public void LoadEntries_ComputesMetricsAccurately()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        var entries = new[]
        {
            CreateEntry("Steam", "steam.exe", "HKCU\\Run", StartupImpactLevel.High, enabled: true),
            CreateEntry("Discord", "discord.exe", "HKCU\\Run", StartupImpactLevel.Medium, enabled: true),
            CreateEntry("OneDrive", "onedrive.exe", "HKLM\\Run", StartupImpactLevel.Low, enabled: false),
            CreateEntry("BackupTool", "backup.exe", "Common Startup", StartupImpactLevel.High, enabled: false)
        };

        vm.LoadEntries(entries);

        Assert.Equal(4, vm.TotalCount);
        Assert.Equal(4, vm.FilteredCount);
        Assert.Equal(2, vm.EnabledCount);
        Assert.Equal(2, vm.DisabledCount);
        Assert.Equal(2, vm.HighImpactCount);
    }

    [Fact]
    public void StatusFilter_FiltersEnabledAndDisabled()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        vm.LoadEntries([
            CreateEntry("App1", "cmd1", "HKCU\\Run", enabled: true),
            CreateEntry("App2", "cmd2", "HKCU\\Run", enabled: false)
        ]);

        vm.StatusFilter = StartupStatusFilter.Enabled;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("App1", vm.FilteredEntries[0].Name);

        vm.StatusFilter = StartupStatusFilter.Disabled;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("App2", vm.FilteredEntries[0].Name);

        vm.StatusFilter = StartupStatusFilter.All;
        Assert.Equal(2, vm.FilteredEntries.Count);
    }

    [Fact]
    public void ImpactFilter_FiltersByImpactLevel()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        vm.LoadEntries([
            CreateEntry("HeavyApp", "cmd1", "HKCU\\Run", StartupImpactLevel.High),
            CreateEntry("MediumApp", "cmd2", "HKCU\\Run", StartupImpactLevel.Medium),
            CreateEntry("LightApp", "cmd3", "HKCU\\Run", StartupImpactLevel.Low)
        ]);

        vm.ImpactFilter = StartupImpactFilter.High;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("HeavyApp", vm.FilteredEntries[0].Name);

        vm.ImpactFilter = StartupImpactFilter.Medium;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("MediumApp", vm.FilteredEntries[0].Name);

        vm.ImpactFilter = StartupImpactFilter.Low;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("LightApp", vm.FilteredEntries[0].Name);
    }

    [Fact]
    public void SourceFilter_FiltersByUserAndMachineSources()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        vm.LoadEntries([
            CreateEntry("UserApp", "cmd1", "HKCU\\Software\\Microsoft\\Windows\\Run"),
            CreateEntry("MachineApp", "cmd2", "HKLM\\Software\\Microsoft\\Windows\\Run"),
            CreateEntry("FolderApp", "cmd3", "User Startup folder shortcut")
        ]);

        vm.SourceFilter = StartupSourceFilter.Machine;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("MachineApp", vm.FilteredEntries[0].Name);

        vm.SourceFilter = StartupSourceFilter.StartupFolder;
        Assert.Single(vm.FilteredEntries);
        Assert.Equal("FolderApp", vm.FilteredEntries[0].Name);

        vm.SourceFilter = StartupSourceFilter.User;
        Assert.Equal(2, vm.FilteredEntries.Count);
        Assert.Contains(vm.FilteredEntries, e => e.Name == "UserApp");
        Assert.Contains(vm.FilteredEntries, e => e.Name == "FolderApp");
    }

    [Fact]
    public void SortOrder_SortsByNameAndImpact()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        vm.LoadEntries([
            CreateEntry("Zeta", "cmd1", "HKCU", StartupImpactLevel.Low),
            CreateEntry("Alpha", "cmd2", "HKCU", StartupImpactLevel.High),
            CreateEntry("Beta", "cmd3", "HKCU", StartupImpactLevel.Medium)
        ]);

        vm.SortOrder = StartupSortOrder.Name;
        Assert.Equal("Alpha", vm.FilteredEntries[0].Name);
        Assert.Equal("Beta", vm.FilteredEntries[1].Name);
        Assert.Equal("Zeta", vm.FilteredEntries[2].Name);

        vm.SortOrder = StartupSortOrder.Impact;
        Assert.Equal("Alpha", vm.FilteredEntries[0].Name); // High first
        Assert.Equal("Beta", vm.FilteredEntries[1].Name);  // Medium second
        Assert.Equal("Zeta", vm.FilteredEntries[2].Name);  // Low third
    }

    [Fact]
    public void ResetFilters_RestoresAllDefaults()
    {
        var service = new StartupService();
        using var operations = new OperationService();
        var vm = new StartupViewModel(service, operations);

        vm.LoadEntries([
            CreateEntry("App1", "cmd1", "HKCU", StartupImpactLevel.High, enabled: true),
            CreateEntry("App2", "cmd2", "HKLM", StartupImpactLevel.Low, enabled: false)
        ]);

        vm.SearchQuery = "App1";
        vm.StatusFilter = StartupStatusFilter.Disabled;
        vm.ImpactFilter = StartupImpactFilter.Low;
        vm.SourceFilter = StartupSourceFilter.Machine;
        vm.SortOrder = StartupSortOrder.Name;
        vm.ActionableOnly = true;

        vm.ResetFilters();

        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal(StartupStatusFilter.All, vm.StatusFilter);
        Assert.Equal(StartupImpactFilter.All, vm.ImpactFilter);
        Assert.Equal(StartupSourceFilter.All, vm.SourceFilter);
        Assert.Equal(StartupSortOrder.Impact, vm.SortOrder);
        Assert.False(vm.ActionableOnly);
        Assert.Equal(2, vm.FilteredCount);
    }

    [Theory]
    [InlineData("HKCU\\Software\\...", true, false)]
    [InlineData("User Startup", true, false)]
    [InlineData("HKLM\\Software\\...", false, true)]
    [InlineData("Common Startup", false, true)]
    public void SourceHelpers_RecognizeUserAndMachine(string source, bool isUser, bool isMachine)
    {
        Assert.Equal(isUser, StartupViewModel.IsUserStartupSource(source));
        Assert.Equal(isMachine, StartupViewModel.IsMachineStartupSource(source));
    }
}
