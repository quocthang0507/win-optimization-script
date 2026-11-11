namespace WinOptimizationApp.Models;

public sealed record RegistryValueSnapshot(
    string Hive,
    string SubKey,
    string ValueName,
    bool Exists,
    string ValueKind,
    string? RawValue);

public sealed record TweakSnapshot(
    string Id,
    DateTimeOffset CreatedAt,
    string Label,
    IReadOnlyDictionary<string, bool> Values,
    IReadOnlyList<RegistryValueSnapshot>? RegistryEntries = null);
