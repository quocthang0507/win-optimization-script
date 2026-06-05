namespace WinOptimizationApp.Models;

public sealed record CleanupTargetPreview(
    string Name,
    string Path,
    bool Exists,
    long Bytes,
    int FileCount,
    string Status);
