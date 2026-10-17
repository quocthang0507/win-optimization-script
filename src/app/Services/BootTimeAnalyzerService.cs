using System.Globalization;
using System.Xml.Linq;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class BootTimeAnalyzerService
{
    private readonly CommandRunner _commands;

    public BootTimeAnalyzerService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<BootTimeSummary> GetBootTimeSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _commands.RunCaptureAsync(
                "wevtutil",
                "qe \"Microsoft-Windows-Diagnostics-Performance/Operational\" /q:\"*[System[(EventID=100)]]\" /c:5 /rd:true /f:xml",
                cancellationToken);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return EmptySummary();
            }

            return ParseBootEventsXml(result.StandardOutput);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EmptySummary();
        }
    }

    public static BootTimeSummary ParseBootEventsXml(string? rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            return EmptySummary();
        }

        try
        {
            // wevtutil outputs a sequence of <Event>...</Event> fragments without a single root node
            var wrappedXml = $"<Events>{rawXml}</Events>";
            var doc = XDocument.Parse(wrappedXml);
            var records = new List<BootEventRecord>();

            foreach (var eventElement in doc.Root?.Elements() ?? [])
            {
                if (!eventElement.Name.LocalName.Equals("Event", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var systemElement = eventElement.Elements().FirstOrDefault(e => e.Name.LocalName == "System");
                var timeCreatedElement = systemElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "TimeCreated");
                var systemTimeAttr = timeCreatedElement?.Attribute("SystemTime")?.Value;

                var timestamp = DateTimeOffset.TryParse(systemTimeAttr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTime)
                    ? parsedTime
                    : DateTimeOffset.UtcNow;

                var eventDataElement = eventElement.Elements().FirstOrDefault(e => e.Name.LocalName == "EventData");
                if (eventDataElement is null)
                {
                    continue;
                }

                double bootTimeMs = 0;
                double mainPathMs = 0;
                double postBootMs = 0;

                foreach (var dataElement in eventDataElement.Elements().Where(e => e.Name.LocalName == "Data"))
                {
                    var name = dataElement.Attribute("Name")?.Value;
                    var valueStr = dataElement.Value;
                    if (!double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                    {
                        continue;
                    }

                    if (string.Equals(name, "BootTime", StringComparison.OrdinalIgnoreCase))
                    {
                        bootTimeMs = val;
                    }
                    else if (string.Equals(name, "MainPathBootTime", StringComparison.OrdinalIgnoreCase))
                    {
                        mainPathMs = val;
                    }
                    else if (string.Equals(name, "BootPostBootTime", StringComparison.OrdinalIgnoreCase))
                    {
                        postBootMs = val;
                    }
                }

                if (bootTimeMs > 0)
                {
                    records.Add(new BootEventRecord(
                        Timestamp: timestamp,
                        TotalBootSeconds: Math.Round(bootTimeMs / 1000.0, 1),
                        MainPathSeconds: Math.Round(mainPathMs / 1000.0, 1),
                        PostBootSeconds: Math.Round(postBootMs / 1000.0, 1)));
                }
            }

            if (records.Count == 0)
            {
                return EmptySummary();
            }

            var ordered = records.OrderByDescending(r => r.Timestamp).ToList();
            var lastBoot = ordered[0];
            var avgSeconds = Math.Round(ordered.Average(r => r.TotalBootSeconds), 1);
            var rating = ClassifyBootRating(lastBoot.TotalBootSeconds);

            return new BootTimeSummary(
                IsAvailable: true,
                LastBoot: lastBoot,
                AverageBootSeconds: avgSeconds,
                Rating: rating,
                RecentBoots: ordered);
        }
        catch
        {
            return EmptySummary();
        }
    }

    public static BootSpeedRating ClassifyBootRating(double bootSeconds)
    {
        if (bootSeconds <= 0)
        {
            return BootSpeedRating.Unknown;
        }

        if (bootSeconds <= 22.0)
        {
            return BootSpeedRating.Fast;
        }

        if (bootSeconds <= 45.0)
        {
            return BootSpeedRating.Normal;
        }

        return BootSpeedRating.Slow;
    }

    private static BootTimeSummary EmptySummary() =>
        new(false, null, 0, BootSpeedRating.Unknown, []);
}
