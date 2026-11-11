namespace WinOptimizationApp.Models;

public sealed record ContextMenuEntry(
    string Name,
    string Clsid,
    string Scope,
    string Hive,
    string HandlerKeyPath,
    string BinaryPath,
    bool IsEnabled);
