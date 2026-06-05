namespace WinOptimizationApp.Models;

public sealed record DiskScanResult(
    DiskItem Root,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long TotalBytes,
    int FileCount,
    int FolderCount,
    int SkippedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DiskItem> LargestFiles,
    IReadOnlyList<FileTypeSummary> FileTypes);
