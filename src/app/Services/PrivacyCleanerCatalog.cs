using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class PrivacyCleanerCatalog
{
    public static IReadOnlyList<PrivacyCleanupItem> BuildDefault()
    {
        return
        [
            new PrivacyCleanupItem(
                "privacy.clipboard",
                "Clipboard",
                "Windows clipboard",
                RiskLevel.Safe,
                IsSensitive: false,
                IsSelectedByDefault: true,
                CanCleanAutomatically: true,
                "Safe quick cleanup for current clipboard contents."),
            new PrivacyCleanupItem(
                "privacy.recentFiles",
                "Recent files",
                "Windows recent documents and shortcuts",
                RiskLevel.Safe,
                IsSensitive: false,
                IsSelectedByDefault: true,
                CanCleanAutomatically: true,
                "Clears Windows recent documents, jump lists and taskbar recent shortcuts."),
            new PrivacyCleanupItem(
                "privacy.powershell",
                "PowerShell history",
                "PSReadLine console history",
                RiskLevel.Medium,
                IsSensitive: true,
                IsSelectedByDefault: false,
                CanCleanAutomatically: true,
                "Command history can contain secrets; review before deleting."),
            new PrivacyCleanupItem(
                "privacy.browserHistory",
                "Browser history",
                "Edge, Chrome, Firefox, Brave and Opera profiles",
                RiskLevel.High,
                IsSensitive: true,
                IsSelectedByDefault: false,
                CanCleanAutomatically: true,
                "Opt-in cleanup for local browsing history; close browsers first."),
            new PrivacyCleanupItem(
                "privacy.browserCookies",
                "Browser cookies and sessions",
                "Browser profile databases",
                RiskLevel.High,
                IsSensitive: true,
                IsSelectedByDefault: false,
                CanCleanAutomatically: true,
                "Opt-in cleanup that signs websites out and removes local browser sessions.")
        ];
    }
}
