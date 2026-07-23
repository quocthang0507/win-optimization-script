using WinOptimizationApp.Services;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Tests.Unit;

public sealed class UninstallerSafetyTests
{
    [Fact]
    public void IsSafeLeftoverPath_RejectsApplicationRootsAndArbitraryPaths()
    {
        Assert.False(UninstallerService.IsSafeLeftoverPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)));
        Assert.False(UninstallerService.IsSafeLeftoverPath(Path.GetPathRoot(Environment.SystemDirectory)!));
        Assert.False(UninstallerService.IsSafeLeftoverPath(Path.Combine(Path.GetTempPath(), "unrelated")));
    }

    [Fact]
    public void IsSafeAppxRemovalCandidate_ProtectsFrameworksAndCoreComponents()
    {
        Assert.False(UninstallerService.IsSafeAppxRemovalCandidate(
            "Microsoft.VCLibs.140.00",
            "Microsoft.VCLibs.140.00_14.0.0_x64__8wekyb3d8bbwe"));
        Assert.False(UninstallerService.IsSafeAppxRemovalCandidate(
            "Microsoft.DesktopAppInstaller",
            "Microsoft.DesktopAppInstaller_1.0.0_x64__8wekyb3d8bbwe"));
        Assert.True(UninstallerService.IsSafeAppxRemovalCandidate(
            "Microsoft.BingWeather",
            "Microsoft.BingWeather_4.0.0_x64__8wekyb3d8bbwe"));
    }

    [Fact]
    public void ParseUninstallCommand_HandlesUnquotedExecutablePathsAndMsiMaintenanceCommands()
    {
        var parsed = UninstallerService.ParseUninstallCommand(@"C:\Program Files\Example App\uninstall.exe /S");

        Assert.Equal(@"C:\Program Files\Example App\uninstall.exe", parsed.Exe);
        Assert.Equal("/S", parsed.Args);
        Assert.Equal("/X{00000000-0000-0000-0000-000000000000} /norestart",
            UninstallerService.NormalizeMsiUninstallArguments("/I{00000000-0000-0000-0000-000000000000}"));
    }

    [Fact]
    public async Task UninstallAppAsync_DoesNotExecuteUntrustedPayloadCommand()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"winopt-untrusted-{Guid.NewGuid():N}.txt");
        var app = new InstalledApp
        {
            Id = "HKCU\\NotARealInstalledApp",
            Name = "Not installed",
            Version = "1.0",
            Publisher = "Test",
            Source = "Registry",
            InstallLocation = string.Empty,
            UninstallString = $"powershell.exe -NoProfile -Command \"Set-Content -LiteralPath '{marker}' -Value unsafe\""
        };

        var result = await new UninstallerService(new CommandRunner()).UninstallAppAsync(app);

        Assert.False(result);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task DeleteLeftoversAsync_RejectsPathsNotAuthorizedByItsOwnScan()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winopt-leftover-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(path, "keep");
        try
        {
            var result = await new UninstallerService(new CommandRunner()).DeleteLeftoversAsync([path]);

            Assert.False(result);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
