using System.Globalization;
using System.Text;
using System.Xml.Linq;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed record ScheduleOperationResult(bool Success, string? Error = null);

public enum ScheduleRegistrationState
{
    Missing,
    Current,
    /// <summary>The task exists but launches a different executable, for example after the portable app was moved.</summary>
    Stale
}

/// <summary>
/// Registers a per-user Windows Task Scheduler entry that launches the app in headless maintenance mode.
/// The task always runs with least privilege, so only cleanup items that do not need elevation and are
/// not high risk are eligible.
/// </summary>
public sealed class ScheduledMaintenanceService(CommandRunner commands)
{
    public const string TaskName = "WinOptimizationApp Scheduled Maintenance";

    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private readonly CommandRunner _commands = commands;

    public static IReadOnlyList<string> GetEligibleTaskIds(MaintenanceCatalog catalog) =>
        OneClickMaintenanceService.Items
            .Select(item => catalog.GetById(item.TaskId))
            .Where(IsEligible)
            .Select(task => task.Id)
            .ToList();

    public static IReadOnlyList<string> FilterEligible(IEnumerable<string> taskIds, MaintenanceCatalog catalog)
    {
        var eligible = GetEligibleTaskIds(catalog);
        var requested = taskIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return eligible.Where(requested.Contains).ToList();
    }

    public static DateTime? GetNextRun(
        MaintenanceScheduleFrequency frequency,
        TimeSpan time,
        DayOfWeek dayOfWeek,
        DateTime now)
    {
        var today = now.Date + time;
        switch (frequency)
        {
            case MaintenanceScheduleFrequency.Daily:
                return today > now ? today : today.AddDays(1);
            case MaintenanceScheduleFrequency.Weekly:
                var days = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
                var candidate = today.AddDays(days);
                return candidate > now ? candidate : candidate.AddDays(7);
            case MaintenanceScheduleFrequency.Monthly:
                var firstOfMonth = new DateTime(now.Year, now.Month, 1) + time;
                return firstOfMonth > now ? firstOfMonth : firstOfMonth.AddMonths(1);
            default:
                return null;
        }
    }

    public static XDocument BuildTaskDefinition(
        string executablePath,
        MaintenanceScheduleFrequency frequency,
        TimeSpan time,
        DayOfWeek dayOfWeek,
        DateTime now,
        string userId)
    {
        if (frequency == MaintenanceScheduleFrequency.Off)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "A disabled schedule has no task definition.");
        }

        var start = GetNextRun(frequency, time, dayOfWeek, now)!.Value;
        XElement schedule = frequency switch
        {
            MaintenanceScheduleFrequency.Daily => new XElement(TaskNamespace + "ScheduleByDay",
                new XElement(TaskNamespace + "DaysInterval", 1)),
            MaintenanceScheduleFrequency.Weekly => new XElement(TaskNamespace + "ScheduleByWeek",
                new XElement(TaskNamespace + "DaysOfWeek", new XElement(TaskNamespace + dayOfWeek.ToString())),
                new XElement(TaskNamespace + "WeeksInterval", 1)),
            _ => new XElement(TaskNamespace + "ScheduleByMonth",
                new XElement(TaskNamespace + "DaysOfMonth", new XElement(TaskNamespace + "Day", 1)),
                new XElement(TaskNamespace + "Months",
                    CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
                        .Where(name => name.Length > 0)
                        .Select(name => new XElement(TaskNamespace + name))))
        };

        return new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(TaskNamespace + "Task",
                new XAttribute("version", "1.2"),
                new XElement(TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Author", "Windows System Maintenance Tool"),
                    new XElement(TaskNamespace + "Description",
                        "Runs the cleanup items selected in Windows System Maintenance Tool settings.")),
                new XElement(TaskNamespace + "Triggers",
                    new XElement(TaskNamespace + "CalendarTrigger",
                        new XElement(TaskNamespace + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)),
                        new XElement(TaskNamespace + "Enabled", true),
                        schedule)),
                new XElement(TaskNamespace + "Principals",
                    new XElement(TaskNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskNamespace + "UserId", userId),
                        new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
                        new XElement(TaskNamespace + "RunLevel", "LeastPrivilege"))),
                new XElement(TaskNamespace + "Settings",
                    new XElement(TaskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(TaskNamespace + "DisallowStartIfOnBatteries", true),
                    new XElement(TaskNamespace + "StopIfGoingOnBatteries", false),
                    new XElement(TaskNamespace + "AllowHardTerminate", true),
                    new XElement(TaskNamespace + "StartWhenAvailable", true),
                    new XElement(TaskNamespace + "RunOnlyIfNetworkAvailable", false),
                    new XElement(TaskNamespace + "IdleSettings",
                        new XElement(TaskNamespace + "StopOnIdleEnd", false),
                        new XElement(TaskNamespace + "RestartOnIdle", false)),
                    new XElement(TaskNamespace + "AllowStartOnDemand", true),
                    new XElement(TaskNamespace + "Enabled", true),
                    new XElement(TaskNamespace + "Hidden", false),
                    new XElement(TaskNamespace + "RunOnlyIfIdle", false),
                    new XElement(TaskNamespace + "WakeToRun", false),
                    new XElement(TaskNamespace + "ExecutionTimeLimit", "PT2H"),
                    new XElement(TaskNamespace + "Priority", 7)),
                new XElement(TaskNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(TaskNamespace + "Exec",
                        new XElement(TaskNamespace + "Command", executablePath),
                        new XElement(TaskNamespace + "Arguments",
                            $"{HeadlessCommandLine.MaintenanceArgument} {HeadlessCommandLine.ScheduledArgument}"),
                        new XElement(TaskNamespace + "WorkingDirectory",
                            Path.GetDirectoryName(executablePath) ?? string.Empty)))));
    }

    public async Task<ScheduleOperationResult> RegisterAsync(
        MaintenanceScheduleFrequency frequency,
        TimeSpan time,
        DayOfWeek dayOfWeek,
        CancellationToken cancellationToken = default)
    {
        if (frequency == MaintenanceScheduleFrequency.Off)
        {
            return await UnregisterAsync(cancellationToken);
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(executablePath))
        {
            return new ScheduleOperationResult(false, "Unable to resolve the application executable.");
        }

        var userId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var definition = BuildTaskDefinition(executablePath, frequency, time, dayOfWeek, DateTime.Now, userId);
        var definitionPath = Path.Combine(Path.GetTempPath(), $"WinOptimizationApp-schedule-{Guid.NewGuid():N}.xml");
        try
        {
            await using (var writer = new StreamWriter(definitionPath, false, Encoding.Unicode))
            {
                await definition.SaveAsync(writer, SaveOptions.None, cancellationToken);
            }

            var result = await _commands.RunCaptureAsync(
                "schtasks.exe",
                $"/Create /TN \"{TaskName}\" /XML \"{definitionPath}\" /F",
                cancellationToken);
            return ToOperationResult(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ScheduleOperationResult(false, ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(definitionPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public async Task<ScheduleOperationResult> UnregisterAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsRegisteredAsync(cancellationToken))
        {
            return new ScheduleOperationResult(true);
        }

        return ToOperationResult(await _commands.RunCaptureAsync(
            "schtasks.exe",
            $"/Delete /TN \"{TaskName}\" /F",
            cancellationToken));
    }

    public async Task<ScheduleOperationResult> RunNowAsync(CancellationToken cancellationToken = default) =>
        ToOperationResult(await _commands.RunCaptureAsync(
            "schtasks.exe",
            $"/Run /TN \"{TaskName}\"",
            cancellationToken));

    public async Task<bool> IsRegisteredAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunCaptureAsync("schtasks.exe", $"/Query /TN \"{TaskName}\"", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<ScheduleRegistrationState> GetRegistrationStateAsync(CancellationToken cancellationToken = default)
    {
        // schtasks.exe writes redirected output in the OEM code page, which corrupts non-ASCII install paths,
        // so read the registered executable through PowerShell with an explicit UTF-8 output encoding.
        var result = await _commands.RunCaptureAsync(
            "powershell.exe",
            "-NoProfile -NonInteractive -Command \"[Console]::OutputEncoding = [Text.Encoding]::UTF8; " +
            $"$task = Get-ScheduledTask -TaskPath '\\' -TaskName '{TaskName}' -ErrorAction SilentlyContinue; " +
            "if ($task) { $task.Actions | ForEach-Object { $_.Execute } } else { exit 3 }\"",
            cancellationToken);
        if (result.ExitCode == 3)
        {
            return ScheduleRegistrationState.Missing;
        }

        if (result.ExitCode != 0)
        {
            // Fall back to existence only when PowerShell or the ScheduledTasks module is unavailable.
            return await IsRegisteredAsync(cancellationToken) ? ScheduleRegistrationState.Current : ScheduleRegistrationState.Missing;
        }

        return ClassifyRegistration(result.StandardOutput, Environment.ProcessPath);
    }

    internal static ScheduleRegistrationState ClassifyRegistration(string registeredExecutables, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return ScheduleRegistrationState.Current;
        }

        return registeredExecutables
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim('"'))
            .Any(line => line.Equals(executablePath, StringComparison.OrdinalIgnoreCase))
            ? ScheduleRegistrationState.Current
            : ScheduleRegistrationState.Stale;
    }

    private static bool IsEligible(MaintenanceTask task) =>
        !task.RequiresAdmin && task.RiskLevel != RiskLevel.High && task.Group is "Cleanup" or "Privacy";

    private static ScheduleOperationResult ToOperationResult(CommandResult result) =>
        result.ExitCode == 0
            ? new ScheduleOperationResult(true)
            : new ScheduleOperationResult(false, FirstNonEmpty(result.StandardError, result.StandardOutput, $"schtasks.exe exited with code {result.ExitCode}."));

    private static string FirstNonEmpty(params string[] values) =>
        values.Select(value => value.Trim()).First(value => value.Length > 0);
}
