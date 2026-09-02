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
    IReadOnlyList<FileTypeSummary> FileTypes,
    bool IsPartial = false)
{
    public bool HasIncompleteTotals => IsPartial || SkippedCount > 0 || Errors.Count > 0;
    public IReadOnlyList<DiskItem> NewestFiles { get; init; } = [];
    public IReadOnlyList<DiskItem> OldestFiles { get; init; } = [];
    public IReadOnlyList<FileAgeSummary> FileAgeSummaries { get; init; } = [];
    public IReadOnlyList<DiskItem> DeveloperArtifacts { get; init; } = [];
}
