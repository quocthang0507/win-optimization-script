namespace WinOptimizationApp.Models;

public sealed record DiskScanOptions(
    string RootPath,
    bool IncludeHidden = false,
    bool IncludeSystem = false,
    bool FollowReparsePoints = false,
    long MinimumFileSize = 0,
    IReadOnlyList<string>? ExcludedPaths = null);
