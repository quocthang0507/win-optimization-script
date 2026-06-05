namespace WinOptimizationApp.Models;

public sealed record FileTypeSummary(
    string Extension,
    long TotalBytes,
    int Count,
    string LargestItemPath,
    DateTimeOffset LastModified);
