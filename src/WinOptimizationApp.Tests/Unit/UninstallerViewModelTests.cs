using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.ViewModels;

namespace WinOptimizationApp.Tests.Unit;

public sealed class UninstallerViewModelTests
{
    private static InstalledApp CreateSampleApp(string id, string name, string publisher, string source) =>
        new()
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            Publisher = publisher,
            UninstallString = "uninstall.exe",
            InstallLocation = @"C:\Program Files\Sample",
            Source = source
        };

    [Fact]
    public void LoadApps_AndFilterBySearchAndSource_WorksCorrectly()
    {
        var vm = new UninstallerViewModel(
            new UninstallerService(new CommandRunner()),
            new OperationService());

        vm.LoadApps(
        [
            CreateSampleApp("Git.Git", "Git", "Git Project", "Winget"),
            CreateSampleApp("HKLM\\7zip", "7-Zip", "Igor Pavlov", "HKLM"),
            CreateSampleApp("HKCU\\Discord", "Discord", "Discord Inc.", "HKCU")
        ]);

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3, vm.FilteredCount);

        vm.SourceFilter = "Winget";
        Assert.Equal(1, vm.FilteredCount);
        Assert.Equal("Git", vm.FilteredItems[0].Name);

        vm.SourceFilter = "Registry";
        Assert.Equal(2, vm.FilteredCount);

        vm.SearchQuery = " Igor ";
        Assert.Equal(1, vm.FilteredCount);
        Assert.Equal("7-Zip", vm.FilteredItems[0].Name);

        vm.SourceFilter = "All";
        vm.SearchQuery = string.Empty;
        Assert.Equal(3, vm.FilteredCount);
    }
}
