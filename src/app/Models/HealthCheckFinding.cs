namespace WinOptimizationApp.Models;

public sealed record HealthCheckFinding(
    string Id,
    RiskLevel Severity,
    string Title,
    string Detail,
    string Source);
