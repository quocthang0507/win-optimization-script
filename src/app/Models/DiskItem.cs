namespace WinOptimizationApp.Models;

public sealed class DiskItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public double PercentOfParent { get; set; }
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public string Extension { get; init; } = string.Empty;
    public string ScanStatus { get; set; } = "Ready";
    public List<DiskItem> Children { get; } = [];
}
