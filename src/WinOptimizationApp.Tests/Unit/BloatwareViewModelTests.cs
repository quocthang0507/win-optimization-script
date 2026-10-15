using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.ViewModels;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class BloatwareViewModelTests
{
    private static InstalledApp CreateTestApp(string id, string name, string publisher = "Microsoft") =>
        new()
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            Publisher = publisher,
            UninstallString = string.Empty,
            InstallLocation = string.Empty,
            Source = "AppX"
        };

    [Fact]
    public void LoadApps_PopulatesAllAndFilteredItems()
    {
        var uninstaller = new UninstallerService(new CommandRunner());
        using var operations = new OperationService();
        var vm = new BloatwareViewModel(uninstaller, operations);

        var apps = new[]
        {
            CreateTestApp("Microsoft.XboxApp", "Xbox App"),
            CreateTestApp("Microsoft.BingWeather", "Weather"),
            CreateTestApp("SpotifyAB.SpotifyMusic", "Spotify", "Spotify AB")
        };

        vm.LoadApps(apps);

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3, vm.FilteredCount);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanRemove);
        Assert.False(vm.CanClearSelection);
        Assert.True(vm.CanSelectAll);
    }

    [Fact]
    public void SearchQuery_FiltersItemsByQuery()
    {
        var uninstaller = new UninstallerService(new CommandRunner());
        using var operations = new OperationService();
        var vm = new BloatwareViewModel(uninstaller, operations);

        var apps = new[]
        {
            CreateTestApp("Microsoft.XboxApp", "Xbox App"),
            CreateTestApp("Microsoft.BingWeather", "Weather"),
            CreateTestApp("SpotifyAB.SpotifyMusic", "Spotify", "Spotify AB")
        };

        vm.LoadApps(apps);

        vm.SearchQuery = "spot";
        Assert.Equal(1, vm.FilteredCount);
        Assert.Equal("Spotify", vm.FilteredItems[0].Name);

        vm.SearchQuery = "MICROSOFT";
        Assert.Equal(2, vm.FilteredCount);

        vm.SearchQuery = "";
        Assert.Equal(3, vm.FilteredCount);
    }

    [Fact]
    public void SelectAllFiltered_OnlySelectsFilteredItems()
    {
        var uninstaller = new UninstallerService(new CommandRunner());
        using var operations = new OperationService();
        var vm = new BloatwareViewModel(uninstaller, operations);

        var apps = new[]
        {
            CreateTestApp("Microsoft.XboxApp", "Xbox App"),
            CreateTestApp("Microsoft.BingWeather", "Weather"),
            CreateTestApp("SpotifyAB.SpotifyMusic", "Spotify", "Spotify AB")
        };

        vm.LoadApps(apps);
        vm.SearchQuery = "weather";
        vm.SelectAllFiltered();

        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.CanRemove);
        Assert.True(vm.CanClearSelection);

        vm.SearchQuery = "";
        var weatherItem = vm.AllItems.First(i => i.Name == "Weather");
        var xboxItem = vm.AllItems.First(i => i.Name == "Xbox App");
        Assert.True(weatherItem.IsSelected);
        Assert.False(xboxItem.IsSelected);
    }

    [Fact]
    public void ClearSelection_DeselectsAllItems()
    {
        var uninstaller = new UninstallerService(new CommandRunner());
        using var operations = new OperationService();
        var vm = new BloatwareViewModel(uninstaller, operations);

        var apps = new[]
        {
            CreateTestApp("App1", "One"),
            CreateTestApp("App2", "Two")
        };

        vm.LoadApps(apps);
        vm.SelectAllFiltered();
        Assert.Equal(2, vm.SelectedCount);

        vm.ClearSelection();
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanRemove);
    }

    [Fact]
    public async Task RemoveSelectedAppsAsync_ReturnsZeroWhenNothingSelected()
    {
        var uninstaller = new UninstallerService(new CommandRunner());
        using var operations = new OperationService();
        var vm = new BloatwareViewModel(uninstaller, operations);

        var result = await vm.RemoveSelectedAppsAsync();
        Assert.Equal((0, 0), result);
    }
}
