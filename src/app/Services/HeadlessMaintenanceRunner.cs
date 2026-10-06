using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

/// <summary>
/// Runs one-click maintenance items without the WinUI window. Used by scripts and by the scheduled task.
/// </summary>
public sealed class HeadlessMaintenanceRunner
{
    public const int ExitSuccess = 0;
    public const int ExitTaskFailed = 1;
    public const int ExitUsage = 2;
    public const int ExitCancelled = 3;
    public const int ExitAlreadyRunning = 4;

    private const string MutexName = @"Local\WinOptimizationApp_HeadlessMaintenance";

    private readonly TextWriter _output;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly MaintenanceCatalog _catalog = new();
    private readonly ReportService _reports;
    private readonly OneClickMaintenanceService _maintenance;

    public HeadlessMaintenanceRunner(TextWriter output, AppSettings settings, PathService? paths = null)
    {
        _output = output;
        _settings = settings;
        _localization = new LocalizationService(settings.Language);
        paths ??= new PathService();
        var commands = new CommandRunner();
        _reports = new ReportService(paths);
        var cleanup = new CleanupService(commands);
        var execution = new MaintenanceExecutionService(cleanup, commands, paths, _reports, new RestorePointService(commands));
        _maintenance = new OneClickMaintenanceService(cleanup, execution, _catalog);
    }

    public async Task<int> RunAsync(HeadlessOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Error is not null)
        {
            _output.WriteLine(options.Error);
            _output.WriteLine();
            WriteHelp();
            return ExitUsage;
        }

        switch (options.Command)
        {
            case HeadlessCommand.Help:
                WriteHelp();
                return ExitSuccess;
            case HeadlessCommand.ListTasks:
                WriteTaskList();
                return ExitSuccess;
        }

        // Existence of the named mutex marks a run in progress. It is not owned because the awaited work
        // resumes on pool threads, where releasing a thread-affine mutex would throw.
        using var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        if (!createdNew)
        {
            _output.WriteLine(_localization.Get("headless.alreadyRunning"));
            return ExitAlreadyRunning;
        }

        return await RunMaintenanceAsync(options, cancellationToken);
    }

    internal IReadOnlyList<string> ResolveTaskIds(HeadlessOptions options)
    {
        if (options.Scheduled)
        {
            return ScheduledMaintenanceService.FilterEligible(_settings.ScheduledMaintenanceTaskIds, _catalog);
        }

        return options.TaskIds.Count > 0
            ? options.TaskIds
            : OneClickMaintenanceService.Items.Where(item => item.DefaultSelected).Select(item => item.TaskId).ToList();
    }

    private async Task<int> RunMaintenanceAsync(HeadlessOptions options, CancellationToken cancellationToken)
    {
        var taskIds = ResolveTaskIds(options);
        if (taskIds.Count == 0)
        {
            _output.WriteLine(_localization.Get("headless.noTasks"));
            return ExitSuccess;
        }

        var protectedPaths = ProtectedPathService.NormalizePaths(_settings.ProtectedPaths).ToList();
        try
        {
            var preview = await _maintenance.PreviewAsync(taskIds, protectedPaths, cancellationToken: cancellationToken);
            _output.WriteLine(_localization.Get(options.PreviewOnly ? "headless.previewHeader" : "headless.runHeader"));
            foreach (var item in preview.Tasks)
            {
                _output.WriteLine(_localization.Format(
                    "headless.previewLine",
                    _localization.TaskLabel(item.Task.Id, item.Task.Label),
                    Formatters.FormatBytes(item.Preview.EstimatedBytes),
                    item.Preview.EstimatedFileCount));
            }

            _output.WriteLine(_localization.Format(
                "headless.previewTotal",
                Formatters.FormatBytes(preview.EstimatedBytes),
                preview.EstimatedFileCount));
            if (options.PreviewOnly)
            {
                return ExitSuccess;
            }

            var progress = new Progress<OneClickProgress>(item => _output.WriteLine(_localization.Format(
                "headless.progress",
                item.Current,
                item.Total,
                _localization.TaskLabel(item.TaskId, item.TaskId))));
            var summary = await _maintenance.RunAsync(preview, protectedPaths, progress, cancellationToken);
            foreach (var result in summary.Results)
            {
                _output.WriteLine(_localization.Format(
                    result.Success ? "headless.resultOk" : "headless.resultFailed",
                    _localization.TaskLabel(result.TaskId, result.TaskLabel),
                    Formatters.FormatBytes(result.FreedBytes)));
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"    {error}");
                }
            }

            _output.WriteLine(_localization.Format(
                "headless.summary",
                Formatters.FormatBytes(summary.FreedBytes),
                summary.FilesRemoved,
                summary.FilesSkipped));
            ApplyRetention();

            if (summary.Cancelled)
            {
                _output.WriteLine(_localization.Get("headless.cancelled"));
                return ExitCancelled;
            }

            return summary.Results.All(result => result.Success) ? ExitSuccess : ExitTaskFailed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _output.WriteLine(_localization.Get("headless.cancelled"));
            return ExitCancelled;
        }
    }

    private void ApplyRetention()
    {
        var removed = _reports.PruneReports(_settings.ReportRetentionDays);
        if (removed > 0)
        {
            _output.WriteLine(_localization.Format("headless.reportsPruned", removed));
        }
    }

    private void WriteTaskList()
    {
        var scheduledEligible = ScheduledMaintenanceService.GetEligibleTaskIds(_catalog).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _output.WriteLine(_localization.Get("headless.taskListHeader"));
        foreach (var (definition, task) in _maintenance.GetItems())
        {
            var flags = new List<string> { _localization.RiskName(task.RiskLevel) };
            if (definition.DefaultSelected) flags.Add(_localization.Get("headless.flagDefault"));
            if (task.RequiresAdmin) flags.Add(_localization.Get("headless.flagAdmin"));
            if (scheduledEligible.Contains(task.Id)) flags.Add(_localization.Get("headless.flagSchedulable"));
            _output.WriteLine($"  {task.Id,-24} {_localization.TaskLabel(task.Id, task.Label)} [{string.Join(", ", flags)}]");
        }
    }

    private void WriteHelp()
    {
        _output.WriteLine(_localization.Get("headless.help"));
    }
}
