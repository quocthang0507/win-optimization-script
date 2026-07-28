namespace WinOptimizationApp.Services;

public sealed record TweakProfile(
    string Id,
    string NameKey,
    string DescriptionKey,
    IReadOnlyDictionary<string, bool> Values);

public static class TweakProfileCatalog
{
    public static IReadOnlyList<TweakProfile> All { get; } =
    [
        new(
            "balanced",
            "optimize.profileBalanced",
            "optimize.profileBalancedDescription",
            new Dictionary<string, bool>
            {
                ["privacy.telemetry"] = true,
                ["privacy.activityHistory"] = true,
                ["privacy.appDiagnostics"] = true,
                ["privacy.windowsSuggestions"] = true,
                ["privacy.webSearch"] = true,
                ["ui.hideSearch"] = true,
                ["ui.hideTaskView"] = true,
                ["ui.showFileExtensions"] = true,
                ["ui.endTaskTaskbar"] = true
            }),
        new(
            "gaming",
            "optimize.profileGaming",
            "optimize.profileGamingDescription",
            new Dictionary<string, bool>
            {
                ["gaming.gameMode"] = true,
                ["gaming.gameBar"] = true
            }),
        new(
            "windows-defaults",
            "optimize.profileDefaults",
            "optimize.profileDefaultsDescription",
            TweakService.Tweaks.ToDictionary(tweak => tweak.Id, _ => false, StringComparer.OrdinalIgnoreCase))
    ];
}
