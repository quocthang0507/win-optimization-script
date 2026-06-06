using System.Text;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class DashboardExportService
{
    public static string FormatMarkdown(DashboardStatus status, DateTimeOffset exportedAt, AppLanguage language = AppLanguage.English)
    {
        var localization = new LocalizationService(language);
        var ramUsed = status.TotalRamBytes > status.AvailableRamBytes ? status.TotalRamBytes - status.AvailableRamBytes : 0;
        var pageFileUsed = status.TotalPageFileBytes > status.AvailablePageFileBytes ? status.TotalPageFileBytes - status.AvailablePageFileBytes : 0;
        var builder = new StringBuilder();

        builder.AppendLine($"# {Text(localization, "report.dashboardTitle", "Windows System Maintenance Dashboard")}");
        builder.AppendLine();
        builder.AppendLine($"{Text(localization, "report.exported", "Exported")}: {exportedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        var health = DashboardHealthCheckService.Analyze(status);
        builder.AppendLine($"## {localization.Get("dashboard.healthCheck")}");
        AppendRow(builder, Text(localization, "report.score", "Score"), $"{health.Score:N0}/100");
        AppendRow(builder, Text(localization, "report.status", "Status"), LocalizedHealthStatus(localization, health.Status));
        if (health.Findings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"### {Text(localization, "report.findings", "Findings")}");
            foreach (var finding in health.Findings)
            {
                builder.AppendLine($"- **{EscapeMarkdown(finding.Title)}** ({finding.Severity}, {EscapeMarkdown(finding.Source)}): {EscapeMarkdown(finding.Detail)}");
            }
        }
        if (health.Recommendations.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"### {localization.Get("dashboard.healthRecommendations")}");
            foreach (var recommendation in health.Recommendations)
            {
                builder.AppendLine($"- **{EscapeMarkdown(LocalizedHealthRecommendationTitle(localization, recommendation))}** ({localization.RiskName(recommendation.Risk)}): {EscapeMarkdown(LocalizedHealthRecommendationDetail(localization, recommendation))}");
            }
        }
        builder.AppendLine();

        builder.AppendLine($"## {Text(localization, "report.system", "System")}");
        AppendRow(builder, localization.Get("dashboard.machine"), status.MachineName);
        AppendRow(builder, localization.Get("dashboard.user"), status.UserName);
        AppendRow(builder, "Windows", status.WindowsVersion);
        AppendRow(builder, localization.Get("dashboard.administrator"), YesNo(localization, status.IsAdministrator));
        AppendRow(builder, localization.Get("dashboard.pendingReboot"), YesNo(localization, status.PendingReboot));
        AppendRow(builder, localization.Get("dashboard.uptime"), Formatters.FormatDuration(status.Uptime, language));
        AppendRow(builder, "WinGet", status.WingetAvailable ? Text(localization, "report.available", "Available") : localization.Get("dashboard.wingetNotFound"));
        AppendRow(builder, localization.Get("dashboard.lastReport"), string.IsNullOrWhiteSpace(status.LastReportPath) ? localization.Get("common.none") : status.LastReportPath);
        builder.AppendLine();

        builder.AppendLine($"## {Text(localization, "report.hardwareRuntime", "Hardware and Runtime")}");
        AppendRow(builder, localization.Get("dashboard.cpu"), status.CpuName);
        AppendRow(builder, localization.Get("dashboard.processors"), status.ProcessorCount.ToString("N0"));
        AppendRow(builder, localization.Get("dashboard.runtime"), status.DotNetRuntime);
        AppendRow(builder, Text(localization, "report.processArchitecture", "Process architecture"), status.ProcessArchitecture);
        AppendRow(builder, Text(localization, "report.osArchitecture", "OS architecture"), status.OSArchitecture);
        AppendRow(builder, localization.Get("dashboard.memoryLoad"), $"{status.MemoryLoadPercent:N0}%");
        AppendRow(builder, Text(localization, "report.ramUsed", "RAM used"), $"{Formatters.FormatBytes((long)ramUsed)} / {Formatters.FormatBytes((long)status.TotalRamBytes)}");
        AppendRow(builder, Text(localization, "report.pageFileUsed", "Page file used"), $"{Formatters.FormatBytes((long)pageFileUsed)} / {Formatters.FormatBytes((long)status.TotalPageFileBytes)}");
        builder.AppendLine();

        builder.AppendLine($"## {localization.Get("dashboard.drives")}");
        builder.AppendLine($"| {Text(localization, "report.drive", "Drive")} | {Text(localization, "report.type", "Type")} | Format | Label | {Text(localization, "report.used", "Used")} | {localization.Get("dashboard.free")} | {Text(localization, "report.total", "Total")} | {Text(localization, "report.usedPercent", "Used %")} |");
        builder.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: |");
        foreach (var drive in status.Drives)
        {
            var used = drive.TotalBytes - drive.FreeBytes;
            var percent = drive.TotalBytes > 0 ? used * 100d / drive.TotalBytes : 0;
            builder.AppendLine($"| {Escape(drive.Name)} | {Escape(drive.DriveType)} | {Escape(drive.Format)} | {Escape(drive.Label)} | {Formatters.FormatBytes(used)} | {Formatters.FormatBytes(drive.FreeBytes)} | {Formatters.FormatBytes(drive.TotalBytes)} | {percent:N1}% |");
        }

        return builder.ToString();
    }

    public static async Task<string> SaveMarkdownAsync(DashboardStatus status, string logsDirectory, AppLanguage language = AppLanguage.English, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(logsDirectory);
        var now = DateTimeOffset.Now;
        var path = Path.Combine(logsDirectory, $"dashboard-{now:yyyyMMdd-HHmmss}.md");
        await File.WriteAllTextAsync(path, FormatMarkdown(status, now, language), cancellationToken);
        return path;
    }

    private static void AppendRow(StringBuilder builder, string label, string value)
    {
        builder.AppendLine($"- **{label}:** {value}");
    }

    private static string Escape(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string EscapeMarkdown(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string LocalizedHealthRecommendationTitle(LocalizationService localization, HealthCheckRecommendation recommendation)
    {
        return Text(localization, $"health.recommendation.{recommendation.Id}.title", recommendation.Title);
    }

    private static string LocalizedHealthRecommendationDetail(LocalizationService localization, HealthCheckRecommendation recommendation)
    {
        return Text(localization, $"health.recommendation.{recommendation.Id}.detail", recommendation.Detail);
    }

    private static string LocalizedHealthStatus(LocalizationService localization, string status)
    {
        return status switch
        {
            "Good" => localization.Get("dashboard.healthGood"),
            "Attention" => localization.Get("dashboard.healthAttention"),
            "Critical" => localization.Get("dashboard.healthCritical"),
            _ => status
        };
    }

    private static string YesNo(LocalizationService localization, bool value)
    {
        return value ? Text(localization, "common.yes", "Yes") : Text(localization, "common.no", "No");
    }

    private static string Text(LocalizationService localization, string key, string fallback)
    {
        var value = localization.Get(key);
        return value == key ? fallback : value;
    }
}
