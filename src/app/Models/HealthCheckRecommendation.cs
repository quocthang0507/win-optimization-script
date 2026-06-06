namespace WinOptimizationApp.Models;

public sealed record HealthCheckRecommendation(
    string Id,
    RiskLevel Risk,
    string Title,
    string Detail,
    string ActionLabel,
    string ActionTag);
