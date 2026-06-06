namespace WinOptimizationApp.Models;

public sealed record StartupImpactAnalysis(
    StartupImpactLevel Impact,
    string Recommendation,
    bool CanDisable,
    bool CanRollback);
