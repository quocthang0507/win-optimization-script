namespace WinOptimizationApp.Models;

public sealed record HealthCheckResult(
    int Score,
    string Status,
    IReadOnlyList<HealthCheckFinding> Findings,
    IReadOnlyList<HealthCheckRecommendation> Recommendations);
