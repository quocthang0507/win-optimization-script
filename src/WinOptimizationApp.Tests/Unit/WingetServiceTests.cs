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

    [Fact]
    public void BuildDownloadArguments_TargetsPackageAndQuotesDownloadDirectory()
    {
        var package = new WingetPackage("Example App", "Vendor.Example", "1.0.0", "2.0.0", "winget");

        var arguments = WingetService.BuildDownloadArguments(package, @"C:\Users\Example User\Downloads\Updates");

        Assert.Contains("download --id \"Vendor.Example\" --exact", arguments);
        Assert.Contains("--source \"winget\"", arguments);
        Assert.Contains("--download-directory \"C:\\Users\\Example User\\Downloads\\Updates\"", arguments);
        Assert.Contains("--disable-interactivity", arguments);
    }

    [Theory]
    [InlineData("Google.Chrome", true)]
    [InlineData("Vendor.Package-Preview_1", true)]
    [InlineData("Vendor.Package\" --scope machine", false)]
    [InlineData("", false)]
    public void IsValidPackageId_RejectsCommandLineMetacharacters(string packageId, bool expected)
    {
        Assert.Equal(expected, WingetService.IsValidPackageId(packageId));
    }

    [Fact]
    public void IsSafeDownloadDirectory_BlocksDriveAndWindowsRoots()
    {
        Assert.False(WingetService.IsSafeDownloadDirectory(Path.GetPathRoot(Environment.SystemDirectory)!));
        Assert.False(WingetService.IsSafeDownloadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        Assert.True(WingetService.IsSafeDownloadDirectory(Path.Combine(Path.GetTempPath(), "WinOptimizationDownloads")));
    }

    [Fact]
    public void ParseShowOutput_ParsesStandardManifestCorrectly()
    {
        var yamlOutput = @"PackageIdentifier: Git.Git
PackageVersion: 2.55.0.3
Moniker: git
Description: 'Git is a free and open source distributed version control system designed
  to handle everything from small to very large projects with speed and efficiency.

  Git for Windows focuses on offering a lightweight, native set of tools that bring
  the full feature set of the Git SCM to Windows while providing appropriate user
  interfaces for experienced Git users and novices alike.'
ShortDescription: A free and open source distributed version control system
License: GPL-2.0
LicenseUrl: https://github.com/git-for-windows/build-extra/blob/HEAD/LICENSE.txt
PackageName: Git
PackageUrl: https://gitforwindows.org/
Publisher: The Git Development Community
PublisherSupportUrl: https://github.com/git-for-windows/git/issues
PublisherUrl: https://gitforwindows.org/
ReleaseNotes: 'Changes since Git for Windows v2.55.0(2) (July 2nd 2026)

  New Features

  - Comes with Git Credential Manager v2.9.0.

  Bug Fixes

  - Fixes heap overflows in the credential helper wincred, see GHSA-rxqw-wxqg-g7hw
  for full details.'
ReleaseNotesUrl: https://github.com/git-for-windows/git/releases/tag/v2.55.0.windows.3";

        // Use reflection to call the private static method ParseShowOutput
        var method = typeof(WingetService).GetMethod("ParseShowOutput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var details = (WingetPackageDetails)method.Invoke(null, new object[] { "Git.Git", yamlOutput })!;

        Assert.NotNull(details);
        Assert.Equal("Git.Git", details.Id);
        Assert.Equal("Git", details.Name);
        Assert.Equal("2.55.0.3", details.Version);
        Assert.Equal("The Git Development Community", details.Publisher);
        Assert.Equal("https://gitforwindows.org/", details.PublisherUrl);
        Assert.Equal("https://gitforwindows.org/", details.Homepage);
        Assert.Equal("GPL-2.0", details.License);
        Assert.Equal("https://github.com/git-for-windows/build-extra/blob/HEAD/LICENSE.txt", details.LicenseUrl);
        Assert.Equal("https://github.com/git-for-windows/git/releases/tag/v2.55.0.windows.3", details.ReleaseNotesUrl);
        Assert.Contains("Git is a free and open source", details.Description);
        Assert.Contains("Comes with Git Credential Manager", details.ReleaseNotes);
    }
}
