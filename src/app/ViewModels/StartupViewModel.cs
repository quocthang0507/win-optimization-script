using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.ViewModels;

public enum StartupStatusFilter
{
    All = 0,
    Enabled = 1,
    Disabled = 2
}

public enum StartupImpactFilter
{
    All = 0,
    High = 1,
    Medium = 2,
    Low = 3
}

public enum StartupSourceFilter
{
    All = 0,
    User = 1,
    Machine = 2,
    StartupFolder = 3
}

public enum StartupSortOrder
{
    Impact = 0,
    Name = 1,
    Source = 2,
    Status = 3
}

public sealed partial class StartupViewModel : ObservableObject
{
    private readonly StartupService _startupService;
    private readonly OperationService _operations;
    private readonly Func<TaskRunResult, Task>? _saveReport;
    private readonly Func<string, string> _t;
    private readonly Func<string, object[], string> _f;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial StartupStatusFilter StatusFilter { get; set; } = StartupStatusFilter.All;

    [ObservableProperty]
    public partial StartupImpactFilter ImpactFilter { get; set; } = StartupImpactFilter.All;

    [ObservableProperty]
    public partial StartupSourceFilter SourceFilter { get; set; } = StartupSourceFilter.All;

    [ObservableProperty]
    public partial StartupSortOrder SortOrder { get; set; } = StartupSortOrder.Impact;

    [ObservableProperty]
    public partial bool ActionableOnly { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private readonly List<StartupEntry> _allEntries = [];
    private readonly ObservableCollection<StartupEntry> _filteredEntries = [];

    public ReadOnlyObservableCollection<StartupEntry> FilteredEntries { get; }

    public StartupViewModel(
        StartupService startupService,
        OperationService operations,
        Func<TaskRunResult, Task>? saveReport = null,
        Func<string, string>? t = null,
        Func<string, object[], string>? f = null)
    {
        _startupService = startupService;
        _operations = operations;
        _saveReport = saveReport;
        _t = t ?? (k => k);
        _f = f ?? ((k, args) => string.Format(k, args));

        FilteredEntries = new ReadOnlyObservableCollection<StartupEntry>(_filteredEntries);
    }

    public IReadOnlyList<StartupEntry> AllEntries => _allEntries;

    public int TotalCount => _allEntries.Count;
    public int FilteredCount => _filteredEntries.Count;
    public int EnabledCount => _allEntries.Count(e => e.Enabled);
    public int DisabledCount => _allEntries.Count(e => !e.Enabled);
    public int HighImpactCount => _allEntries.Count(e => e.Impact == StartupImpactLevel.High);

    public int FilteredEnabledCount => _filteredEntries.Count(e => e.Enabled);
    public int FilteredDisabledCount => _filteredEntries.Count(e => !e.Enabled);
    public int FilteredHighImpactCount => _filteredEntries.Count(e => e.Impact == StartupImpactLevel.High);

    partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();
    partial void OnStatusFilterChanged(StartupStatusFilter value) => ApplyFilterAndSort();
    partial void OnImpactFilterChanged(StartupImpactFilter value) => ApplyFilterAndSort();
    partial void OnSourceFilterChanged(StartupSourceFilter value) => ApplyFilterAndSort();
    partial void OnSortOrderChanged(StartupSortOrder value) => ApplyFilterAndSort();
    partial void OnActionableOnlyChanged(bool value) => ApplyFilterAndSort();

    public void LoadEntries(IEnumerable<StartupEntry> entries, string? error = null)
    {
        _allEntries.Clear();
        ErrorMessage = error;

        foreach (var entry in entries)
        {
            _allEntries.Add(entry);
        }

        ApplyFilterAndSort();
    }

    public void ApplyFilterAndSort()
    {
        var filtered = FilterEntries(_allEntries);
        var sorted = SortEntries(filtered).ToList();

        _filteredEntries.Clear();
        foreach (var item in sorted)
        {
            _filteredEntries.Add(item);
        }

        NotifyCountsChanged();
    }

    public void ResetFilters()
    {
        SearchQuery = string.Empty;
        StatusFilter = StartupStatusFilter.All;
        ImpactFilter = StartupImpactFilter.All;
        SourceFilter = StartupSourceFilter.All;
        SortOrder = StartupSortOrder.Impact;
        ActionableOnly = false;
        ApplyFilterAndSort();
    }

    public IEnumerable<StartupEntry> FilterEntries(IEnumerable<StartupEntry> entries)
    {
        var query = SearchQuery.Trim();

        foreach (var entry in entries)
        {
            if (!MatchesStartupQuery(entry, query))
            {
                continue;
            }

            if (StatusFilter == StartupStatusFilter.Enabled && !entry.Enabled)
            {
                continue;
            }

            if (StatusFilter == StartupStatusFilter.Disabled && entry.Enabled)
            {
                continue;
            }

            if (ImpactFilter == StartupImpactFilter.High && entry.Impact != StartupImpactLevel.High)
            {
                continue;
            }

            if (ImpactFilter == StartupImpactFilter.Medium && entry.Impact != StartupImpactLevel.Medium)
            {
                continue;
            }

            if (ImpactFilter == StartupImpactFilter.Low && entry.Impact != StartupImpactLevel.Low)
            {
                continue;
            }

            if (SourceFilter == StartupSourceFilter.User && !IsUserStartupSource(entry.Source))
            {
                continue;
            }

            if (SourceFilter == StartupSourceFilter.Machine && !IsMachineStartupSource(entry.Source))
            {
                continue;
            }

            if (SourceFilter == StartupSourceFilter.StartupFolder &&
                !entry.Source.Contains("Startup folder", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ActionableOnly && !CanToggleStartupEntry(entry))
            {
                continue;
            }

            yield return entry;
        }
    }

    public IEnumerable<StartupEntry> SortEntries(IEnumerable<StartupEntry> entries)
    {
        return SortOrder switch
        {
            StartupSortOrder.Name => entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase),
            StartupSortOrder.Source => entries
                .OrderBy(e => e.Source, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase),
            StartupSortOrder.Status => entries
                .OrderByDescending(e => e.Enabled)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => entries
                .OrderByDescending(e => e.Impact)
                .ThenByDescending(e => e.Enabled)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    public async Task<bool> ToggleEntryAsync(StartupEntry entry, CancellationToken cancellationToken = default)
    {
        var targetEnable = !entry.Enabled;
        var actionTitle = targetEnable ? _t("startup.enable") : _t("startup.disable");
        var started = DateTimeOffset.Now;

        return await _operations.RunAsync(
            $"{actionTitle}: {entry.Name}",
            OperationCategory.Maintenance,
            async (reporter, token) =>
            {
                reporter.Report(_t("common.loading"));
                var ok = targetEnable
                    ? await _startupService.EnableAsync(entry)
                    : await _startupService.DisableAsync(entry);

                if (_saveReport != null)
                {
                    try
                    {
                        var actionText = targetEnable ? "Enable Startup Item" : "Disable Startup Item";
                        await _saveReport(new TaskRunResult(
                            targetEnable ? "startup.enable" : "startup.disable",
                            actionText,
                            started,
                            DateTimeOffset.Now,
                            ok,
                            0,
                            ok ? 1 : 0,
                            ok ? 0 : 1,
                            ok ? [$"{entry.Name} ({entry.Source}): {(targetEnable ? "enabled" : "disabled")}"] : [],
                            ok ? [] : [$"{entry.Name} ({entry.Source}): operation failed"]));
                    }
                    catch
                    {
                        // Best effort report saving.
                    }
                }

                return ok;
            },
            canCancel: false,
            isExclusive: true,
            callerToken: cancellationToken);
    }

    public static bool MatchesStartupQuery(StartupEntry entry, string query)
    {
        return string.IsNullOrWhiteSpace(query)
               || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.RiskHint.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Recommendation.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUserStartupSource(string source)
    {
        return source.Contains("HKCU", StringComparison.OrdinalIgnoreCase)
               || source.Contains("User Startup", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMachineStartupSource(string source)
    {
        return source.Contains("HKLM", StringComparison.OrdinalIgnoreCase)
               || source.Contains("Common Startup", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanToggleStartupEntry(StartupEntry entry)
    {
        return entry.Enabled ? entry.CanDisable : entry.CanRollback;
    }

    private void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(HighImpactCount));
        OnPropertyChanged(nameof(FilteredEnabledCount));
        OnPropertyChanged(nameof(FilteredDisabledCount));
        OnPropertyChanged(nameof(FilteredHighImpactCount));
    }
}
