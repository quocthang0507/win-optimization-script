namespace WinOptimizationApp.Models;

public enum LeftoverCleanupConfidence
{
    High,
    Medium
}

public sealed record LeftoverCleanupCandidate(
    string Path,
    LeftoverCleanupConfidence Confidence,
    string Reason);