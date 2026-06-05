namespace WinOptimizationApp.Models;

public sealed record MaintenanceTask(
    string Id,
    string Group,
    string Label,
    string Description,
    RiskLevel RiskLevel,
    bool RequiresAdmin,
    bool RequiresConfirmation,
    bool CanPreview,
    bool CanRollback,
    string EstimatedImpact);
