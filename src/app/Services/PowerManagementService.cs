using System.Text.RegularExpressions;
using System.Xml.Linq;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed partial class PowerManagementService
{
    public const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    private static readonly Regex PowerSchemeRegex = new(
        @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\(([^)]+)\)\s*(\*)?",
        RegexOptions.Compiled);

    private readonly CommandRunner _commands;

    public PowerManagementService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<BatteryHealthReport> GetBatteryHealthAsync(CancellationToken cancellationToken = default)
    {
        var tempXmlPath = Path.Combine(Path.GetTempPath(), $"winopt-battery-{Guid.NewGuid():N}.xml");
        try
        {
            var result = await _commands.RunCaptureAsync(
                "powercfg.exe",
                $"/batteryreport /xml /output \"{tempXmlPath}\"",
                cancellationToken);

            if (File.Exists(tempXmlPath))
            {
                var xml = await File.ReadAllTextAsync(tempXmlPath, cancellationToken);
                return ParseBatteryReportXml(xml);
            }

            return new BatteryHealthReport(
                false,
                string.Empty,
                string.Empty,
                0,
                0,
                null,
                0,
                0,
                string.IsNullOrWhiteSpace(result.StandardError) ? "No battery detected." : result.StandardError.Trim());
        }
        finally
        {
            try
            {
                if (File.Exists(tempXmlPath))
                {
                    File.Delete(tempXmlPath);
                }
            }
            catch
            {
                // Ignore temp file cleanup errors.
            }
        }
    }

    public static BatteryHealthReport ParseBatteryReportXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new BatteryHealthReport(false, string.Empty, string.Empty, 0, 0, null, 0, 0, "No battery report data.");
        }

        try
        {
            var doc = XDocument.Parse(xml);
            var batteryElement = doc
                .Descendants()
                .FirstOrDefault(el => el.Name.LocalName.Equals("Battery", StringComparison.OrdinalIgnoreCase) &&
                                      el.Elements().Any(child => child.Name.LocalName.Equals("DesignCapacity", StringComparison.OrdinalIgnoreCase)));

            if (batteryElement is null)
            {
                return new BatteryHealthReport(false, string.Empty, string.Empty, 0, 0, null, 0, 0, "No system battery installed.");
            }

            string ReadElementText(string localName) =>
                batteryElement.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? string.Empty;

            var id = ReadElementText("Id");
            var manufacturer = ReadElementText("Manufacturer");
            var chemistry = ReadElementText("Chemistry");
            var designStr = ReadElementText("DesignCapacity");
            var fullStr = ReadElementText("FullChargeCapacity");
            var cycleStr = ReadElementText("CycleCount");

            _ = long.TryParse(designStr, out var designCapacity);
            _ = long.TryParse(fullStr, out var fullCapacity);
            int? cycleCount = int.TryParse(cycleStr, out var parsedCycles) && parsedCycles >= 0 ? parsedCycles : null;

            var name = string.Join(" ", new[] { manufacturer, id }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "System Battery";
            }

            var healthPercent = designCapacity > 0 && fullCapacity > 0
                ? Math.Round(Math.Min(100.0, fullCapacity * 100.0 / designCapacity), 1)
                : 0.0;
            var wearPercent = designCapacity > 0 && fullCapacity > 0
                ? Math.Round(Math.Max(0.0, 100.0 - (fullCapacity * 100.0 / designCapacity)), 1)
                : 0.0;

            return new BatteryHealthReport(
                true,
                name,
                chemistry,
                designCapacity,
                fullCapacity,
                cycleCount,
                healthPercent,
                wearPercent,
                $"{healthPercent:0.0}% health ({fullCapacity:N0} / {designCapacity:N0} mWh)");
        }
        catch (Exception ex)
        {
            return new BatteryHealthReport(false, string.Empty, string.Empty, 0, 0, null, 0, 0, $"Failed to parse battery report: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunCaptureAsync(
            "powercfg.exe",
            "/list",
            cancellationToken);

        return ParsePowerPlansOutput(result.StandardOutput);
    }

    public static IReadOnlyList<PowerPlanInfo> ParsePowerPlansOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var plans = new List<PowerPlanInfo>();
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var match = PowerSchemeRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var schemeGuid = match.Groups[1].Value.ToLowerInvariant();
            var name = match.Groups[2].Value.Trim();
            var isActive = match.Groups[3].Success && match.Groups[3].Value == "*";
            plans.Add(new PowerPlanInfo(schemeGuid, name, isActive));
        }

        return plans;
    }

    public async Task<CommandResult> SetActivePowerPlanAsync(string schemeGuid, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(schemeGuid, out var parsedGuid))
        {
            throw new ArgumentException("Invalid power plan GUID.", nameof(schemeGuid));
        }

        return await _commands.RunCaptureAsync(
            "powercfg.exe",
            $"/setactive {parsedGuid:D}",
            cancellationToken);
    }

    public async Task<CommandResult> UnlockUltimatePerformancePlanAsync(CancellationToken cancellationToken = default)
    {
        return await _commands.RunCaptureAsync(
            "powercfg.exe",
            $"-duplicatescheme {UltimatePerformanceTemplateGuid}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<PowerRequestEntry>> GetPowerRequestsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunCaptureAsync(
            "powercfg.exe",
            "/requests",
            cancellationToken);

        return ParsePowerRequestsOutput(result.StandardOutput);
    }

    public static IReadOnlyList<PowerRequestEntry> ParsePowerRequestsOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var entries = new List<PowerRequestEntry>();
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var currentCategory = string.Empty;
        string? pendingRequester = null;

        foreach (var line in lines)
        {
            if (line.EndsWith(':') && !line.Contains('\\') && line.Length <= 32)
            {
                if (pendingRequester is not null && !string.IsNullOrWhiteSpace(currentCategory))
                {
                    entries.Add(new PowerRequestEntry(currentCategory, pendingRequester, string.Empty));
                    pendingRequester = null;
                }

                currentCategory = line.TrimEnd(':').Trim();
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentCategory))
            {
                continue;
            }

            if (line.Equals("None.", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("Không có.", StringComparison.OrdinalIgnoreCase))
            {
                pendingRequester = null;
                continue;
            }

            if (pendingRequester is null)
            {
                pendingRequester = line;
            }
            else
            {
                entries.Add(new PowerRequestEntry(currentCategory, pendingRequester, line));
                pendingRequester = null;
            }
        }

        if (pendingRequester is not null && !string.IsNullOrWhiteSpace(currentCategory))
        {
            entries.Add(new PowerRequestEntry(currentCategory, pendingRequester, string.Empty));
        }

        return entries;
    }
}
