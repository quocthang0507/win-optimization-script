namespace WinOptimizationApp.Models;

public enum BrowserCookieDbKind
{
    Chromium,
    Firefox
}

public sealed record BrowserCookieTarget(
    string BrowserName,
    string ProfileName,
    string ProcessName,
    string DatabasePath,
    BrowserCookieDbKind Kind);

public sealed record CookieDomainSummary(
    string Domain,
    int CookieCount,
    bool IsKept);

public sealed record BrowserCookieCleanupResult(
    string BrowserName,
    string DatabasePath,
    bool SkippedBecauseRunning,
    int DomainsPreserved,
    int CookiesDeleted,
    int CookiesRemaining,
    string? ErrorMessage = null);
