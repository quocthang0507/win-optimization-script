namespace WinOptimizationApp.Models;

public sealed record StartupEntry(
    string Name,
    string Source,
    string Command,
    bool Enabled,
    string RiskHint);
