using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class SetupConfigurationServiceTests
{
    [Fact]
    public void ExportAndImportJson_RoundTripsSetupBundle_AndValidates()
    {
        var tweaks = new TweakService(new CommandRunner(), new InMemoryRegistryAccessor());
        var catalog = new MaintenanceCatalog();
        var service = new SetupConfigurationService(tweaks, catalog);

        var bundle = service.CreateBundleFromProfile(
            "Gaming Workstation",
            new Dictionary<string, bool>
            {
                ["gaming.gameMode"] = true,
                ["ui.classicContextMenu"] = true
            },
            wingetPackageIds: ["Git.Git", "Microsoft.VisualStudioCode"],
            cleanupTaskIds: [catalog.All[0].Id],
            schedule: MaintenanceScheduleFrequency.Weekly);

        var validation = service.ValidateBundle(bundle);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);

        var json = service.ExportJson(bundle);
        var imported = service.ImportJson(json);

        Assert.Equal("Gaming Workstation", imported.Name);
        Assert.True(imported.TweakValues["gaming.gameMode"]);
        Assert.True(imported.TweakValues["ui.classicContextMenu"]);
        Assert.Contains("Git.Git", imported.WingetPackageIds);
        Assert.Equal(MaintenanceScheduleFrequency.Weekly, imported.ScheduleFrequency);

        var script = service.GeneratePowerShellScript(imported);
        Assert.Contains("winget install --id 'Git.Git'", script, StringComparison.Ordinal);
        Assert.Contains("AllowAutoGameMode", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateBundle_FlagsUnsafeWingetPackageId()
    {
        var tweaks = new TweakService(new CommandRunner(), new InMemoryRegistryAccessor());
        var catalog = new MaintenanceCatalog();
        var service = new SetupConfigurationService(tweaks, catalog);

        var bundle = new MachineSetupBundle
        {
            Name = "Invalid Bundle",
            WingetPackageIds = ["Bad;Package&Id"]
        };

        var validation = service.ValidateBundle(bundle);
        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }
}
