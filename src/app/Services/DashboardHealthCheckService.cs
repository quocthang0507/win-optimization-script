using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class DashboardHealthCheckService
{
    private const long FiveGb = 5L * 1024 * 1024 * 1024;
    private const long FifteenGb = 15L * 1024 * 1024 * 1024;

    public static HealthCheckResult Analyze(DashboardStatus status, HealthCheckScanMetrics? metrics = null)
    {
        var score = 100;
        var findings = new List<HealthCheckFinding>();
        var recommendations = new List<HealthCheckRecommendation>();

        AnalyzeSystemDrive(status, findings, recommendations, ref score);
        AnalyzeMemory(status, findings, recommendations, ref score);
        AnalyzePendingReboot(status, findings, recommendations, ref score);
        AnalyzeUptime(status, findings, recommendations, ref score);
        AnalyzeWinget(status, findings, recommendations, ref score);
        AnalyzeMaintenanceHistory(status, findings, recommendations, ref score);
        AnalyzeAdminState(status, findings);
        AnalyzeDeepScan(metrics, findings, recommendations, ref score);

        score = Math.Clamp(score, 0, 100);
        return new HealthCheckResult(score, GetStatus(score), findings, recommendations, metrics);
    }

    private static void AnalyzeDeepScan(
        HealthCheckScanMetrics? metrics,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (metrics is null)
        {
            return;
        }

        if (metrics.Errors.Count > 0)
        {
            findings.Add(new HealthCheckFinding(
                "scan.partial",
                RiskLevel.Safe,
                "Health scan completed partially",
                $"{metrics.Errors.Count:N0} data source(s) could not be checked.",
                "Diagnostics"));
            score -= 2;
        }

        if (metrics.CleanupBytes >= 500L * 1024 * 1024)
        {
            findings.Add(new HealthCheckFinding(
                "cleanup.available",
                RiskLevel.Safe,
                "Safe cleanup is available",
                $"{Formatters.FormatBytes(metrics.CleanupBytes)} across {metrics.CleanupFiles:N0} files can be reviewed.",
                "Space"));
            recommendations.Add(new HealthCheckRecommendation(
                "cleanup.review",
                RiskLevel.Safe,
                "Review safe cleanup",
                "Open Cleanup to inspect every target before deleting files.",
                "Review Cleanup",
                "cleanup"));
            score -= 4;
        }

        if (metrics.AvailableUpdates > 0)
        {
            findings.Add(new HealthCheckFinding(
                "updates.available",
                RiskLevel.Medium,
                "Application updates are available",
                $"WinGet found {metrics.AvailableUpdates:N0} package updates.",
                "Security"));
            recommendations.Add(new HealthCheckRecommendation(
                "updates.review",
                RiskLevel.Medium,
                "Review application updates",
                "Review package versions and sources before upgrading.",
                "Review Updates",
                "updates"));
            score -= Math.Min(12, 3 + metrics.AvailableUpdates);
        }

        if (metrics.HighImpactStartupItems > 0)
        {
            findings.Add(new HealthCheckFinding(
                "startup.highImpact",
                RiskLevel.Medium,
                "High-impact startup items need review",
                $"{metrics.HighImpactStartupItems:N0} enabled startup item(s) were classified as high impact.",
                "Speed"));
            recommendations.Add(new HealthCheckRecommendation(
                "startup.highImpact",
                RiskLevel.Medium,
                "Review high-impact startup items",
                "Verify the publisher and purpose before disabling anything.",
                "Open Startup",
                "startup"));
            score -= Math.Min(10, metrics.HighImpactStartupItems * 3);
        }
    }

    private static void AnalyzeSystemDrive(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (status.SystemDriveTotalBytes <= 0)
        {
            findings.Add(new HealthCheckFinding(
                "disk.unknown",
                RiskLevel.Medium,
                "System drive size is unavailable",
                "The app could not read system drive capacity.",
                "Storage"));
            score -= 8;
            return;
        }

        var freePercent = status.SystemDriveFreeBytes * 100d / status.SystemDriveTotalBytes;
        if (status.SystemDriveFreeBytes < FiveGb || freePercent < 10)
        {
            findings.Add(new HealthCheckFinding(
                "disk.critical",
                RiskLevel.High,
                "System drive is almost full",
                $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} free on {status.SystemDrive}.",
                "Storage"));
            recommendations.Add(new HealthCheckRecommendation(
                "storage.analyze",
                RiskLevel.Medium,
                "Review storage usage",
                "Open Storage Analyzer to find large files and cleanup candidates before deleting anything.",
                "Analyze Storage",
                "storage"));
            score -= 40;
        }
        else if (status.SystemDriveFreeBytes < FifteenGb || freePercent < 20)
        {
            findings.Add(new HealthCheckFinding(
                "disk.warning",
                RiskLevel.Medium,
                "System drive free space is getting low",
                $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} free on {status.SystemDrive}.",
                "Storage"));
            recommendations.Add(new HealthCheckRecommendation(
                "cleanup.scan",
                RiskLevel.Safe,
                "Scan cleanup targets",
                "Preview safe cleanup targets and review estimated savings.",
                "Scan Cleanup",
                "cleanup"));
            score -= 12;
        }
    }

    private static void AnalyzeMemory(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (status.MemoryLoadPercent >= 90)
        {
            findings.Add(new HealthCheckFinding(
                "memory.critical",
                RiskLevel.High,
                "Memory pressure is high",
                $"Current memory load is {status.MemoryLoadPercent:N0}%.",
                "Memory"));
            recommendations.Add(new HealthCheckRecommendation(
                "performance.review",
                RiskLevel.Safe,
                "Review background load",
                "Check startup entries and close unneeded background apps.",
                "Open Startup",
                "startup"));
            score -= 20;
        }
        else if (status.MemoryLoadPercent >= 75)
        {
            findings.Add(new HealthCheckFinding(
                "memory.warning",
                RiskLevel.Medium,
                "Memory usage is elevated",
                $"Current memory load is {status.MemoryLoadPercent:N0}%.",
                "Memory"));
            recommendations.Add(new HealthCheckRecommendation(
                "startup.review",
                RiskLevel.Safe,
                "Review startup apps",
                "Open Startup to inspect apps that may add background load.",
                "Open Startup",
                "startup"));
            score -= 10;
        }
    }

    private static void AnalyzePendingReboot(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (!status.PendingReboot)
        {
            return;
        }

        findings.Add(new HealthCheckFinding(
            "reboot.pending",
            RiskLevel.Medium,
            "Restart is pending",
            "Windows has pending operations that need a restart to finish.",
            "Windows"));
        recommendations.Add(new HealthCheckRecommendation(
            "reboot.restart",
            RiskLevel.Safe,
            "Restart when convenient",
            "Save your work and restart Windows to finish pending maintenance.",
            "Review Updates",
            "updates"));
        score -= 15;
    }

    private static void AnalyzeUptime(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (status.Uptime.TotalDays < 7)
        {
            return;
        }

        findings.Add(new HealthCheckFinding(
            "uptime.long",
            RiskLevel.Safe,
            "Long uptime",
            $"The machine has been running for {(int)status.Uptime.TotalDays:N0} days.",
            "Windows"));
        recommendations.Add(new HealthCheckRecommendation(
            "uptime.restart",
            RiskLevel.Safe,
            "Schedule a restart",
            "A periodic restart can clear pending handles and complete background updates.",
            "Review Updates",
            "updates"));
        score -= 6;
    }

    private static void AnalyzeWinget(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (status.WingetAvailable)
        {
            return;
        }

        findings.Add(new HealthCheckFinding(
            "winget.missing",
            RiskLevel.Medium,
            "WinGet is not available",
            "Package update scanning is limited because winget was not found.",
            "Updates"));
        recommendations.Add(new HealthCheckRecommendation(
            "winget.install",
            RiskLevel.Safe,
            "Review update setup",
            "Open Updates to verify package update support.",
            "Open Updates",
            "updates"));
        score -= 8;
    }

    private static void AnalyzeMaintenanceHistory(
        DashboardStatus status,
        List<HealthCheckFinding> findings,
        List<HealthCheckRecommendation> recommendations,
        ref int score)
    {
        if (!string.IsNullOrWhiteSpace(status.LastReportPath))
        {
            return;
        }

        findings.Add(new HealthCheckFinding(
            "maintenance.noReport",
            RiskLevel.Safe,
            "No maintenance report yet",
            "Run a scan or task to create the first maintenance report.",
            "History"));
        recommendations.Add(new HealthCheckRecommendation(
            "maintenance.scan",
            RiskLevel.Safe,
            "Run a cleanup scan",
            "Preview cleanup targets and generate a report without deleting anything.",
            "Scan Cleanup",
            "cleanup"));
        score -= 4;
    }

    private static void AnalyzeAdminState(DashboardStatus status, List<HealthCheckFinding> findings)
    {
        if (status.IsAdministrator)
        {
            return;
        }

        findings.Add(new HealthCheckFinding(
            "admin.standard",
            RiskLevel.Safe,
            "Running as standard user",
            "High-risk maintenance actions will require an elevated session.",
            "Permissions"));
    }

    private static string GetStatus(int score)
    {
        return score >= 85 ? "Good" : score >= 65 ? "Attention" : "Critical";
    }
}
