namespace WinOptimizationApp.Models;

public sealed record BatteryHealthReport(
    bool HasBattery,
    string BatteryName,
    string Chemistry,
    long DesignCapacityMwh,
    long FullChargeCapacityMwh,
    int? CycleCount,
    double HealthPercent,
    double WearPercent,
    string StatusSummary);

public sealed record PowerPlanInfo(
    string SchemeGuid,
    string Name,
    bool IsActive);

public sealed record PowerRequestEntry(
    string Category,
    string Requester,
    string Reason);
