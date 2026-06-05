namespace WinOptimizationApp.Models;

public sealed record WingetPackage(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string Source);
