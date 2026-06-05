namespace WinOptimizationApp.Models;

public sealed record DiskScanProgress(
    string CurrentPath,
    long TotalBytes,
    int FileCount,
    int FolderCount,
    int SkippedCount);
