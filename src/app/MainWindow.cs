using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinRT.Interop;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace WinOptimizationApp;

public sealed class MainWindow : Window
{
    private readonly PathService _paths = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly CommandRunner _commands = new();
    private readonly MaintenanceCatalog _catalog = new();
    private readonly AppSettings _settings;
    private readonly ReportService _reports;
    private readonly CleanupService _cleanup;
    private readonly SystemStatusService _status;
    private readonly WingetService _winget;
    private readonly StartupService _startup = new();
    private readonly LocalizationService _localization;
    private readonly MaintenanceExecutionService _execution;
    private readonly DiskAnalysisService _diskAnalysis = new();
    private readonly StorageCleanupService _storageCleanup;

    private readonly NavigationView _navigation;
    private readonly Dictionary<string, NavigationViewItem> _navItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _page;
    private readonly TextBlock _statusText;
    private CancellationTokenSource? _diskScanCts;
    private DiskScanResult? _lastDiskScan;
    private string _currentPageTag = "dashboard";

    public MainWindow()
    {
        _settings = _settingsService.Load();
        _localization = new LocalizationService(_settings.Language);
        _reports = new ReportService(_paths);
        _cleanup = new CleanupService(_commands);
        _status = new SystemStatusService(_commands, _reports);
        _winget = new WingetService(_commands);
        _execution = new MaintenanceExecutionService(_cleanup, _commands, _paths, _reports, new RestorePointService(_commands));
        _storageCleanup = new StorageCleanupService(_reports);

        Title = T("app.title");

        _page = new StackPanel { Spacing = 16, Padding = new Thickness(28, 22, 28, 28) };
        _scrollViewer = new ScrollViewer { Content = _page };
        _statusText = new TextBlock { Text = T("common.ready"), Opacity = 0.7, Margin = new Thickness(12, 0, 16, 0) };

        _navigation = new NavigationView
        {
            PaneTitle = T("app.paneTitle"),
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            Content = _scrollViewer,
            PaneFooter = _statusText
        };
        ApplyTheme(_settings.Theme ?? AppTheme.System);
        ApplyWinUiStyle(_settings.WinUiStyle ?? AppWinUiStyle.Default);

        AddNavItem("dashboard", "nav.dashboard", Symbol.Home);
        AddNavItem("cleanup", "nav.cleanup", Symbol.Delete);
        AddNavItem("storage", "nav.storage", Symbol.View);
        AddNavItem("startup", "nav.startup", Symbol.List);
        AddNavItem("updates", "nav.updates", Symbol.Download);
        AddNavItem("repair", "nav.repair", Symbol.Refresh);
        AddNavItem("history", "nav.history", Symbol.Document);
        AddNavItem("settings", "nav.settings", Symbol.Setting);

        _navigation.SelectionChanged += (sender, args) =>
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                var ignored = NavigateAsync(tag);
            }
        };

        Content = _navigation;
        _navigation.SelectedItem = _navigation.MenuItems[0];
    }

    private async Task NavigateAsync(string tag)
    {
        _currentPageTag = tag;
        _page.Children.Clear();
        SetStatus(T("common.loading"));

        switch (tag)
        {
            case "dashboard":
                await RenderDashboardAsync();
                break;
            case "cleanup":
                RenderTaskPage(T("nav.cleanup"), "Cleanup", includePrivacy: true);
                break;
            case "storage":
                RenderStorageAnalyzerPage();
                break;
            case "startup":
                RenderStartupPage();
                break;
            case "updates":
                RenderUpdatesPage();
                break;
            case "repair":
                RenderTaskPage(T("nav.repair"), "Repair", includeOptimization: true);
                break;
            case "history":
                RenderHistoryPage();
                break;
            case "settings":
                RenderSettingsPage();
                break;
        }

        SetStatus(T("common.ready"));
    }

    private async Task RenderDashboardAsync()
    {
        AddHeader(T("dashboard.title"), T("dashboard.subtitle"));
        var status = await _status.GetAsync();

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        AddMetric(grid, 0, 0, T("dashboard.windows"), status.WindowsVersion, status.PendingReboot ? T("dashboard.pendingReboot") : T("dashboard.noRebootPending"), status.PendingReboot ? Colors.OrangeRed : Colors.SeaGreen);
        AddMetric(grid, 0, 1, T("dashboard.administrator"), status.IsAdministrator ? T("dashboard.elevated") : T("dashboard.standardUser"), status.IsAdministrator ? T("dashboard.highRiskEnabled") : T("dashboard.highRiskNeedAdmin"), status.IsAdministrator ? Colors.SeaGreen : Colors.DarkOrange);
        AddMetric(grid, 1, 0, T("dashboard.systemDrive"), $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} {T("dashboard.free")}", $"{status.SystemDrive} of {Formatters.FormatBytes(status.SystemDriveTotalBytes)}", Colors.SteelBlue);
        AddMetric(grid, 1, 1, T("dashboard.uptime"), Formatters.FormatDuration(status.Uptime), status.WingetAvailable ? T("dashboard.wingetAvailable") : T("dashboard.wingetNotFound"), status.WingetAvailable ? Colors.SeaGreen : Colors.Gray);
        _page.Children.Add(grid);

        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        quick.Children.Add(ActionButton(T("dashboard.scanCleanup"), Symbol.Find, async (_, _) => await NavigateAsync("cleanup")));
        quick.Children.Add(ActionButton(T("dashboard.analyzeStorage"), Symbol.View, async (_, _) => await NavigateAsync("storage")));
        quick.Children.Add(ActionButton(T("dashboard.scanUpdates"), Symbol.Download, async (_, _) => await ScanWingetAsync()));
        quick.Children.Add(ActionButton(T("dashboard.openLogs"), Symbol.OpenFile, (_, _) => OpenFolder(_paths.LogsDirectory)));
        _page.Children.Add(quick);

        if (!string.IsNullOrWhiteSpace(status.LastReportPath))
        {
            _page.Children.Add(Card(T("dashboard.lastReport"), status.LastReportPath, T("common.open"), (_, _) => OpenFile(status.LastReportPath)));
        }
    }

    private void RenderTaskPage(string title, string group, bool includePrivacy = false, bool includeOptimization = false)
    {
        AddHeader(title, T("taskPage.subtitle"));

        var groups = new List<string> { group };
        if (includePrivacy)
        {
            groups.Add("Privacy");
        }

        if (includeOptimization)
        {
            groups.Add("Optimization");
        }

        foreach (var groupName in groups)
        {
            _page.Children.Add(SectionTitle(_localization.GroupName(groupName)));
            foreach (var task in _catalog.ByGroup(groupName))
            {
                AddTaskRow(task);
            }
        }
    }

    private void RenderStartupPage()
    {
        AddHeader(T("startup.title"), T("startup.subtitle"));
        var resultPanel = new StackPanel { Spacing = 8 };
        var scanButton = ActionButton(T("startup.scan"), Symbol.Find, async (_, _) =>
        {
            SetStatus(T("startup.scanning"));
            resultPanel.Children.Clear();
            var entries = await _startup.ScanAsync();
            resultPanel.Children.Add(SectionTitle(F("startup.entries", entries.Count)));
            foreach (var entry in entries)
            {
                resultPanel.Children.Add(StartupRow(entry));
            }
            SetStatus(T("common.ready"));
        });

        _page.Children.Add(scanButton);
        _page.Children.Add(resultPanel);
    }

    private void RenderUpdatesPage()
    {
        AddHeader(T("updates.title"), T("updates.subtitle"));
        var resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(ActionButton(T("updates.scanWinget"), Symbol.Find, async (_, _) =>
        {
            await ScanWingetAsync(resultPanel);
        }));
        actions.Children.Add(ActionButton(T("updates.upgradeAll"), Symbol.Download, async (_, _) =>
        {
            var task = _catalog.GetById("software.winget");
            await RunTaskAsync(task);
        }));
        _page.Children.Add(actions);
        _page.Children.Add(resultPanel);
    }

    private void RenderStorageAnalyzerPage()
    {
        AddHeader(T("storage.title"), T("storage.subtitle"));

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var rootBox = new TextBox
        {
            Header = T("storage.driveOrFolder"),
            Text = _lastDiskScan?.Root.FullPath ?? systemRoot,
            MinWidth = 360,
            PlaceholderText = T("storage.placeholder")
        };

        var driveBox = new ComboBox { Header = T("storage.drive"), MinWidth = 180 };
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
        {
            driveBox.Items.Add($"{drive.Name}  {Formatters.FormatBytes(drive.AvailableFreeSpace)} {T("storage.free")}");
        }

        driveBox.SelectionChanged += (_, _) =>
        {
            if (driveBox.SelectedItem is string selected && selected.Length >= 3)
            {
                rootBox.Text = selected[..3];
            }
        };

        var includeHidden = new CheckBox { Content = T("common.hidden"), VerticalAlignment = VerticalAlignment.Bottom };
        var includeSystem = new CheckBox { Content = T("common.system"), VerticalAlignment = VerticalAlignment.Bottom };
        var followLinks = new CheckBox { Content = T("common.followLinks"), VerticalAlignment = VerticalAlignment.Bottom };
        ToolTipService.SetToolTip(followLinks, T("storage.followTooltip"));

        var resultPanel = new StackPanel { Spacing = 14 };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 5, Visibility = Visibility.Collapsed };
        var progressText = new TextBlock { Opacity = 0.72, TextWrapping = TextWrapping.Wrap };

        Button? scanButton = null;
        Button? stopButton = null;

        scanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await StartDiskScanAsync(
                rootBox.Text,
                includeHidden.IsChecked == true,
                includeSystem.IsChecked == true,
                followLinks.IsChecked == true,
                progress,
                progressText,
                resultPanel);
        });

        stopButton = ActionButton(T("common.stop"), Symbol.Stop, (_, _) =>
        {
            _diskScanCts?.Cancel();
            SetStatus(T("storage.stopping"));
        });
        stopButton.IsEnabled = false;

        var browseButton = ActionButton(T("common.browse"), Symbol.OpenFile, async (_, _) =>
        {
            var folder = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                rootBox.Text = folder;
            }
        });

        var commandGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition());
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(rootBox, 0);
        commandGrid.Children.Add(rootBox);
        Grid.SetColumn(driveBox, 1);
        commandGrid.Children.Add(driveBox);
        Grid.SetColumn(includeHidden, 2);
        commandGrid.Children.Add(includeHidden);
        Grid.SetColumn(includeSystem, 3);
        commandGrid.Children.Add(includeSystem);
        Grid.SetColumn(followLinks, 4);
        commandGrid.Children.Add(followLinks);
        Grid.SetColumn(browseButton, 5);
        commandGrid.Children.Add(browseButton);
        Grid.SetColumn(scanButton, 6);
        commandGrid.Children.Add(scanButton);

        var stopRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        stopRow.Children.Add(stopButton);
        stopRow.Children.Add(progressText);

        _page.Children.Add(commandGrid);
        _page.Children.Add(progress);
        _page.Children.Add(stopRow);
        _page.Children.Add(resultPanel);

        if (_lastDiskScan is not null)
        {
            RenderStorageResults(resultPanel, _lastDiskScan);
        }

        async Task StartDiskScanAsync(
            string rootPath,
            bool withHidden,
            bool withSystem,
            bool withLinks,
            ProgressBar progressBar,
            TextBlock statusBlock,
            StackPanel output)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                await ShowDialogAsync(T("storage.title"), InfoBlock(T("storage.enterPath")), T("common.close"));
                return;
            }

            if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
            {
                await ShowDialogAsync(T("storage.pathNotFound"), InfoBlock(rootPath), T("common.close"));
                return;
            }

            _diskScanCts?.Cancel();
            _diskScanCts = new CancellationTokenSource();
            scanButton!.IsEnabled = false;
            stopButton!.IsEnabled = true;
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;
            output.Children.Clear();
            SetStatus(T("storage.scanning"));

            var options = new DiskScanOptions(rootPath, withHidden, withSystem, withLinks);
            var scanProgress = new Progress<DiskScanProgress>(value =>
            {
                statusBlock.Text = F("storage.progress", Formatters.FormatBytes(value.TotalBytes), value.FileCount, value.FolderCount, value.SkippedCount, value.CurrentPath);
            });

            try
            {
                var result = await _diskAnalysis.ScanAsync(options, scanProgress, _diskScanCts.Token);
                _lastDiskScan = result;
                RenderStorageResults(output, result);
                statusBlock.Text = F("storage.completedIn", (result.FinishedAt - result.StartedAt).TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                statusBlock.Text = T("storage.scanCanceled");
                output.Children.Add(InfoBlock(T("storage.scanCanceledDetail")));
            }
            catch (Exception ex)
            {
                statusBlock.Text = ex.Message;
                output.Children.Add(InfoBlock(F("storage.scanFailed", ex.Message)));
            }
            finally
            {
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                scanButton!.IsEnabled = true;
                stopButton!.IsEnabled = false;
                SetStatus(T("common.ready"));
            }
        }
    }

    private void RenderHistoryPage()
    {
        AddHeader(T("history.title"), T("history.subtitle"));

        if (!Directory.Exists(_paths.LogsDirectory))
        {
            _page.Children.Add(InfoBlock(T("history.empty")));
            return;
        }

        foreach (var report in Directory.GetFiles(_paths.LogsDirectory, "maintenance-*.json").OrderByDescending(File.GetLastWriteTime).Take(30))
        {
            _page.Children.Add(Card(Path.GetFileName(report), report, T("common.open"), (_, _) => OpenFile(report)));
        }
    }

    private void RenderSettingsPage()
    {
        AddHeader(T("settings.title"), T("settings.subtitle"));
        _page.Children.Add(LanguageCard());
        _page.Children.Add(ThemeCard());
        _page.Children.Add(WinUiStyleCard());
        _page.Children.Add(Card(T("settings.cliScript"), _paths.CliScriptPath, T("common.launch"), async (_, _) => await RunTaskAsync(_catalog.GetById("cli.launch"))));
        _page.Children.Add(Card(T("settings.storageSense"), T("settings.storageSenseDescription"), T("common.open"), async (_, _) => await RunTaskAsync(_catalog.GetById("settings.storage"))));
        _page.Children.Add(Card(T("settings.logs"), _paths.LogsDirectory, T("common.open"), (_, _) => OpenFolder(_paths.LogsDirectory)));
        _page.Children.Add(Card(T("settings.repository"), _paths.RepositoryRoot, T("common.open"), (_, _) => OpenFolder(_paths.RepositoryRoot)));
    }

    private async Task ScanWingetAsync(StackPanel? resultPanel = null)
    {
        resultPanel ??= new StackPanel { Spacing = 8 };
        if (!_page.Children.Contains(resultPanel))
        {
            _page.Children.Add(resultPanel);
        }

        SetStatus(T("updates.scanning"));
        resultPanel.Children.Clear();
        var packages = await _winget.ScanAsync();
        resultPanel.Children.Add(SectionTitle(F("updates.packageUpdates", packages.Count)));
        foreach (var package in packages)
        {
            resultPanel.Children.Add(PackageRow(package));
        }
        SetStatus(T("common.ready"));
    }

    private void RenderStorageResults(StackPanel resultPanel, DiskScanResult result)
    {
        resultPanel.Children.Clear();

        var summary = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.RowDefinitions.Add(new RowDefinition());
        var largestFolder = _diskAnalysis.GetLargestDirectories(result, 1).FirstOrDefault();
        AddMetric(summary, 0, 0, T("storage.scanned"), Formatters.FormatBytes(result.TotalBytes), F("storage.filesFolders", result.FileCount, result.FolderCount), Colors.SteelBlue);
        AddMetric(summary, 0, 1, T("storage.largestFolder"), largestFolder is null ? T("common.none") : Formatters.FormatBytes(largestFolder.Size), largestFolder?.FullPath ?? result.Root.FullPath, Colors.DarkCyan);
        AddMetric(summary, 0, 2, T("storage.skipped"), $"{result.SkippedCount:N0}", F("storage.errors", result.Errors.Count), result.Errors.Count > 0 ? Colors.DarkOrange : Colors.SeaGreen);
        resultPanel.Children.Add(summary);

        var candidatePanel = new StackPanel { Spacing = 8 };
        var candidates = _storageCleanup.CreateCandidates(result);
        if (candidates.Count > 0)
        {
            var selected = new List<(CheckBox Box, StorageCleanupCandidate Candidate)>();
            var candidateHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            candidateHeader.Children.Add(SectionTitle(T("storage.cleanupReview")));
            candidateHeader.Children.Add(ActionButton(T("storage.reviewSelected"), Symbol.Delete, async (_, _) =>
            {
                var picked = selected.Where(item => item.Box.IsChecked == true).Select(item => item.Candidate).ToList();
                await ReviewStorageCandidatesAsync(picked);
            }));
            candidatePanel.Children.Add(candidateHeader);

            foreach (var candidate in candidates.Take(12))
            {
                var checkBox = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                selected.Add((checkBox, candidate));
                candidatePanel.Children.Add(StorageCandidateRow(candidate, checkBox));
            }

            resultPanel.Children.Add(candidatePanel);
        }

        resultPanel.Children.Add(SectionTitle(T("storage.spaceMap")));
        foreach (var directory in _diskAnalysis.GetLargestDirectories(result, 12))
        {
            resultPanel.Children.Add(StorageBarRow(directory, result.TotalBytes));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.folderTree")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.name"), T("storage.size"), T("storage.files"), T("storage.modified"), T("storage.action")));
        foreach (var item in _diskAnalysis.FlattenVisibleTree(result.Root, 120))
        {
            resultPanel.Children.Add(StorageDiskItemRow(item, result.Root));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.largestFiles")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.name"), T("storage.size"), T("storage.type"), T("storage.modified"), T("storage.action")));
        foreach (var file in result.LargestFiles.Take(80))
        {
            resultPanel.Children.Add(StorageFileRow(file));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.fileTypes")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.extension"), T("storage.size"), T("storage.count"), T("storage.lastModified"), T("storage.largest")));
        foreach (var type in result.FileTypes.Take(40))
        {
            resultPanel.Children.Add(StorageFileTypeRow(type));
        }

        if (result.Errors.Count > 0)
        {
            resultPanel.Children.Add(SectionTitle(T("storage.skippedErrors")));
            foreach (var error in result.Errors.Take(20))
            {
                resultPanel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            }
        }
    }

    private static FrameworkElement StorageHeaderRow(string first, string second, string third, string fourth, string fifth)
    {
        var row = StorageGrid();
        row.Children.Add(HeaderText(first, 0));
        row.Children.Add(HeaderText(second, 1));
        row.Children.Add(HeaderText(third, 2));
        row.Children.Add(HeaderText(fourth, 3));
        row.Children.Add(HeaderText(fifth, 4));
        return row;

        static TextBlock HeaderText(string text, int column)
        {
            var block = new TextBlock { Text = text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = 0.75 };
            Grid.SetColumn(block, column);
            return block;
        }
    }

    private FrameworkElement StorageDiskItemRow(DiskItem item, DiskItem root)
    {
        var row = StorageGrid();
        row.Children.Add(CellText($"{(item.IsDirectory ? "[D]" : "[F]")} {item.Name}", 0, item.FullPath));
        row.Children.Add(CellText(Formatters.FormatBytes(item.Size), 1));
        row.Children.Add(CellText(item.FileCount.ToString("N0"), 2));
        row.Children.Add(CellText(FormatStorageDate(item.LastModified), 3));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("common.openLocation"), (_, _) => OpenContainingFolder(item.FullPath)));
        if (item.FullPath != root.FullPath)
        {
            actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
            {
                await ReviewStorageCandidatesAsync([CreateManualCandidate(item)]);
            }));
        }
        Grid.SetColumn(actions, 4);
        row.Children.Add(actions);
        return row;
    }

    private FrameworkElement StorageFileRow(DiskItem file)
    {
        var row = StorageGrid();
        row.Children.Add(CellText(file.Name, 0, file.FullPath));
        row.Children.Add(CellText(Formatters.FormatBytes(file.Size), 1));
        row.Children.Add(CellText(file.Extension, 2));
        row.Children.Add(CellText(FormatStorageDate(file.LastModified), 3));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("common.openLocation"), (_, _) => OpenContainingFolder(file.FullPath)));
        actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
        {
            await ReviewStorageCandidatesAsync([CreateManualCandidate(file)]);
        }));
        Grid.SetColumn(actions, 4);
        row.Children.Add(actions);
        return row;
    }

    private static FrameworkElement StorageFileTypeRow(FileTypeSummary summary)
    {
        var row = StorageGrid();
        row.Children.Add(CellText(summary.Extension, 0));
        row.Children.Add(CellText(Formatters.FormatBytes(summary.TotalBytes), 1));
        row.Children.Add(CellText(summary.Count.ToString("N0"), 2));
        row.Children.Add(CellText(FormatStorageDate(summary.LastModified), 3));
        row.Children.Add(CellText(summary.LargestItemPath, 4));
        return row;
    }

    private static FrameworkElement StorageBarRow(DiskItem item, long totalBytes)
    {
        var percent = totalBytes > 0 ? Math.Max(2, Math.Min(100, item.Size * 100d / totalBytes)) : 0;
        var wrapper = new StackPanel { Spacing = 4 };
        wrapper.Children.Add(new TextBlock
        {
            Text = $"{item.Name}  {Formatters.FormatBytes(item.Size)}  ({percent:N1}%)",
            TextWrapping = TextWrapping.Wrap
        });

        var bar = new Grid { Height = 18 };
        bar.Children.Add(new Rectangle
        {
            Fill = Brush(Color.FromArgb(28, 128, 128, 128)),
            RadiusX = 3,
            RadiusY = 3
        });
        bar.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = percent,
            Height = 18,
            IsHitTestVisible = false
        });
        wrapper.Children.Add(bar);
        return wrapper;
    }

    private FrameworkElement StorageCandidateRow(StorageCleanupCandidate candidate, CheckBox checkBox)
    {
        var row = StorageGrid();
        Grid.SetColumn(checkBox, 0);
        row.Children.Add(checkBox);
        row.Children.Add(CellText(candidate.Label, 0, candidate.SourcePath, new Thickness(34, 0, 0, 0)));
        row.Children.Add(CellText(Formatters.FormatBytes(candidate.EstimatedBytes), 1));
        var risk = RiskBadge(candidate.RiskLevel, _localization.RiskName(candidate.RiskLevel));
        Grid.SetColumn(risk, 2);
        row.Children.Add(risk);
        row.Children.Add(CellText(candidate.Reason, 3));
        row.Children.Add(CellText(candidate.CleanupMode == StorageCleanupMode.MoveToRecycleBin ? T("common.recycleBin") : T("common.delete"), 4));
        return row;
    }

    private async Task ReviewStorageCandidatesAsync(IReadOnlyList<StorageCleanupCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            await ShowDialogAsync(T("storage.cleanupReview"), InfoBlock(T("storage.selectAtLeastOne")), T("common.close"));
            return;
        }

        var panel = new StackPanel { Spacing = 10, MaxWidth = 760 };
        panel.Children.Add(new TextBlock
        {
            Text = F("storage.itemSummary", candidates.Count, Formatters.FormatBytes(candidates.Sum(candidate => candidate.EstimatedBytes))),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        foreach (var candidate in candidates.Take(16))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"{candidate.Label} / {Formatters.FormatBytes(candidate.EstimatedBytes)} / {candidate.RiskLevel}\n{candidate.SourcePath}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.82
            });
        }

        if (candidates.Count > 16)
        {
            panel.Children.Add(new TextBlock { Text = F("storage.moreItems", candidates.Count - 16), Opacity = 0.65 });
        }

        var dialog = new ContentDialog
        {
            Title = T("storage.moveQuestion"),
            Content = panel,
            PrimaryButtonText = T("common.move"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _navigation.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        SetStatus(T("storage.cleaning"));
        var result = await _storageCleanup.CleanupAsync(candidates);
        await ShowRunResultAsync(result);
        SetStatus(T("common.ready"));
    }

    private StorageCleanupCandidate CreateManualCandidate(DiskItem item)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var isUserFile = item.FullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
        var risk = item.IsDirectory || !isUserFile ? RiskLevel.High : RiskLevel.Medium;

        return new StorageCleanupCandidate(
            $"manual:{item.FullPath}",
            item.Name,
            item.FullPath,
            item.Size,
            risk,
            StorageCleanupMode.MoveToRecycleBin,
            T("storage.manualReason"),
            item.IsDirectory);
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static Grid StorageGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 7, 8, 7),
            Background = Brush(Color.FromArgb(10, 128, 128, 128))
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        return grid;
    }

    private static TextBlock CellText(string text, int column, string? tooltip = null, Thickness? margin = null)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.86,
            Margin = margin ?? new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            ToolTipService.SetToolTip(block, tooltip);
        }

        Grid.SetColumn(block, column);
        return block;
    }

    private static string FormatStorageDate(DateTimeOffset value)
    {
        return value <= DateTimeOffset.MinValue.AddDays(1) ? "-" : value.LocalDateTime.ToString("yyyy-MM-dd");
    }

    private void AddTaskRow(MaintenanceTask task)
    {
        var row = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(24, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = TaskLabel(task), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = TaskDescription(task), TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        text.Children.Add(new TextBlock { Text = TaskImpact(task), TextWrapping = TextWrapping.Wrap, Opacity = 0.65 });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var risk = RiskBadge(task.RiskLevel, _localization.RiskName(task.RiskLevel));
        Grid.SetColumn(risk, 1);
        grid.Children.Add(risk);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (task.CanPreview)
        {
            actions.Children.Add(IconButton(Symbol.Find, "Scan", async (_, _) => await PreviewTaskAsync(task)));
        }
        actions.Children.Add(IconButton(Symbol.Play, "Run", async (_, _) => await RunTaskAsync(task)));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        row.Child = grid;
        _page.Children.Add(row);
    }

    private async Task PreviewTaskAsync(MaintenanceTask task)
    {
        SetStatus(F("status.scanningTask", TaskLabel(task)));
        var preview = await _cleanup.PreviewAsync(task);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = preview.Summary, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        foreach (var warning in preview.Warnings)
        {
            panel.Children.Add(new TextBlock { Text = warning, Foreground = Brush(Colors.DarkOrange), TextWrapping = TextWrapping.Wrap });
        }

        foreach (var command in preview.PlannedCommands)
        {
            panel.Children.Add(new TextBlock { Text = command, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"), TextWrapping = TextWrapping.Wrap });
        }

        foreach (var target in preview.Targets.Take(20))
        {
            panel.Children.Add(new TextBlock
            {
                Text = F("preview.targetLine", target.Name, Formatters.FormatBytes(target.Bytes), target.FileCount, target.Status),
                TextWrapping = TextWrapping.Wrap,
                Opacity = target.Exists ? 0.9 : 0.55
            });
        }

        if (preview.Targets.Count > 20)
        {
            panel.Children.Add(new TextBlock { Text = F("preview.moreTargets", preview.Targets.Count - 20), Opacity = 0.65 });
        }

        await ShowDialogAsync(F("preview.title", TaskLabel(task)), panel, T("common.close"));
        SetStatus(T("common.ready"));
    }

    private async Task RunTaskAsync(MaintenanceTask task)
    {
        if (task.RequiresAdmin && !SystemStatusService.IsAdministrator())
        {
            await ShowDialogAsync(T("admin.title"), new TextBlock
            {
                Text = F("admin.message", TaskLabel(task)),
                TextWrapping = TextWrapping.Wrap
            }, T("common.close"));
            return;
        }

        if (task.RequiresConfirmation || task.RiskLevel == RiskLevel.High)
        {
            var confirmed = await ConfirmAsync(task);
            if (!confirmed)
            {
                return;
            }
        }

        SetStatus(F("status.runningTask", TaskLabel(task)));
        var result = await _execution.RunAsync(task);
        await ShowRunResultAsync(result);
        SetStatus(T("common.ready"));
    }

    private async Task<bool> ConfirmAsync(MaintenanceTask task)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = TaskDescription(task), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = F("confirm.risk", _localization.RiskName(task.RiskLevel)), Foreground = RiskBrush(task.RiskLevel) });
        panel.Children.Add(new TextBlock { Text = TaskImpact(task), TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        if (task.CanRollback)
        {
            panel.Children.Add(new TextBlock { Text = T("confirm.restorePoint"), TextWrapping = TextWrapping.Wrap });
        }

        var dialog = new ContentDialog
        {
            Title = F("confirm.runQuestion", TaskLabel(task)),
            Content = panel,
            PrimaryButtonText = T("common.run"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _navigation.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowRunResultAsync(TaskRunResult result)
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 720 };
        panel.Children.Add(new TextBlock { Text = result.Success ? T("run.completed") : T("run.completedWarnings"), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = F("run.summary", Formatters.FormatBytes(result.FreedBytes), result.FilesRemoved, result.FilesSkipped), TextWrapping = TextWrapping.Wrap });

        foreach (var message in result.Messages.Where(message => !string.IsNullOrWhiteSpace(message)).Take(8))
        {
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        }

        foreach (var error in result.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).Take(8))
        {
            panel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Foreground = Brush(Colors.IndianRed) });
        }

        await ShowDialogAsync(LocalizeTaskLabel(result.TaskId, result.TaskLabel), panel, T("common.close"));
    }

    private void AddHeader(string title, string subtitle)
    {
        _page.Children.Add(new TextBlock { Text = title, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _page.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap, Opacity = 0.72, Margin = new Thickness(0, -8, 0, 4) });
    }

    private static TextBlock SectionTitle(string text)
    {
        return new TextBlock { Text = text, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
    }

    private static TextBlock InfoBlock(string text)
    {
        return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
    }

    private void AddMetric(Grid grid, int row, int column, string title, string value, string detail, Color color)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(22, color.R, color.G, color.B))
        };

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = title, Opacity = 0.7 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = detail, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap });
        card.Child = stack;

        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static Border Card(string title, string body, string buttonText, RoutedEventHandler action)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(18, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var button = new Button { Content = buttonText, MinWidth = 86 };
        button.Click += action;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        border.Child = grid;
        return border;
    }

    private FrameworkElement StartupRow(StartupEntry entry)
    {
        return Card(entry.Name, $"{entry.Source} / {(entry.Enabled ? T("startup.enabled") : T("startup.disabled"))}\n{entry.Command}\n{entry.RiskHint}", T("common.open"), (_, _) => OpenContainingFolder(entry.Command));
    }

    private FrameworkElement PackageRow(WingetPackage package)
    {
        return Card(package.Name, $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}", T("common.open"), (_, _) => { });
    }

    private FrameworkElement LanguageCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(18, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = T("settings.language"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.languageDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var combo = new ComboBox { MinWidth = 170 };
        combo.Items.Add(new ComboBoxItem { Content = "English", Tag = AppLanguage.English });
        combo.Items.Add(new ComboBoxItem { Content = "Tiếng Việt", Tag = AppLanguage.Vietnamese });
        combo.SelectedIndex = _localization.CurrentLanguage == AppLanguage.Vietnamese ? 1 : 0;
        combo.SelectionChanged += async (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppLanguage language && language != _localization.CurrentLanguage)
            {
                _localization.CurrentLanguage = language;
                _settings.Language = language;
                var saved = _settingsService.Save(_settings);
                RefreshShellText();
                await NavigateAsync(_currentPageTag);
                SetStatus(saved ? T("settings.saved") : F("settings.saveFailed", _settingsService.SettingsPath));
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private FrameworkElement ThemeCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(18, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = T("settings.theme"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.themeDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var currentTheme = _settings.Theme ?? AppTheme.System;
        var combo = new ComboBox { MinWidth = 170 };
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeSystem"), Tag = AppTheme.System });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeLight"), Tag = AppTheme.Light });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeDark"), Tag = AppTheme.Dark });
        combo.SelectedIndex = currentTheme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppTheme theme && theme != (_settings.Theme ?? AppTheme.System))
            {
                _settings.Theme = theme;
                ApplyTheme(theme);
                var saved = _settingsService.Save(_settings);
                SetStatus(saved ? T("settings.saved") : F("settings.saveFailed", _settingsService.SettingsPath));
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private FrameworkElement WinUiStyleCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(18, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = T("settings.winUiStyle"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.winUiStyleDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var currentStyle = _settings.WinUiStyle ?? AppWinUiStyle.Default;
        var combo = new ComboBox { MinWidth = 170 };
        combo.Items.Add(new ComboBoxItem { Content = T("settings.winUiStyleDefault"), Tag = AppWinUiStyle.Default });
        combo.Items.Add(new ComboBoxItem { Content = "Mica", Tag = AppWinUiStyle.Mica });
        combo.Items.Add(new ComboBoxItem { Content = "Acrylic", Tag = AppWinUiStyle.Acrylic });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.winUiStyleSolid"), Tag = AppWinUiStyle.Solid });
        combo.SelectedIndex = currentStyle switch
        {
            AppWinUiStyle.Mica => 1,
            AppWinUiStyle.Acrylic => 2,
            AppWinUiStyle.Solid => 3,
            _ => 0
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppWinUiStyle style && style != (_settings.WinUiStyle ?? AppWinUiStyle.Default))
            {
                _settings.WinUiStyle = style;
                ApplyWinUiStyle(style);
                var saved = _settingsService.Save(_settings);
                SetStatus(saved ? T("settings.saved") : F("settings.saveFailed", _settingsService.SettingsPath));
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private static Border RiskBadge(RiskLevel risk, string? label = null)
    {
        var color = risk switch
        {
            RiskLevel.Safe => Colors.SeaGreen,
            RiskLevel.Medium => Colors.DarkOrange,
            RiskLevel.High => Colors.IndianRed,
            _ => Colors.Gray
        };

        return new Border
        {
            Padding = new Thickness(10, 5, 10, 5),
            CornerRadius = new CornerRadius(6),
            Background = Brush(Color.FromArgb(38, color.R, color.G, color.B)),
            Child = new TextBlock { Text = label ?? risk.ToString(), Foreground = Brush(color), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
        };
    }

    private static Button ActionButton(string text, Symbol symbol, RoutedEventHandler action)
    {
        var button = new Button
        {
            MinWidth = 126,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new SymbolIcon(symbol),
                    new TextBlock { Text = text }
                }
            }
        };
        button.Click += action;
        return button;
    }

    private static Button IconButton(Symbol symbol, string label, RoutedEventHandler action)
    {
        var button = new Button
        {
            Width = 42,
            Height = 36,
            Content = new SymbolIcon(symbol)
        };
        ToolTipService.SetToolTip(button, label);
        button.Click += action;
        return button;
    }

    private void AddNavItem(string tag, string localizationKey, Symbol symbol)
    {
        var item = new NavigationViewItem
        {
            Content = T(localizationKey),
            Tag = tag,
            Icon = new SymbolIcon(symbol)
        };
        _navItems[tag] = item;
        _navigation.MenuItems.Add(item);
    }

    private async Task ShowDialogAsync(string title, object content, string closeText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeText,
            XamlRoot = _navigation.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    private static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    private static SolidColorBrush RiskBrush(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.Safe => Brush(Colors.SeaGreen),
            RiskLevel.Medium => Brush(Colors.DarkOrange),
            RiskLevel.High => Brush(Colors.IndianRed),
            _ => Brush(Colors.Gray)
        };
    }

    private string T(string key)
    {
        return _localization.Get(key);
    }

    private string F(string key, params object[] args)
    {
        return _localization.Format(key, args);
    }

    private string TaskLabel(MaintenanceTask task)
    {
        return _localization.TaskLabel(task.Id, task.Label);
    }

    private string TaskDescription(MaintenanceTask task)
    {
        return _localization.TaskDescription(task.Id, task.Description);
    }

    private string TaskImpact(MaintenanceTask task)
    {
        return _localization.TaskImpact(task.Id, task.EstimatedImpact);
    }

    private string LocalizeTaskLabel(string taskId, string fallback)
    {
        var key = $"task.{taskId}.label";
        var value = _localization.Get(key);
        return value == key ? fallback : value;
    }

    private void RefreshShellText()
    {
        Title = T("app.title");
        _navigation.PaneTitle = T("app.paneTitle");
        _statusText.Text = T("common.ready");

        foreach (var pair in _navItems)
        {
            pair.Value.Content = T(GetNavKey(pair.Key));
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        _navigation.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ApplyWinUiStyle(AppWinUiStyle style)
    {
        SystemBackdrop = style switch
        {
            AppWinUiStyle.Mica => new MicaBackdrop(),
            AppWinUiStyle.Acrylic => new DesktopAcrylicBackdrop(),
            AppWinUiStyle.Solid => null,
            _ => new MicaBackdrop()
        };
    }

    private static string GetNavKey(string tag)
    {
        return tag switch
        {
            "dashboard" => "nav.dashboard",
            "cleanup" => "nav.cleanup",
            "storage" => "nav.storage",
            "startup" => "nav.startup",
            "updates" => "nav.updates",
            "repair" => "nav.repair",
            "history" => "nav.history",
            "settings" => "nav.settings",
            _ => tag
        };
    }

    private void TryResize(int width, int height)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(width, height));
        }
        catch
        {
            // Window sizing is best-effort for unpackaged WinUI.
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void OpenFile(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

    private static void OpenContainingFolder(string command)
    {
        var path = command.Trim('"');
        if (!File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(path), UseShellExecute = true });
    }
}
