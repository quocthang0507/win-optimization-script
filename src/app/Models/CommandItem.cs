namespace WinOptimizationApp.Models;

public enum CommandCategory
{
    Navigation,
    Maintenance,
    SystemTool,
    Settings,
    Action
}

public sealed class CommandItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required CommandCategory Category { get; init; }
    public required IReadOnlyList<string> Keywords { get; init; }
    public string? Glyph { get; init; }
    public required Func<Task> ExecuteAsync { get; init; }
}
