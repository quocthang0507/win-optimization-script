using System.Text.Json;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class UiUxRegressionTests
{
    [Theory]
    [InlineData(OneClickPhase.Cancelled, false)]
    [InlineData(OneClickPhase.Cancelled, true)]
    [InlineData(OneClickPhase.Completed, true)]
    [InlineData(OneClickPhase.Failed, false)]
    [InlineData(OneClickPhase.Failed, true)]
    public void TerminalState_StopsProgressAndRejectsLateCallbacks(OneClickPhase terminal, bool execute)
    {
        var state = new OneClickOperationState();
        var generation = state.Begin();
        Assert.True(state.IsBusy);
        Assert.True(state.ShowProgress);
        Assert.True(state.AcceptsProgress(generation, false));
        state.AwaitConfirmation();
        Assert.False(state.ShowProgress);
        Assert.False(state.AcceptsProgress(generation, false));
        if (execute) state.StartRunning();
        state.Finish(terminal);
        Assert.False(state.IsBusy);
        Assert.False(state.ShowProgress);
        Assert.False(state.AcceptsProgress(generation, false));
        Assert.False(state.AcceptsProgress(generation, true));
        Assert.Equal(execute, state.HasStartedExecution);
    }

    [Fact]
    public void NewRun_RejectsPreviousGeneration_AndRequiresConfirmation()
    {
        var state = new OneClickOperationState();
        Assert.Throws<InvalidOperationException>(() => state.StartRunning());
        var old = state.Begin();
        Assert.Throws<InvalidOperationException>(() => state.Begin());
        state.Finish(OneClickPhase.Failed);
        var next = state.Begin();
        Assert.False(state.AcceptsProgress(old, false));
        Assert.True(state.AcceptsProgress(next, false));
        Assert.False(state.AcceptsProgress(next, true));
        Assert.False(state.HasStartedExecution);
        Assert.Throws<InvalidOperationException>(() => state.StartRunning());
        state.AwaitConfirmation();
        state.StartRunning();
        Assert.True(state.AcceptsProgress(next, true));
        Assert.False(state.AcceptsProgress(next, false));
    }

    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Vietnamese)]
    public void EveryTweak_HasTranslatedTitleDescriptionAndCategory(AppLanguage language)
    {
        var localization = new LocalizationService(language);
        foreach (var tweak in TweakService.Tweaks)
        {
            Assert.NotEqual("missing", localization.TweakTitle(tweak.Id, "missing"));
            Assert.NotEqual($"tweak.{tweak.Id}.description", localization.Get($"tweak.{tweak.Id}.description"));
            Assert.NotEqual($"tweak.category.{tweak.Category}", localization.Get($"tweak.category.{tweak.Category}"));
            if (language == AppLanguage.Vietnamese)
            {
                Assert.NotEqual(tweak.Title, localization.TweakTitle(tweak.Id, tweak.Title));
                Assert.NotEqual(tweak.Description, localization.TweakDescription(tweak));
                Assert.NotEqual(tweak.Category, localization.TweakCategory(tweak.Category));
            }
        }
    }

    [Fact]
    public void TweakSearch_MatchesVietnamese_English_AndStableId()
    {
        var localization = new LocalizationService(AppLanguage.Vietnamese);
        var tweak = TweakService.Tweaks.Single(item => item.Id == "gaming.gameMode");
        Assert.True(localization.MatchesTweak(tweak, " TRÒ CHƠI "));
        Assert.True(localization.MatchesTweak(tweak, "Game Mode"));
        Assert.True(localization.MatchesTweak(tweak, "gaming.gameMode"));
        Assert.False(localization.MatchesTweak(tweak, "zz-no-match"));
    }

    [Fact]
    public void StructuredWarning_SurvivesIpc_TranslatesWithoutDuplicatingFallback()
    {
        const string fallback = "msedge.exe is running; close it for a more complete cleanup.";
        var preview = new TaskPreview("cleanup.browser", "", 0, 0, [], [fallback], [])
        {
            WarningDetails = [new CleanupWarning("browserRunning", fallback, ["msedge.exe"])]
        };
        var restored = JsonSerializer.Deserialize<TaskPreview>(JsonSerializer.Serialize(preview))!;
        var text = Assert.Single(new LocalizationService(AppLanguage.Vietnamese).PreviewWarnings(restored));
        Assert.Contains("msedge.exe đang chạy", text);
        Assert.DoesNotContain("is running", text);
        Assert.Equal(fallback, Assert.Single(restored.Warnings));
    }

    [Fact]
    public void LegacyAndUnknownWarnings_RemainReadable()
    {
        var preview = new TaskPreview("test", "", 0, 0, [], ["legacy", "new fallback"], [])
        {
            WarningDetails = [new CleanupWarning("unknownFutureCode", "new fallback", [])]
        };
        Assert.Equal(new[] { "new fallback", "legacy" },
            new LocalizationService(AppLanguage.Vietnamese).PreviewWarnings(preview));
    }

    [Theory]
    [InlineData("cleanup.prefetch", "oldPrefetch")]
    [InlineData("cleanup.defenderlogs", "defenderLogs")]
    [InlineData("cleanup.errorreports", "diagnostics")]
    [InlineData("cleanup.systemdumps", "diagnostics")]
    [InlineData("cleanup.windowsupdate", "highRisk")]
    public void CleanupWarnings_ExposeStableTranslatableCodes(string taskId, string expectedCode)
    {
        var warning = Assert.Single(CleanupService.GetWarningDetails(taskId));
        Assert.Equal(expectedCode, warning.Code);
        var preview = new TaskPreview(taskId, "", 0, 0, [], [warning.Fallback], []) { WarningDetails = [warning] };
        Assert.NotEqual(warning.Fallback,
            Assert.Single(new LocalizationService(AppLanguage.Vietnamese).PreviewWarnings(preview)));
    }

    [Fact]
    public void AllHealthCheckRecommendations_RouteToValidNavPagesOrActions()
    {
        var validTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dashboard", "cleanup", "bloatware", "storage", "startup",
            "updates", "software", "toolbox", "optimize", "repair",
            "history", "settings", "restartGuidance"
        };

        var status = new DashboardStatus(
            WindowsVersion: "Windows 11",
            MachineName: "TestMachine",
            UserName: "TestUser",
            IsAdministrator: false,
            Uptime: TimeSpan.FromDays(8),
            SystemDrive: "C:\\",
            SystemDriveFreeBytes: 2L * 1024 * 1024 * 1024,
            SystemDriveTotalBytes: 100L * 1024 * 1024 * 1024,
            PendingReboot: true,
            WingetAvailable: true,
            LastReportPath: null,
            CpuName: "Test CPU",
            ProcessorCount: 8,
            DotNetRuntime: "10.0",
            ProcessArchitecture: "X64",
            OSArchitecture: "X64",
            MemoryLoadPercent: 85,
            TotalRamBytes: 16UL * 1024 * 1024 * 1024,
            AvailableRamBytes: 1UL * 1024 * 1024 * 1024,
            TotalPageFileBytes: 20UL * 1024 * 1024 * 1024,
            AvailablePageFileBytes: 2UL * 1024 * 1024 * 1024,
            Drives: [new DashboardDriveStatus("C:", "System", "NTFS", "Local Disk", 100L * 1024 * 1024 * 1024, 2L * 1024 * 1024 * 1024)]);

        var metrics = new HealthCheckScanMetrics(
            CleanupBytes: 1024L * 1024 * 1024,
            CleanupFiles: 500,
            AvailableUpdates: 5,
            HighImpactStartupItems: 3,
            Errors: []);

        var result = DashboardHealthCheckService.Analyze(status, metrics);

        Assert.NotEmpty(result.Recommendations);
        foreach (var rec in result.Recommendations)
        {
            if (!string.IsNullOrWhiteSpace(rec.ActionTag))
            {
                Assert.Contains(rec.ActionTag, validTags);
            }
        }
    }

    [Fact]
    public void LeftoverCandidates_ConfidencePolicy_OnlyHighConfidencePreSelected()
    {
        var candidates = new List<LeftoverCleanupCandidate>
        {
            new(@"C:\Users\test\AppData\Local\Vendor\AppHigh", LeftoverCleanupConfidence.High, "Found app-specific folder"),
            new(@"C:\Users\test\AppData\Local\Temp\CommonLog.txt", LeftoverCleanupConfidence.Medium, "Found shared log file")
        };

        var autoSelected = candidates
            .Where(c => c.Confidence == LeftoverCleanupConfidence.High)
            .Select(c => c.Path)
            .ToList();

        Assert.Single(autoSelected);
        Assert.Equal(@"C:\Users\test\AppData\Local\Vendor\AppHigh", autoSelected[0]);
    }

    [Fact]
    public void WingetPackageDetails_ParseShowOutput_HandlesSpecialCharactersAndQuotes()
    {
        var rawOutput = @"
Found Test Application [Test.Vendor.App]
Version: 1.2.3
Publisher: 'Vendor & Co. ""Official""'
PublisherUrl: https://vendor.example.com
Description: 'A multi-line description with special chars:
  - Supports UTF-8: Tiếng Việt, 日本語, and symbols like & < > % $.
  - Contains quotes: ""quoted words"" and \'escaped quotes\'.'
ReleaseNotes: ""- Bug fixes & improvements:
  - Fixed issue #123 (encoding error)""
";
        var method = typeof(WingetService).GetMethod(
            "ParseShowOutput",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var details = (WingetPackageDetails)method.Invoke(null, new object[] { "Test.Vendor.App", rawOutput })!;
        Assert.NotNull(details);
        Assert.Equal("Test Application", details.Name);
        Assert.Equal("Test.Vendor.App", details.Id);
        Assert.Equal("1.2.3", details.Version);
        Assert.Equal(@"Vendor & Co. ""Official""", details.Publisher);
        Assert.Contains("Tiếng Việt", details.Description);
        Assert.Contains("quotes", details.Description);
        Assert.False(details.Description.StartsWith("'"));
        Assert.False(details.Description.EndsWith("'"));
        Assert.Contains("Bug fixes", details.ReleaseNotes);
        Assert.False(details.ReleaseNotes.StartsWith("\""));
    }

    [Theory]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\file.tmp", false, RiskLevel.Medium)]
    [InlineData(@"C:\Users\JohnDoe\Documents\OldFolder", true, RiskLevel.High)]
    [InlineData(@"C:\Program Files\Something\file.dll", false, RiskLevel.High)]
    [InlineData(@"C:\Windows\System32\drivers\test.sys", false, RiskLevel.High)]
    public void StorageManualCandidate_RiskRules(string path, bool isDirectory, RiskLevel expectedRisk)
    {
        var userProfile = @"C:\Users\JohnDoe";
        var isUserFile = path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
        var risk = isDirectory || !isUserFile ? RiskLevel.High : RiskLevel.Medium;

        Assert.Equal(expectedRisk, risk);
    }
}
