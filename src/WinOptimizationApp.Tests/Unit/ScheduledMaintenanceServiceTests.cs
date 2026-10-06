using System.Xml.Linq;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ScheduledMaintenanceServiceTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private readonly MaintenanceCatalog _catalog = new();

    [Fact]
    public void EligibleTasks_ExcludeAdminAndHighRiskItems()
    {
        var eligible = ScheduledMaintenanceService.GetEligibleTaskIds(_catalog);

        Assert.Contains("cleanup.temp", eligible);
        Assert.NotEmpty(eligible);
        Assert.All(eligible, id =>
        {
            var task = _catalog.GetById(id);
            Assert.False(task.RequiresAdmin);
            Assert.NotEqual(RiskLevel.High, task.RiskLevel);
            Assert.Contains(OneClickMaintenanceService.Items, item => item.TaskId == id);
        });
        Assert.DoesNotContain("cleanup.windowsupdate", eligible);
        Assert.DoesNotContain("optimization.drives", eligible);
        // ipconfig /flushdns needs elevation on current Windows 11 builds, so it cannot run under a limited task.
        Assert.DoesNotContain("network.dns", eligible);
    }

    [Fact]
    public void FilterEligible_KeepsCatalogOrderAndDropsUnknownIds()
    {
        var filtered = ScheduledMaintenanceService.FilterEligible(
            ["cleanup.recyclebin", "made.up", "CLEANUP.TEMP", "cleanup.prefetch", "network.dns"],
            _catalog);

        Assert.Equal(["cleanup.temp", "cleanup.recyclebin"], filtered);
    }

    [Theory]
    [InlineData("2026-10-06T09:00:00", "2026-10-06T12:00:00")]
    [InlineData("2026-10-06T12:00:00", "2026-10-07T12:00:00")]
    [InlineData("2026-10-06T23:59:00", "2026-10-07T12:00:00")]
    public void GetNextRun_Daily(string now, string expected)
    {
        var next = ScheduledMaintenanceService.GetNextRun(
            MaintenanceScheduleFrequency.Daily, new TimeSpan(12, 0, 0), DayOfWeek.Sunday, DateTime.Parse(now));

        Assert.Equal(DateTime.Parse(expected), next);
    }

    [Theory]
    // 2026-10-06 is a Tuesday.
    [InlineData("2026-10-06T09:00:00", DayOfWeek.Tuesday, "2026-10-06T12:00:00")]
    [InlineData("2026-10-06T13:00:00", DayOfWeek.Tuesday, "2026-10-13T12:00:00")]
    [InlineData("2026-10-06T13:00:00", DayOfWeek.Sunday, "2026-10-11T12:00:00")]
    [InlineData("2026-10-06T13:00:00", DayOfWeek.Monday, "2026-10-12T12:00:00")]
    public void GetNextRun_Weekly(string now, DayOfWeek day, string expected)
    {
        var next = ScheduledMaintenanceService.GetNextRun(
            MaintenanceScheduleFrequency.Weekly, new TimeSpan(12, 0, 0), day, DateTime.Parse(now));

        Assert.Equal(DateTime.Parse(expected), next);
    }

    [Theory]
    [InlineData("2026-10-01T08:00:00", "2026-10-01T12:00:00")]
    [InlineData("2026-10-06T08:00:00", "2026-11-01T12:00:00")]
    [InlineData("2026-12-15T08:00:00", "2027-01-01T12:00:00")]
    public void GetNextRun_Monthly(string now, string expected)
    {
        var next = ScheduledMaintenanceService.GetNextRun(
            MaintenanceScheduleFrequency.Monthly, new TimeSpan(12, 0, 0), DayOfWeek.Sunday, DateTime.Parse(now));

        Assert.Equal(DateTime.Parse(expected), next);
    }

    [Fact]
    public void GetNextRun_OffHasNoNextRun()
    {
        Assert.Null(ScheduledMaintenanceService.GetNextRun(
            MaintenanceScheduleFrequency.Off, TimeSpan.Zero, DayOfWeek.Sunday, DateTime.Now));
    }

    [Fact]
    public void BuildTaskDefinition_RunsHeadlessWithLeastPrivilegeAndSafeSettings()
    {
        var document = ScheduledMaintenanceService.BuildTaskDefinition(
            @"C:\Tools\Win Opt\WinOptimizationApp.exe",
            MaintenanceScheduleFrequency.Weekly,
            new TimeSpan(18, 30, 0),
            DayOfWeek.Friday,
            DateTime.Parse("2026-10-06T09:00:00"),
            @"PC\user");

        var root = document.Root!;
        Assert.Equal("LeastPrivilege", root.Descendants(Ns + "RunLevel").Single().Value);
        Assert.Equal("InteractiveToken", root.Descendants(Ns + "LogonType").Single().Value);
        Assert.Equal(@"PC\user", root.Descendants(Ns + "UserId").Single().Value);
        Assert.Equal(@"C:\Tools\Win Opt\WinOptimizationApp.exe", root.Descendants(Ns + "Command").Single().Value);
        Assert.Equal(@"C:\Tools\Win Opt", root.Descendants(Ns + "WorkingDirectory").Single().Value);
        Assert.Equal("--maintenance --scheduled", root.Descendants(Ns + "Arguments").Single().Value);
        Assert.Equal("2026-10-09T18:30:00", root.Descendants(Ns + "StartBoundary").Single().Value);
        Assert.NotNull(root.Descendants(Ns + "DaysOfWeek").Single().Element(Ns + "Friday"));
        Assert.Equal("true", root.Descendants(Ns + "StartWhenAvailable").Single().Value);
        Assert.Equal("true", root.Descendants(Ns + "DisallowStartIfOnBatteries").Single().Value);
        Assert.Equal("IgnoreNew", root.Descendants(Ns + "MultipleInstancesPolicy").Single().Value);
        Assert.Equal("PT2H", root.Descendants(Ns + "ExecutionTimeLimit").Single().Value);
    }

    [Fact]
    public void BuildTaskDefinition_MonthlyIncludesEveryMonth()
    {
        var document = ScheduledMaintenanceService.BuildTaskDefinition(
            @"C:\App\WinOptimizationApp.exe",
            MaintenanceScheduleFrequency.Monthly,
            new TimeSpan(9, 0, 0),
            DayOfWeek.Sunday,
            DateTime.Parse("2026-10-06T09:00:00"),
            @"PC\user");

        Assert.Equal(12, document.Root!.Descendants(Ns + "Months").Single().Elements().Count());
        Assert.Equal("1", document.Root.Descendants(Ns + "Day").Single().Value);
    }

    [Theory]
    [InlineData("C:\\Tëst & Ứng dụng\\WinOptimizationApp.exe\r\n", ScheduleRegistrationState.Current)]
    [InlineData("\"c:\\tëst & ứng dụng\\winoptimizationapp.exe\"\n", ScheduleRegistrationState.Current)]
    [InlineData("D:\\Old Location\\WinOptimizationApp.exe\r\n", ScheduleRegistrationState.Stale)]
    [InlineData("", ScheduleRegistrationState.Stale)]
    public void ClassifyRegistration_ComparesRegisteredExecutableWithCurrentPath(string registered, ScheduleRegistrationState expected)
    {
        Assert.Equal(
            expected,
            ScheduledMaintenanceService.ClassifyRegistration(registered, "C:\\Tëst & Ứng dụng\\WinOptimizationApp.exe"));
    }

    [Fact]
    public void BuildTaskDefinition_RejectsDisabledSchedule()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScheduledMaintenanceService.BuildTaskDefinition(
            @"C:\App\WinOptimizationApp.exe",
            MaintenanceScheduleFrequency.Off,
            TimeSpan.Zero,
            DayOfWeek.Sunday,
            DateTime.Now,
            @"PC\user"));
    }
}
