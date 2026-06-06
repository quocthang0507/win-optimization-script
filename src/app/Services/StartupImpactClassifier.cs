using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class StartupImpactClassifier
{
    private static readonly string[] ScriptHosts =
    [
        "powershell.exe",
        "pwsh.exe",
        "cmd.exe",
        "wscript.exe",
        "cscript.exe",
        "mshta.exe",
        "rundll32.exe",
        "regsvr32.exe"
    ];

    private static readonly string[] UserProfileHints =
    [
        "AppData",
        "Temp",
        "\\Downloads\\"
    ];

    private static readonly string[] ProtectedHints =
    [
        "Windows Defender",
        "SecurityHealth",
        "Microsoft\\EdgeUpdate",
        "MicrosoftEdgeUpdate"
    ];

    private static readonly string[] HeavyAppHints =
    [
        "Teams",
        "Discord",
        "Steam",
        "Adobe",
        "Creative Cloud",
        "OneDrive",
        "Dropbox",
        "Google\\Drive",
        "Updater"
    ];

    public static StartupImpactAnalysis Analyze(string name, string source, string command, bool enabled)
    {
        return !enabled
            ? new StartupImpactAnalysis(
                StartupImpactLevel.Low,
                "Already disabled",
                CanDisable: false,
                CanRollback: true)
            : ContainsAny(command, ProtectedHints) || ContainsAny(name, ProtectedHints)
            ? new StartupImpactAnalysis(
                StartupImpactLevel.Low,
                "Keep enabled unless you know this Windows component is unnecessary",
                CanDisable: false,
                CanRollback: false)
            : ContainsAny(command, ScriptHosts) && ContainsAny(command, UserProfileHints)
            ? new StartupImpactAnalysis(
                StartupImpactLevel.High,
                "Review carefully; script-based startup from a user-writable path can slow boot or be unsafe",
                CanDisable: true,
                CanRollback: true)
            : ContainsAny(command, HeavyAppHints) || ContainsAny(name, HeavyAppHints)
            ? new StartupImpactAnalysis(
                StartupImpactLevel.Medium,
                "Consider delaying or disabling if you do not need it immediately after sign-in",
                CanDisable: true,
                CanRollback: true)
            : source.Contains("HKLM", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("Common Startup", StringComparison.OrdinalIgnoreCase)
            ? new StartupImpactAnalysis(
                StartupImpactLevel.Medium,
                "Review publisher and purpose before changing machine-wide startup",
                CanDisable: true,
                CanRollback: true)
            : ContainsAny(command, UserProfileHints)
            ? new StartupImpactAnalysis(
                StartupImpactLevel.Medium,
                "Review user-profile startup entries before disabling",
                CanDisable: true,
                CanRollback: true)
            : new StartupImpactAnalysis(
            StartupImpactLevel.Low,
            "Low startup impact; keep enabled unless it is unwanted",
            CanDisable: true,
            CanRollback: true);
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
