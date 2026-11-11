using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class BrowserCookieServiceTests
{
    [Fact]
    public void CleanCookieDatabase_DeletesNonKeepListDomains_AndPreservesSubdomainsOfKeepList()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"winopt-cookies-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "Cookies");

        try
        {
            CreateSampleChromiumCookieDb(dbPath);
            var service = new BrowserCookieService(_ => false);

            var domainsBefore = service.ReadCookieDomains(dbPath, BrowserCookieDbKind.Chromium, ["github.com"]);
            Assert.Equal(4, domainsBefore.Count);

            var target = new BrowserCookieTarget(
                "TestBrowser",
                "Default",
                "testbrowser",
                dbPath,
                BrowserCookieDbKind.Chromium);

            var result = service.CleanCookieDatabase(target, ["github.com"]);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.SkippedBecauseRunning);
            Assert.Equal(2, result.CookiesDeleted);
            Assert.Equal(2, result.CookiesRemaining);

            var domainsAfter = service.ReadCookieDomains(dbPath, BrowserCookieDbKind.Chromium, ["github.com"]);
            Assert.All(domainsAfter, d => Assert.True(d.IsKept));
            Assert.Contains(domainsAfter, d => d.Domain == ".github.com");
            Assert.Contains(domainsAfter, d => d.Domain == "gist.github.com");
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore temp cleanup
            }
        }
    }

    [Fact]
    public void CleanCookieDatabase_SkipsWhenBrowserProcessIsRunning()
    {
        var service = new BrowserCookieService(_ => true);
        var target = new BrowserCookieTarget("Chrome", "Default", "chrome", @"C:\nonexistent\Cookies", BrowserCookieDbKind.Chromium);

        var result = service.CleanCookieDatabase(target, ["github.com"]);
        Assert.True(result.SkippedBecauseRunning);
        Assert.Equal(0, result.CookiesDeleted);
    }

    private static void CreateSampleChromiumCookieDb(string dbPath)
    {
        var rc = WinSqliteInterop.sqlite3_open_v2(
            dbPath,
            out var db,
            WinSqliteInterop.SQLITE_OPEN_READWRITE | WinSqliteInterop.SQLITE_OPEN_CREATE,
            IntPtr.Zero);
        Assert.Equal(WinSqliteInterop.SQLITE_OK, rc);

        try
        {
            WinSqliteInterop.ExecuteNonQuery(db, """
                CREATE TABLE cookies (
                    host_key TEXT NOT NULL,
                    name TEXT NOT NULL,
                    value TEXT NOT NULL
                );
                INSERT INTO cookies (host_key, name, value) VALUES ('.github.com', 'user_session', 'abc');
                INSERT INTO cookies (host_key, name, value) VALUES ('gist.github.com', 'gist_token', 'def');
                INSERT INTO cookies (host_key, name, value) VALUES ('.doubleclick.net', 'ad_id', '123');
                INSERT INTO cookies (host_key, name, value) VALUES ('tracker.example.org', 'trk', '456');
                """);
        }
        finally
        {
            _ = WinSqliteInterop.sqlite3_close_v2(db);
        }
    }
}
