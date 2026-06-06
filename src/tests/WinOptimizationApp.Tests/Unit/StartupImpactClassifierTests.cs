using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class StartupImpactClassifierTests
{
    [Fact]
    public void Analyze_ClassifiesScriptFromUserWritablePathAsHighImpact()
    {
        var result = StartupImpactClassifier.Analyze(
            "Updater",
            "HKCU Run",
            @"powershell.exe -File C:\Users\test\AppData\Local\Temp\update.ps1",
            enabled: true);

        Assert.Equal(StartupImpactLevel.High, result.Impact);
        Assert.True(result.CanDisable);
        Assert.True(result.CanRollback);
    }

    [Fact]
    public void Analyze_ProtectsWindowsSecurityComponents()
    {
        var result = StartupImpactClassifier.Analyze(
            "SecurityHealth",
            "HKLM Run",
            @"C:\Windows\System32\SecurityHealthSystray.exe",
            enabled: true);

        Assert.Equal(StartupImpactLevel.Low, result.Impact);
        Assert.False(result.CanDisable);
        Assert.False(result.CanRollback);
    }

    [Fact]
    public void Analyze_ClassifiesHeavyAppsAsMediumImpact()
    {
        var result = StartupImpactClassifier.Analyze(
            "Teams",
            "HKCU Run",
            @"C:\Users\test\AppData\Local\Microsoft\Teams\Update.exe --processStart Teams.exe",
            enabled: true);

        Assert.Equal(StartupImpactLevel.Medium, result.Impact);
        Assert.True(result.CanDisable);
        Assert.True(result.CanRollback);
    }

    [Fact]
    public void Analyze_DisabledEntriesAreLowImpactAndNotDisableable()
    {
        var result = StartupImpactClassifier.Analyze(
            "OldApp",
            "HKCU StartupApproved",
            @"C:\Tools\oldapp.exe",
            enabled: false);

        Assert.Equal(StartupImpactLevel.Low, result.Impact);
        Assert.False(result.CanDisable);
        Assert.True(result.CanRollback);
    }
}
