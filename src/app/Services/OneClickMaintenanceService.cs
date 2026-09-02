using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class OneClickMaintenanceService(
    CleanupService cleanup,
    MaintenanceExecutionService execution,
    MaintenanceCatalog catalog)
{
    private readonly CleanupService _cleanup = cleanup;
    private readonly MaintenanceExecutionService _execution = execution;
    private readonly MaintenanceCatalog _catalog = catalog;

    public static IReadOnlyList<OneClickItemDefinition> Items { get; } =
    [
        new("cleanup.temp", true, false),
        new("cleanup.browser", true, false),
        new("cleanup.shaders", false, false),
        new("cleanup.crashdumps", false, false),
        new("cleanup.errorreports", false, false),
        new("cleanup.prefetch", false, false),
        new("cleanup.defenderlogs", false, false),
        new("cleanup.systemdumps", false, false),
        new("cleanup.recyclebin", false, false),
        new("privacy.recentFiles", false, false),
        new("privacy.powershell", false, false),
        new("cleanup.dev", false, false),
        new("cleanup.windowsupdate", false, false),
        new("network.dns", true, true),
        new("optimization.drives", false, true)
    ];

    public IReadOnlyList<(OneClickItemDefinition Definition, MaintenanceTask Task)> GetItems()
    {
        return Items
            .Select(definition => (Definition: definition, Task: _catalog.GetById(definition.TaskId)))
            .ToList();
    }

    public async Task<OneClickPreview> PreviewAsync(
        IEnumerable<string> selectedTaskIds,
        IReadOnlyList<string>? protectedPaths = null,
        IProgress<OneClickProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var selected = ResolveSelectedItems(selectedTaskIds);
        var previews = new List<OneClickTaskPreview>(selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (definition, task) = selected[index];
            progress?.Report(new OneClickProgress(index + 1, selected.Count, task.Id, IsRunning: false));

            TaskPreview preview;
            if (task.Group is "Cleanup" or "Privacy")
            {
                preview = await _cleanup.PreviewAsync(task, protectedPaths, cancellationToken);
            }
            else
            {
                preview = new TaskPreview(
                    task.Id,
                    task.EstimatedImpact,
                    0,
                    0,
                    [],
                    task.RequiresAdmin ? ["Administrator permission is required."] : [],
                    [])
                {
                    WarningDetails = task.RequiresAdmin
                        ? [new CleanupWarning("adminRequired", "Administrator permission is required.", [])] : []
                };
            }

            previews.Add(new OneClickTaskPreview(task, preview, definition.IsPerformanceAction));
        }

        return new OneClickPreview(previews);
    }

    public async Task<OneClickRunSummary> RunAsync(
        OneClickPreview preview,
        IReadOnlyList<string>? protectedPaths = null,
        IProgress<OneClickProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allowedIds = Items.Select(item => item.TaskId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trustedTasks = preview.Tasks
            .Where(item => allowedIds.Contains(item.Task.Id))
            .Select(item => _catalog.GetById(item.Task.Id))
            .Where(task => task.RiskLevel != RiskLevel.High || task.Id == "cleanup.windowsupdate")
            .DistinctBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var results = new List<TaskRunResult>(trustedTasks.Count);

        for (var index = 0; index < trustedTasks.Count; index++)
        {
            var task = trustedTasks[index];
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new OneClickProgress(index + 1, trustedTasks.Count, task.Id, IsRunning: true));

                if (task.RequiresAdmin && !SystemStatusService.IsAdministrator())
                {
                    var now = DateTimeOffset.Now;
                    results.Add(new TaskRunResult(
                        task.Id,
                        task.Label,
                        now,
                        now,
                        false,
                        0,
                        0,
                        0,
                        [],
                        ["Administrator permission is required."]));
                    continue;
                }

                results.Add(await _execution.RunAsync(task, protectedPaths, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new OneClickRunSummary(results, Cancelled: true);
            }
        }

        return new OneClickRunSummary(results, Cancelled: false);
    }

    private List<(OneClickItemDefinition Definition, MaintenanceTask Task)> ResolveSelectedItems(
        IEnumerable<string> selectedTaskIds)
    {
        var selectedIds = selectedTaskIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Items
            .Where(definition => selectedIds.Contains(definition.TaskId))
            .Select(definition => (Definition: definition, Task: _catalog.GetById(definition.TaskId)))
            .ToList();
    }
}
