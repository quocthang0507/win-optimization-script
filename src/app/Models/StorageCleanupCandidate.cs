namespace WinOptimizationApp.Models;

public sealed record StorageCleanupCandidate(
    string Id,
    string Label,
    string SourcePath,
    long EstimatedBytes,
    RiskLevel RiskLevel,
    StorageCleanupMode CleanupMode,
    string Reason,
    bool IsDirectory)
{
    public static RiskLevel EvaluateManualCandidateRisk(string fullPath, bool isDirectory, string? userProfile = null)
    {
        userProfile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var isUserFile = !string.IsNullOrWhiteSpace(userProfile) && fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
        return isDirectory || !isUserFile ? RiskLevel.High : RiskLevel.Medium;
    }
}
