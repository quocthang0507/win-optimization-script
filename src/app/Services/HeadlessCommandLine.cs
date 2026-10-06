namespace WinOptimizationApp.Services;

public enum HeadlessCommand
{
    Help,
    ListTasks,
    Maintenance
}

public sealed record HeadlessOptions(
    HeadlessCommand Command,
    IReadOnlyList<string> TaskIds,
    bool PreviewOnly,
    bool Scheduled,
    string? Error = null);

/// <summary>
/// Parses the command-line surface that runs maintenance without opening the WinUI window, so the
/// tool can be driven from scripts, Task Scheduler, or remote management tools.
/// </summary>
public static class HeadlessCommandLine
{
    public const string MaintenanceArgument = "--maintenance";
    public const string ListTasksArgument = "--list-tasks";
    public const string HelpArgument = "--help";
    public const string TasksArgument = "--tasks";
    public const string PreviewArgument = "--preview";
    public const string ScheduledArgument = "--scheduled";

    private static readonly string[] HelpAliases = [HelpArgument, "-h", "/?"];

    /// <summary>Returns null when the arguments describe a normal UI or runner launch.</summary>
    public static HeadlessOptions? TryParse(IReadOnlyList<string> args)
    {
        var isHelp = args.Any(arg => HelpAliases.Contains(arg, StringComparer.OrdinalIgnoreCase));
        var isList = args.Any(arg => arg.Equals(ListTasksArgument, StringComparison.OrdinalIgnoreCase));
        var isMaintenance = args.Any(arg => arg.Equals(MaintenanceArgument, StringComparison.OrdinalIgnoreCase));
        if (!isHelp && !isList && !isMaintenance)
        {
            return null;
        }

        if (isHelp)
        {
            return new HeadlessOptions(HeadlessCommand.Help, [], false, false);
        }

        if (isList && isMaintenance)
        {
            return Invalid($"{ListTasksArgument} cannot be combined with {MaintenanceArgument}.");
        }

        var taskIds = new List<string>();
        var previewOnly = false;
        var scheduled = false;
        var tasksSpecified = false;
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (arg.Equals(MaintenanceArgument, StringComparison.OrdinalIgnoreCase) ||
                arg.Equals(ListTasksArgument, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (arg.Equals(AppProcessLauncher.BaseDirectoryArgument, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (isList)
            {
                return Invalid($"Unknown argument: {arg}");
            }

            if (arg.Equals(PreviewArgument, StringComparison.OrdinalIgnoreCase))
            {
                previewOnly = true;
            }
            else if (arg.Equals(ScheduledArgument, StringComparison.OrdinalIgnoreCase))
            {
                scheduled = true;
            }
            else if (arg.Equals(TasksArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return Invalid($"{TasksArgument} requires a comma-separated list of task IDs.");
                }

                tasksSpecified = true;
                taskIds.AddRange(SplitTaskIds(args[++index]));
            }
            else if (arg.StartsWith(TasksArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                tasksSpecified = true;
                taskIds.AddRange(SplitTaskIds(arg[(TasksArgument.Length + 1)..]));
            }
            else
            {
                return Invalid($"Unknown argument: {arg}");
            }
        }

        if (isList)
        {
            return new HeadlessOptions(HeadlessCommand.ListTasks, [], false, false);
        }

        if (tasksSpecified && taskIds.Count == 0)
        {
            return Invalid($"{TasksArgument} requires at least one task ID.");
        }

        if (tasksSpecified && scheduled)
        {
            return Invalid($"{ScheduledArgument} uses the tasks saved in Settings and cannot be combined with {TasksArgument}.");
        }

        var unknown = taskIds
            .Where(id => !OneClickMaintenanceService.Items.Any(item => item.TaskId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unknown.Count > 0)
        {
            return Invalid($"Unsupported task ID(s): {string.Join(", ", unknown)}. Use {ListTasksArgument} to see valid IDs.");
        }

        return new HeadlessOptions(
            HeadlessCommand.Maintenance,
            taskIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            previewOnly,
            scheduled);
    }

    private static IEnumerable<string> SplitTaskIds(string value) =>
        value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static HeadlessOptions Invalid(string error) =>
        new(HeadlessCommand.Help, [], false, false, error);
}
