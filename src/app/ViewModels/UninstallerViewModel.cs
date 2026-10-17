using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.ViewModels;

public sealed partial class UninstallerViewModel : ObservableObject
{
    private readonly UninstallerService _uninstaller;
    private readonly OperationService _operations;
    private readonly Func<string, string> _t;
    private readonly Func<string, object[], string> _f;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceFilter { get; set; } = "All";

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private readonly List<InstalledApp> _allItems = [];
    private readonly ObservableCollection<InstalledApp> _filteredItems = [];

    public ReadOnlyObservableCollection<InstalledApp> FilteredItems { get; }

    public UninstallerViewModel(
        UninstallerService uninstaller,
        OperationService operations,
        Func<string, string>? t = null,
        Func<string, object[], string>? f = null)
    {
        _uninstaller = uninstaller;
        _operations = operations;
        _t = t ?? (k => k);
        _f = f ?? ((k, args) => string.Format(k, args));
        FilteredItems = new ReadOnlyObservableCollection<InstalledApp>(_filteredItems);
    }

    private string T(string key) => _t(key);
    private string F(string key, params object[] args) => _f(key, args);

    public IReadOnlyList<InstalledApp> AllItems => _allItems;

    public int TotalCount => _allItems.Count;
    public int FilteredCount => _filteredItems.Count;

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSourceFilterChanged(string value)
    {
        ApplyFilter();
    }

    public void LoadApps(IEnumerable<InstalledApp> apps, string? error = null)
    {
        _allItems.Clear();
        ErrorMessage = error;
        _allItems.AddRange(apps);
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var query = SearchQuery.Trim();
        var sourceFilter = SourceFilter;

        var matches = _allItems.Where(app =>
        {
            if (sourceFilter.Equals("Winget", StringComparison.OrdinalIgnoreCase) &&
                !app.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (sourceFilter.Equals("Registry", StringComparison.OrdinalIgnoreCase) &&
                app.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   app.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   app.Version.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

        _filteredItems.Clear();
        foreach (var item in matches)
        {
            _filteredItems.Add(item);
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
    }

    public async Task<int> ScanAppsAsync(CancellationToken cancellationToken = default)
    {
        if (IsWorking)
        {
            return TotalCount;
        }

        IsWorking = true;
        ErrorMessage = null;

        try
        {
            var apps = await _operations.RunAsync(
                T("uninstaller.title"),
                OperationCategory.Software,
                async (reporter, token) =>
                {
                    reporter.Report(T("uninstaller.scanning"));
                    return await _uninstaller.ScanInstalledAppsAsync(token);
                },
                canCancel: true,
                isExclusive: false,
                callerToken: cancellationToken);

            LoadApps(apps);
            return apps.Count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoadApps([], ex.Message);
            return 0;
        }
        finally
        {
            IsWorking = false;
        }
    }

    public async Task<bool> UninstallAppAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (IsWorking)
        {
            return false;
        }

        IsWorking = true;
        try
        {
            var success = await _operations.RunAsync(
                F("uninstaller.uninstalling", app.Name),
                OperationCategory.Software,
                async (reporter, token) =>
                {
                    reporter.Report(F("uninstaller.uninstalling", app.Name));
                    return await _uninstaller.UninstallAppAsync(app, token);
                },
                canCancel: true,
                isExclusive: true,
                callerToken: cancellationToken);

            if (success)
            {
                _allItems.RemoveAll(a => a.Id.Equals(app.Id, StringComparison.OrdinalIgnoreCase));
                ApplyFilter();
            }

            return success;
        }
        finally
        {
            IsWorking = false;
        }
    }

    public async Task<IReadOnlyList<LeftoverCleanupCandidate>> ScanLeftoversAsync(
        InstalledApp app,
        CancellationToken cancellationToken = default)
    {
        return await _uninstaller.ScanLeftoversAsync(app, cancellationToken);
    }

    public async Task<bool> DeleteLeftoversAsync(
        List<string> paths,
        CancellationToken cancellationToken = default)
    {
        return await _uninstaller.DeleteLeftoversAsync(paths, cancellationToken);
    }
}
