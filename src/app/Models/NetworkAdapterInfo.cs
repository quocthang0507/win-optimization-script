namespace WinOptimizationApp.Models;

public sealed class NetworkAdapterInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required string Speed { get; init; }
    public required string MacAddress { get; init; }
    public required string IpAddress { get; init; }
}
