using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class AppProcessLauncherTests
{
    [Fact]
    public void CreateRunnerPipeName_ReturnsUniqueValidatedNames()
    {
        var first = AppProcessLauncher.CreateRunnerPipeName();
        var second = AppProcessLauncher.CreateRunnerPipeName();

        Assert.True(AppProcessLauncher.IsValidRunnerPipeName(first));
        Assert.True(AppProcessLauncher.IsValidRunnerPipeName(second));
        Assert.NotEqual(first, second);
        Assert.False(AppProcessLauncher.IsValidRunnerPipeName("WinOptimizationApp_Runner"));
    }

    [Fact]
    public void GetRunnerPipeName_AcceptsOnlyValidatedPipeArgument()
    {
        var pipeName = AppProcessLauncher.CreateRunnerPipeName();

        Assert.Equal(
            pipeName,
            AppProcessLauncher.GetRunnerPipeName(["app.exe", AppProcessLauncher.RunnerPipeArgument, pipeName]));
        Assert.Null(AppProcessLauncher.GetRunnerPipeName(
            ["app.exe", AppProcessLauncher.RunnerPipeArgument, "WinOptimizationApp_Runner_fixed"]));
    }
}
