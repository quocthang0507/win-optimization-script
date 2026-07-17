namespace WinOptimizationApp.Models;

public sealed record Winapp2CleanupCandidate(
    string Entry,
    string Path,
    long Bytes);

public sealed record Winapp2CleanupPreview(
    IReadOnlyList<Winapp2CleanupCandidate> Candidates,
    IReadOnlyList<string> Warnings)
{
    public long TotalBytes => Candidates.Sum(candidate => candidate.Bytes);
}
