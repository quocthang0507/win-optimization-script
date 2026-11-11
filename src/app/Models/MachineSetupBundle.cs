namespace WinOptimizationApp.Models;

public sealed class MachineSetupBundle
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Custom Windows Setup";
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, bool> TweakValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> WingetPackageIds { get; set; } = [];
    public List<string> CleanupTaskIds { get; set; } = [];
    public MaintenanceScheduleFrequency ScheduleFrequency { get; set; } = MaintenanceScheduleFrequency.Off;
}

public sealed record SetupBundleValidationResult(
    bool IsValid,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
