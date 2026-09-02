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
}
