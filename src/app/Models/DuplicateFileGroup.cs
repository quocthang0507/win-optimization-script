namespace WinOptimizationApp.Models;

public sealed class DuplicateFileItem
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string DirectoryPath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset LastModified { get; init; }
    public bool IsOriginal { get; set; }
    public bool IsSelected { get; set; }
}

public sealed class DuplicateFileGroup
{
    public required string Hash { get; init; }
    public required long FileSizeBytes { get; init; }
    public required IReadOnlyList<DuplicateFileItem> Files { get; init; }

    public long ReclaimableBytes => Files.Where(f => !f.IsOriginal).Sum(f => f.SizeBytes);
    public long SelectedReclaimableBytes => Files.Where(f => f.IsSelected && !f.IsOriginal).Sum(f => f.SizeBytes);
}

public sealed record DuplicateScanResult(
    string RootPath,
    IReadOnlyList<DuplicateFileGroup> Groups,
    int TotalFilesScanned)
{
    public long TotalReclaimableBytes => Groups.Sum(g => g.ReclaimableBytes);
    public int TotalDuplicateFiles => Groups.Sum(g => Math.Max(0, g.Files.Count - 1));
}
