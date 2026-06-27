using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class WingetServiceTests
{
    [Fact]
    public void BuildUpgradeAllArguments_DisablesInteractiveAgreementPrompts()
    {
        var arguments = WingetService.BuildUpgradeAllArguments();

        Assert.Contains("--all", arguments);
        Assert.Contains("--silent", arguments);
        Assert.Contains("--accept-package-agreements", arguments);
        Assert.Contains("--accept-source-agreements", arguments);
        Assert.Contains("--disable-interactivity", arguments);
    }

    [Fact]
    public void BuildUpgradeArguments_TargetsSinglePackageAndDisablesInteractiveAgreementPrompts()
    {
        var package = new WingetPackage(
            "Example App",
            "Vendor.Example",
            "1.0.0",
            "2.0.0",
            "winget");

        var arguments = WingetService.BuildUpgradeArguments(package);

        Assert.Contains("upgrade --id \"Vendor.Example\" --exact", arguments);
        Assert.Contains("--source \"winget\"", arguments);
        Assert.Contains("--silent", arguments);
        Assert.Contains("--accept-package-agreements", arguments);
        Assert.Contains("--accept-source-agreements", arguments);
        Assert.Contains("--disable-interactivity", arguments);
    }

    [Fact]
    public void BuildInstallArguments_TargetsSinglePackageAndDisablesInteractiveAgreementPrompts()
    {
        var packageId = "Google.Chrome";

        var arguments = WingetService.BuildInstallArguments(packageId);

        Assert.Contains("install --id \"Google.Chrome\" --exact", arguments);
        Assert.Contains("--silent", arguments);
        Assert.Contains("--accept-package-agreements", arguments);
        Assert.Contains("--accept-source-agreements", arguments);
        Assert.Contains("--disable-interactivity", arguments);
    }
}
