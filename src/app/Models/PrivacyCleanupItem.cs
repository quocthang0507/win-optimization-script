namespace WinOptimizationApp.Models;

public sealed record PrivacyCleanupItem(
    string Id,
    string Label,
    string Source,
    RiskLevel RiskLevel,
    bool IsSensitive,
    bool IsSelectedByDefault,
    bool CanCleanAutomatically,
    string Recommendation);
