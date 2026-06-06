namespace WinOptimizationApp.Models;

public sealed class RegistryIssue
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string KeyPath { get; init; }
    public required string ValueName { get; init; }
    public required string ValueData { get; init; }
    public required string Description { get; init; }
    public bool IsSelected { get; set; } = true;
}
