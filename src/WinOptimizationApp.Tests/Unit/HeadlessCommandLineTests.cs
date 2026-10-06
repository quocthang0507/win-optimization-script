using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class HeadlessCommandLineTests
{
    [Theory]
    [InlineData]
    [InlineData("--ui")]
    [InlineData("--runner")]
    [InlineData("--ui", "--connect-runner", "--base-dir", "C:\\App")]
    public void TryParse_ReturnsNullForNormalLaunches(params string[] args)
    {
        Assert.Null(HeadlessCommandLine.TryParse(args));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void TryParse_RecognizesHelpAliases(string argument)
    {
        var options = HeadlessCommandLine.TryParse([argument]);

        Assert.NotNull(options);
        Assert.Equal(HeadlessCommand.Help, options.Command);
        Assert.Null(options.Error);
    }

    [Fact]
    public void TryParse_MaintenanceWithoutTasksUsesDefaults()
    {
        var options = HeadlessCommandLine.TryParse(["--maintenance"]);

        Assert.NotNull(options);
        Assert.Equal(HeadlessCommand.Maintenance, options.Command);
        Assert.Empty(options.TaskIds);
        Assert.False(options.PreviewOnly);
        Assert.False(options.Scheduled);
        Assert.Null(options.Error);
    }

    [Fact]
    public void TryParse_AcceptsSeparateAndInlineTaskListsCaseInsensitively()
    {
        var options = HeadlessCommandLine.TryParse(
            ["--MAINTENANCE", "--tasks", "cleanup.temp, network.dns", "--tasks=CLEANUP.TEMP;cleanup.shaders", "--preview"]);

        Assert.NotNull(options);
        Assert.Null(options.Error);
        Assert.True(options.PreviewOnly);
        Assert.Equal(["cleanup.temp", "network.dns", "cleanup.shaders"], options.TaskIds);
    }

    [Fact]
    public void TryParse_SkipsBaseDirectoryValue()
    {
        var options = HeadlessCommandLine.TryParse(["--maintenance", "--base-dir", "C:\\Portable App", "--scheduled"]);

        Assert.NotNull(options);
        Assert.Null(options.Error);
        Assert.True(options.Scheduled);
    }

    [Theory]
    [InlineData("--maintenance", "--tasks", "repair.dism")]
    [InlineData("--maintenance", "--tasks", "cleanup.windowsold")]
    [InlineData("--maintenance", "--tasks", "does.not.exist")]
    public void TryParse_RejectsTasksOutsideTheOneClickAllowList(params string[] args)
    {
        var options = HeadlessCommandLine.TryParse(args);

        Assert.NotNull(options);
        Assert.Contains("Unsupported task ID", options.Error);
    }

    [Theory]
    [InlineData("--maintenance", "--tasks")]
    [InlineData("--maintenance", "--tasks", "--preview")]
    [InlineData("--maintenance", "--tasks=")]
    [InlineData("--maintenance", "--force")]
    [InlineData("--maintenance", "--list-tasks")]
    [InlineData("--list-tasks", "--preview")]
    [InlineData("--maintenance", "--scheduled", "--tasks", "cleanup.temp")]
    public void TryParse_ReportsUsageErrors(params string[] args)
    {
        var options = HeadlessCommandLine.TryParse(args);

        Assert.NotNull(options);
        Assert.NotNull(options.Error);
    }

    [Fact]
    public async Task Runner_ReturnsUsageExitCodeAndPrintsHelpForInvalidArguments()
    {
        var output = new StringWriter();
        var runner = new HeadlessMaintenanceRunner(output, new AppSettings { Language = AppLanguage.English });

        var exitCode = await runner.RunAsync(HeadlessCommandLine.TryParse(["--maintenance", "--bogus"])!);

        Assert.Equal(HeadlessMaintenanceRunner.ExitUsage, exitCode);
        Assert.Contains("Unknown argument: --bogus", output.ToString());
        Assert.Contains("--list-tasks", output.ToString());
    }

    [Fact]
    public async Task Runner_ListsEveryOneClickTask()
    {
        var output = new StringWriter();
        var runner = new HeadlessMaintenanceRunner(output, new AppSettings { Language = AppLanguage.English });

        var exitCode = await runner.RunAsync(HeadlessCommandLine.TryParse(["--list-tasks"])!);

        Assert.Equal(HeadlessMaintenanceRunner.ExitSuccess, exitCode);
        Assert.All(OneClickMaintenanceService.Items, item => Assert.Contains(item.TaskId, output.ToString()));
    }

    [Fact]
    public void Runner_ScheduledRunsIgnoreIneligibleTasksSavedInSettings()
    {
        var settings = new AppSettings
        {
            ScheduledMaintenanceTaskIds = ["cleanup.temp", "cleanup.windowsupdate", "optimization.drives", "repair.dism"]
        };
        var runner = new HeadlessMaintenanceRunner(TextWriter.Null, settings);

        var taskIds = runner.ResolveTaskIds(new HeadlessOptions(HeadlessCommand.Maintenance, [], false, Scheduled: true));

        Assert.Equal(["cleanup.temp"], taskIds);
    }

    [Fact]
    public void Runner_ManualRunsWithoutTasksUseOneClickDefaults()
    {
        var runner = new HeadlessMaintenanceRunner(TextWriter.Null, new AppSettings());

        var taskIds = runner.ResolveTaskIds(new HeadlessOptions(HeadlessCommand.Maintenance, [], false, false));

        Assert.Equal(
            OneClickMaintenanceService.Items.Where(item => item.DefaultSelected).Select(item => item.TaskId),
            taskIds);
    }
}
