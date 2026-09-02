namespace WinOptimizationApp.Models;

/// <summary>Stable warning code for UI translation; fallback remains usable in reports/older clients.</summary>
public sealed record CleanupWarning(string Code, string Fallback, IReadOnlyList<string> Arguments);
