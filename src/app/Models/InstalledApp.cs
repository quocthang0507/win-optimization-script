namespace WinOptimizationApp.Models;

public sealed class InstalledApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Publisher { get; init; }
    public required string UninstallString { get; init; }
    public required string InstallLocation { get; init; }
    public required string Source { get; init; }
}
