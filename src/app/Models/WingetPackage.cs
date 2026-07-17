namespace WinOptimizationApp.Models;

public sealed record WingetPackage(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string Source);

public sealed record WingetPackageUpgradeResult(
    WingetPackage Package,
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed record WingetPackageDownloadRequest(
    WingetPackage Package,
    string DownloadDirectory);

public sealed class WingetPackageDetails
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string PublisherUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Homepage { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string LicenseUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleaseNotesUrl { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string InstallerUrl { get; set; } = string.Empty;
    public string InstallerType { get; set; } = string.Empty;
}
