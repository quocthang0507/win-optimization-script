using System.Diagnostics;
using System.Runtime.InteropServices;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class BrowserCookieService
{
    private readonly Func<string, bool> _isProcessRunning;

    public BrowserCookieService(Func<string, bool>? isProcessRunning = null)
    {
        _isProcessRunning = isProcessRunning ?? DefaultIsProcessRunning;
    }

    public IReadOnlyList<BrowserCookieTarget> DiscoverTargets()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var targets = new List<BrowserCookieTarget>();

        var chromiumBrowsers = new (string Name, string ProcessName, string UserDataPath)[]
        {
            ("Google Chrome", "chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data")),
            ("Microsoft Edge", "msedge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data")),
            ("Brave", "brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")),
            ("Vivaldi", "vivaldi", Path.Combine(localAppData, "Vivaldi", "User Data")),
            ("Opera", "opera", Path.Combine(roamingAppData, "Opera Software", "Opera Stable"))
        };

        foreach (var (browserName, processName, userDataPath) in chromiumBrowsers)
        {
            if (!Directory.Exists(userDataPath))
            {
                continue;
            }

            foreach (var profileDir in GetChromiumProfileDirectories(userDataPath))
            {
                var networkCookies = Path.Combine(profileDir, "Network", "Cookies");
                var legacyCookies = Path.Combine(profileDir, "Cookies");
                var dbPath = File.Exists(networkCookies) ? networkCookies : File.Exists(legacyCookies) ? legacyCookies : null;
                if (dbPath is not null)
                {
                    targets.Add(new BrowserCookieTarget(
                        browserName,
                        Path.GetFileName(profileDir),
                        processName,
                        dbPath,
                        BrowserCookieDbKind.Chromium));
                }
            }
        }

        var firefoxProfilesRoot = Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfilesRoot))
        {
            foreach (var profileDir in Directory.GetDirectories(firefoxProfilesRoot))
            {
                var cookiesSqlite = Path.Combine(profileDir, "cookies.sqlite");
                if (File.Exists(cookiesSqlite))
                {
                    targets.Add(new BrowserCookieTarget(
                        "Mozilla Firefox",
                        Path.GetFileName(profileDir),
                        "firefox",
                        cookiesSqlite,
                        BrowserCookieDbKind.Firefox));
                }
            }
        }

        return targets;
    }

    public static bool MatchesKeepDomain(string cookieHost, IReadOnlyCollection<string> normalizedKeepDomains)
    {
        if (string.IsNullOrWhiteSpace(cookieHost) || normalizedKeepDomains.Count == 0)
        {
            return false;
        }

        var cleanHost = cookieHost.Trim().TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanHost))
        {
            return false;
        }

        foreach (var keepDomain in normalizedKeepDomains)
        {
            var domain = keepDomain.Trim().TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(domain))
            {
                continue;
            }

            if (cleanHost.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                cleanHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<CookieDomainSummary> ReadCookieDomains(
        string databasePath,
        BrowserCookieDbKind kind,
        IEnumerable<string>? keepDomains = null)
    {
        if (!File.Exists(databasePath))
        {
            return [];
        }

        var normalizedKeep = AppSettingsService.NormalizeCookieKeepDomains(keepDomains);
        var (tableName, hostColumn) = GetSchemaNames(kind);
        var sql = $"SELECT {hostColumn}, COUNT(*) FROM {tableName} GROUP BY {hostColumn} ORDER BY COUNT(*) DESC;";

        var rc = WinSqliteInterop.sqlite3_open_v2(
            databasePath,
            out var db,
            WinSqliteInterop.SQLITE_OPEN_READONLY,
            IntPtr.Zero);
        if (rc != WinSqliteInterop.SQLITE_OK)
        {
            if (db != IntPtr.Zero)
            {
                _ = WinSqliteInterop.sqlite3_close_v2(db);
            }

            throw new InvalidOperationException($"Cannot open cookie database '{databasePath}' (code {rc}).");
        }

        try
        {
            var prepRc = WinSqliteInterop.sqlite3_prepare_v2(db, sql, -1, out var stmt, IntPtr.Zero);
            if (prepRc != WinSqliteInterop.SQLITE_OK)
            {
                throw new InvalidOperationException($"Failed to query cookies: {WinSqliteInterop.GetErrorMessage(db)}");
            }

            var results = new List<CookieDomainSummary>();
            try
            {
                while (WinSqliteInterop.sqlite3_step(stmt) == WinSqliteInterop.SQLITE_ROW)
                {
                    var hostPtr = WinSqliteInterop.sqlite3_column_text(stmt, 0);
                    var host = hostPtr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(hostPtr) ?? string.Empty;
                    var count = WinSqliteInterop.sqlite3_column_int(stmt, 1);
                    var isKept = MatchesKeepDomain(host, normalizedKeep);
                    results.Add(new CookieDomainSummary(host, count, isKept));
                }
            }
            finally
            {
                _ = WinSqliteInterop.sqlite3_finalize(stmt);
            }

            return results;
        }
        finally
        {
            _ = WinSqliteInterop.sqlite3_close_v2(db);
        }
    }

    public BrowserCookieCleanupResult CleanCookieDatabase(
        BrowserCookieTarget target,
        IEnumerable<string>? keepDomains)
    {
        if (!string.IsNullOrWhiteSpace(target.ProcessName) && _isProcessRunning(target.ProcessName))
        {
            return new BrowserCookieCleanupResult(
                target.BrowserName,
                target.DatabasePath,
                SkippedBecauseRunning: true,
                DomainsPreserved: 0,
                CookiesDeleted: 0,
                CookiesRemaining: 0,
                ErrorMessage: $"{target.BrowserName} is currently running. Close the browser before cleaning cookies.");
        }

        if (!File.Exists(target.DatabasePath))
        {
            return new BrowserCookieCleanupResult(
                target.BrowserName,
                target.DatabasePath,
                SkippedBecauseRunning: false,
                DomainsPreserved: 0,
                CookiesDeleted: 0,
                CookiesRemaining: 0,
                ErrorMessage: "Cookie database file does not exist.");
        }

        var normalizedKeep = AppSettingsService.NormalizeCookieKeepDomains(keepDomains);
        var backupPath = target.DatabasePath + ".winopt.bak";
        try
        {
            File.Copy(target.DatabasePath, backupPath, overwrite: true);

            var domains = ReadCookieDomains(target.DatabasePath, target.Kind, normalizedKeep);
            var (tableName, hostColumn) = GetSchemaNames(target.Kind);

            var rc = WinSqliteInterop.sqlite3_open_v2(
                target.DatabasePath,
                out var db,
                WinSqliteInterop.SQLITE_OPEN_READWRITE,
                IntPtr.Zero);
            if (rc != WinSqliteInterop.SQLITE_OK)
            {
                if (db != IntPtr.Zero)
                {
                    _ = WinSqliteInterop.sqlite3_close_v2(db);
                }

                throw new InvalidOperationException($"Unable to open cookie DB for write (code {rc}).");
            }

            var deletedCount = 0;
            try
            {
                WinSqliteInterop.ExecuteNonQuery(db, "BEGIN IMMEDIATE;");
                var deleteSql = $"DELETE FROM {tableName} WHERE {hostColumn} = ?;";
                var prepRc = WinSqliteInterop.sqlite3_prepare_v2(db, deleteSql, -1, out var deleteStmt, IntPtr.Zero);
                if (prepRc != WinSqliteInterop.SQLITE_OK)
                {
                    throw new InvalidOperationException(WinSqliteInterop.GetErrorMessage(db));
                }

                try
                {
                    foreach (var summary in domains.Where(d => !d.IsKept))
                    {
                        _ = WinSqliteInterop.sqlite3_finalize(deleteStmt);
                        prepRc = WinSqliteInterop.sqlite3_prepare_v2(db, deleteSql, -1, out deleteStmt, IntPtr.Zero);
                        if (prepRc != WinSqliteInterop.SQLITE_OK)
                        {
                            throw new InvalidOperationException(WinSqliteInterop.GetErrorMessage(db));
                        }

                        var bindRc = WinSqliteInterop.sqlite3_bind_text(
                            deleteStmt,
                            1,
                            summary.Domain,
                            -1,
                            WinSqliteInterop.SQLITE_TRANSIENT);
                        if (bindRc != WinSqliteInterop.SQLITE_OK)
                        {
                            throw new InvalidOperationException(WinSqliteInterop.GetErrorMessage(db));
                        }

                        var stepRc = WinSqliteInterop.sqlite3_step(deleteStmt);
                        if (stepRc != WinSqliteInterop.SQLITE_DONE)
                        {
                            throw new InvalidOperationException(WinSqliteInterop.GetErrorMessage(db));
                        }

                        deletedCount += WinSqliteInterop.sqlite3_changes(db);
                    }
                }
                finally
                {
                    if (deleteStmt != IntPtr.Zero)
                    {
                        _ = WinSqliteInterop.sqlite3_finalize(deleteStmt);
                    }
                }

                WinSqliteInterop.ExecuteNonQuery(db, "COMMIT;");
            }
            catch
            {
                try
                {
                    WinSqliteInterop.ExecuteNonQuery(db, "ROLLBACK;");
                }
                catch
                {
                    // Ignore rollback errors
                }

                throw;
            }
            finally
            {
                _ = WinSqliteInterop.sqlite3_close_v2(db);
            }

            var remainingDomains = ReadCookieDomains(target.DatabasePath, target.Kind, normalizedKeep);
            var remainingCookies = remainingDomains.Sum(d => d.CookieCount);

            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Ignore backup cleanup errors
            }

            return new BrowserCookieCleanupResult(
                target.BrowserName,
                target.DatabasePath,
                SkippedBecauseRunning: false,
                DomainsPreserved: remainingDomains.Count,
                CookiesDeleted: deletedCount,
                CookiesRemaining: remainingCookies);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, target.DatabasePath, overwrite: true);
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Best-effort restore from backup
            }

            return new BrowserCookieCleanupResult(
                target.BrowserName,
                target.DatabasePath,
                SkippedBecauseRunning: false,
                DomainsPreserved: 0,
                CookiesDeleted: 0,
                CookiesRemaining: 0,
                ErrorMessage: ex.Message);
        }
    }

    private static (string TableName, string HostColumn) GetSchemaNames(BrowserCookieDbKind kind)
    {
        return kind switch
        {
            BrowserCookieDbKind.Firefox => ("moz_cookies", "host"),
            _ => ("cookies", "host_key")
        };
    }

    private static IEnumerable<string> GetChromiumProfileDirectories(string userDataPath)
    {
        var defaultProfile = Path.Combine(userDataPath, "Default");
        if (Directory.Exists(defaultProfile))
        {
            yield return defaultProfile;
        }

        if (File.Exists(Path.Combine(userDataPath, "Network", "Cookies")) ||
            File.Exists(Path.Combine(userDataPath, "Cookies")))
        {
            yield return userDataPath;
        }

        foreach (var subDir in Directory.EnumerateDirectories(userDataPath, "Profile *"))
        {
            yield return subDir;
        }
    }

    private static bool DefaultIsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
