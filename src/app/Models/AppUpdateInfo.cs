namespace WinOptimizationApp.Models;

public sealed record AppUpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    string TagName,
    string ReleaseUrl,
    string? AssetName,
    string? AssetUrl,
    bool IsUpdateAvailable);
