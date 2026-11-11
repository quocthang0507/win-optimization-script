using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class SetupConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TweakService _tweakService;
    private readonly MaintenanceCatalog _catalog;

    public SetupConfigurationService(TweakService tweakService, MaintenanceCatalog catalog)
    {
        _tweakService = tweakService;
        _catalog = catalog;
    }

    public MachineSetupBundle CreateBundleFromProfile(
        string profileName,
        IReadOnlyDictionary<string, bool> tweakValues,
        IEnumerable<string>? wingetPackageIds = null,
        IEnumerable<string>? cleanupTaskIds = null,
        MaintenanceScheduleFrequency schedule = MaintenanceScheduleFrequency.Off)
    {
        return new MachineSetupBundle
        {
            SchemaVersion = 1,
            Name = string.IsNullOrWhiteSpace(profileName) ? "Custom Windows Setup" : profileName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            TweakValues = new Dictionary<string, bool>(tweakValues, StringComparer.OrdinalIgnoreCase),
            WingetPackageIds = (wingetPackageIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CleanupTaskIds = (cleanupTaskIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ScheduleFrequency = schedule
        };
    }

    public SetupBundleValidationResult ValidateBundle(MachineSetupBundle? bundle)
    {
        if (bundle is null)
        {
            return new SetupBundleValidationResult(false, [], ["Bundle is null."]);
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        if (bundle.SchemaVersion <= 0)
        {
            errors.Add("SchemaVersion must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(bundle.Name))
        {
            errors.Add("Bundle Name is required.");
        }

        var knownTweaks = _tweakService.GetAllTweaks().ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var (tweakId, _) in bundle.TweakValues)
        {
            if (!knownTweaks.ContainsKey(tweakId))
            {
                warnings.Add($"Unknown tweak ID '{tweakId}' will be ignored.");
            }
        }

        var knownTasks = _catalog.All.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var taskId in bundle.CleanupTaskIds)
        {
            if (!knownTasks.Contains(taskId))
            {
                warnings.Add($"Unknown cleanup task ID '{taskId}' will be ignored.");
            }
        }

        foreach (var packageId in bundle.WingetPackageIds)
        {
            if (!WingetService.IsValidPackageId(packageId))
            {
                errors.Add($"Invalid WinGet package ID '{packageId}'.");
            }
        }

        return new SetupBundleValidationResult(errors.Count == 0, warnings, errors);
    }

    public string ExportJson(MachineSetupBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return JsonSerializer.Serialize(bundle, JsonOptions);
    }

    public MachineSetupBundle ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be empty.", nameof(json));
        }

        var bundle = JsonSerializer.Deserialize<MachineSetupBundle>(json, JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize MachineSetupBundle.");

        bundle.TweakValues = new Dictionary<string, bool>(
            bundle.TweakValues ?? new Dictionary<string, bool>(),
            StringComparer.OrdinalIgnoreCase);
        bundle.WingetPackageIds = (bundle.WingetPackageIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        bundle.CleanupTaskIds = (bundle.CleanupTaskIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return bundle;
    }

    public string GeneratePowerShellScript(MachineSetupBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var sb = new StringBuilder();
        sb.AppendLine("#Requires -Version 5.1");
        sb.AppendLine($"# Generated by WinOptimizationApp Configuration Builder: {bundle.Name}");
        sb.AppendLine($"# Created At: {bundle.CreatedAt:O}");
        sb.AppendLine("Set-StrictMode -Version Latest");
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        sb.AppendLine();

        if (bundle.WingetPackageIds.Count > 0)
        {
            sb.AppendLine("Write-Host '=== Installing / Updating WinGet Packages ===' -ForegroundColor Cyan");
            foreach (var pkgId in bundle.WingetPackageIds.Where(WingetService.IsValidPackageId))
            {
                sb.AppendLine($"winget install --id '{pkgId}' --exact --accept-package-agreements --accept-source-agreements --silent");
            }
            sb.AppendLine();
        }

        if (bundle.TweakValues.Count > 0)
        {
            var catalog = _tweakService.GetAllTweaks().ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            sb.AppendLine("Write-Host '=== Applying System Tweaks ===' -ForegroundColor Cyan");
            foreach (var (tweakId, enable) in bundle.TweakValues)
            {
                if (!catalog.TryGetValue(tweakId, out var tweak))
                {
                    continue;
                }

                sb.AppendLine($"# Tweak: {tweak.Title} ({tweak.Id}) -> {(enable ? "Enable" : "Disable")}");
                var script = enable ? tweak.EnableScript : tweak.DisableScript;
                sb.AppendLine(script.Trim());
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
