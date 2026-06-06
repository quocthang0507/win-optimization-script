namespace WinOptimizationApp.Models;

public sealed record CloudStorageLocation(
    CloudStorageProvider Provider,
    string DisplayName,
    string Path,
    bool IsDetected,
    string Detail);
