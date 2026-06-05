namespace WinOptimizationApp.Models;

public sealed record StorageCleanupCandidate(
    string Id,
    string Label,
    string SourcePath,
    long EstimatedBytes,
    RiskLevel RiskLevel,
    StorageCleanupMode CleanupMode,
    string Reason,
    bool IsDirectory);
