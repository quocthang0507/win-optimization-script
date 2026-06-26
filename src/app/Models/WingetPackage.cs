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
