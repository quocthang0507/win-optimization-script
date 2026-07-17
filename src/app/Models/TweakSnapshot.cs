namespace WinOptimizationApp.Models;

public sealed record TweakSnapshot(
    string Id,
    DateTimeOffset CreatedAt,
    string Label,
    IReadOnlyDictionary<string, bool> Values);
