using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.ViewModels;

public sealed partial class BloatwareItemViewModel : ObservableObject
{
    public InstalledApp App { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public BloatwareItemViewModel(InstalledApp app, bool isSelected = false)
    {
        App = app;
        IsSelected = isSelected;
    }

    public string Id => App.Id;
    public string Name => App.Name;
    public string Publisher => App.Publisher;
    public string Version => App.Version;
    public string Details => string.IsNullOrWhiteSpace(App.Publisher)
        ? App.Version
        : $"{App.Publisher} / {App.Version}";
}

public sealed partial class BloatwareViewModel : ObservableObject
{
    private readonly UninstallerService _uninstaller;
    private readonly OperationService _operations;
    private readonly Func<TaskRunResult, Task>? _saveReport;
    private readonly Func<string, string> _t;
    private readonly Func<string, object[], string> _f;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private readonly List<BloatwareItemViewModel> _allItems = [];
    private readonly ObservableCollection<BloatwareItemViewModel> _filteredItems = [];

    public ReadOnlyObservableCollection<BloatwareItemViewModel> FilteredItems { get; }

    public BloatwareViewModel(
        UninstallerService uninstaller,
        OperationService operations,
        Func<TaskRunResult, Task>? saveReport = null,
        Func<string, string>? t = null,
        Func<string, object[], string>? f = null)
    {
        _uninstaller = uninstaller;
        _operations = operations;
        _saveReport = saveReport;
        _t = t ?? (k => k);
        _f = f ?? ((k, args) => string.Format(k, args));

        FilteredItems = new ReadOnlyObservableCollection<BloatwareItemViewModel>(_filteredItems);
    }

    private string T(string key) => _t(key);
    private string F(string key, params object[] args) => _f(key, args);

    public IReadOnlyList<BloatwareItemViewModel> AllItems => _allItems;

    public int TotalCount => _allItems.Count;
    public int FilteredCount => _filteredItems.Count;
    public int SelectedCount => _allItems.Count(i => i.IsSelected);
    public bool CanRemove => SelectedCount > 0 && !IsWorking;
    public bool CanClearSelection => SelectedCount > 0 && !IsWorking;
    public bool CanSelectAll => _filteredItems.Count > 0 && !IsWorking;

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnIsWorkingChanged(bool value)
    {
        NotifyStateChanged();
    }

    public void LoadApps(IEnumerable<InstalledApp> apps, string? error = null)
    {
        _allItems.Clear();
        ErrorMessage = error;

        foreach (var app in apps)
        {
            var item = new BloatwareItemViewModel(app);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BloatwareItemViewModel.IsSelected))
                {
                    NotifyStateChanged();
                }
            };
            _allItems.Add(item);
        }

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var query = SearchQuery.Trim();
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : _allItems.Where(i =>
                i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _filteredItems.Clear();
        foreach (var item in matches)
        {
            _filteredItems.Add(item);
        }

        NotifyStateChanged();
    }

    public void SelectAllFiltered()
    {
        foreach (var item in _filteredItems)
        {
            item.IsSelected = true;
        }

        NotifyStateChanged();
    }

    public void ClearSelection()
    {
        foreach (var item in _allItems)
        {
            item.IsSelected = false;
        }

        NotifyStateChanged();
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
                T("bloatware.title"),
                OperationCategory.Software,
                async (reporter, token) =>
                {
                    reporter.Report(T("bloatware.scanning"));
                    var scanned = await _uninstaller.ScanAppxPackagesAsync(token);
                    return scanned.ToList();
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
            ErrorMessage = ex.Message;
            LoadApps([], ex.Message);
            return 0;
        }
        finally
        {
            IsWorking = false;
        }
    }

    public async Task<(int Succeeded, int Failed)> RemoveSelectedAppsAsync(CancellationToken cancellationToken = default)
    {
        var selected = _allItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || IsWorking)
        {
            return (0, 0);
        }

        IsWorking = true;
        var started = DateTimeOffset.Now;
        var succeeded = 0;
        var failed = 0;
        var reportMessages = new List<string>();
        var reportErrors = new List<string>();

        try
        {
            return await _operations.RunAsync(
                T("bloatware.removeSelected"),
                OperationCategory.Software,
                async (reporter, token) =>
                {
                    for (var i = 0; i < selected.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var item = selected[i];
                        var percent = (double)i / selected.Count * 100.0;
                        reporter.Report(percent, F("bloatware.removing", i + 1, selected.Count, item.Name));

                        try
                        {
                            var ok = await _uninstaller.RemoveAppxPackageAsync(item.App);
                            if (ok)
                            {
                                succeeded++;
                                reportMessages.Add($"{item.Name} ({item.Id}): removed");
                            }
                            else
                            {
                                failed++;
                                reportErrors.Add($"{item.Name} ({item.Id}): Windows rejected removal");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            reportErrors.Add($"{item.Name} ({item.Id}): {ex.Message}");
                        }
                    }

                    reporter.Report(100.0, F("bloatware.summary", succeeded, failed));
                    return (succeeded, failed);
                },
                canCancel: true,
                isExclusive: true,
                callerToken: cancellationToken);
        }
        finally
        {
            if (_saveReport != null)
            {
                try
                {
                    await _saveReport(new TaskRunResult(
                        "software.appx.remove",
                        "Windows App Removal",
                        started,
                        DateTimeOffset.Now,
                        failed == 0,
                        0,
                        succeeded,
                        failed,
                        reportMessages,
                        reportErrors));
                }
                catch
                {
                    // Report save failure should not break app flow.
                }
            }

            IsWorking = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanClearSelection));
        OnPropertyChanged(nameof(CanSelectAll));
    }
}
