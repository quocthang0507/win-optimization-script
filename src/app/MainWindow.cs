using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.Views;
using WinRT.Interop;

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
    private readonly IpcClient _ipcClient = new();

    private readonly NavigationView _navigation;
    private readonly Dictionary<string, NavigationViewItem> _navItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScrollViewer _scrollViewer;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _statusProgress;
    
    private readonly Dictionary<string, BasePage> _pages = new(StringComparer.OrdinalIgnoreCase);
    private string _currentPageTag = "dashboard";

    internal PathService Paths => _paths;
    internal AppSettingsService SettingsService => _settingsService;
    internal CommandRunner Commands => _commands;
    internal MaintenanceCatalog Catalog => _catalog;
    internal AppSettings Settings => _settings;
    internal ReportService Reports => _reports;
    internal CleanupService Cleanup => _cleanup;
    internal SystemStatusService Status => _status;
    internal WingetService Winget => _winget;
    internal StartupService Startup => _startup;
    internal LocalizationService Localization => _localization;
    internal MaintenanceExecutionService Execution => _execution;
    internal DiskAnalysisService DiskAnalysis => _diskAnalysis;
    internal StorageCleanupService StorageCleanup => _storageCleanup;
    internal NavigationView Navigation_Internal => _navigation;

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

        try
        {
            var connected = Task.Run(() => _ipcClient.ConnectAsync(2000)).GetAwaiter().GetResult();
            if (connected)
            {
                _cleanup.Client = _ipcClient;
                _status.Client = _ipcClient;
                _winget.Client = _ipcClient;
                _execution.Client = _ipcClient;
                _startup.Client = _ipcClient;
            }
        }
        catch
        {
            // Fallback
        }

        Title = T("app.title");

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon loading is best-effort.
        }

        _scrollViewer = new ScrollViewer();
        _statusProgress = new ProgressBar
        {
            IsIndeterminate = true,
            Width = 140,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _statusText = new TextBlock 
        { 
            Text = T("common.ready"), 
            Foreground = new SolidColorBrush(Colors.MediumSeaGreen),
            Opacity = 0.9, 
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _navigation = new NavigationView
        {
            PaneTitle = T("app.paneTitle"),
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            Content = _scrollViewer
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
        AddNavItem("settings", "nav.settings", Symbol.Setting, isFooter: true);

        _navigation.SelectionChanged += (sender, args) =>
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                var ignored = NavigateAsync(tag);
            }
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(_navigation, 0);
        rootGrid.Children.Add(_navigation);

        var statusBar = new Border
        {
            Padding = new Thickness(0, 4, 28, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12,
                Children = { _statusProgress, _statusText }
            }
        };
        Grid.SetRow(statusBar, 1);
        rootGrid.Children.Add(statusBar);

        Content = rootGrid;
        _navigation.SelectedItem = _navigation.MenuItems[0];
    }

    internal async Task NavigateToTagAsync(string tag)
    {
        if (_navItems.TryGetValue(tag, out var item))
        {
            _navigation.SelectedItem = item;
        }
        else
        {
            await NavigateAsync(tag);
        }
    }

    internal async Task NavigateAsync(string tag)
    {
        _currentPageTag = tag;
        _scrollViewer.Content = null;
        SetStatus(T("common.loading"));

        if (!_pages.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "dashboard" => new DashboardPage(this),
                "cleanup" => new MaintenancePage(this, T("nav.cleanup"), "Cleanup", includePrivacy: true),
                "storage" => new StoragePage(this),
                "startup" => new StartupPage(this),
                "updates" => new UpdatesPage(this),
                "repair" => new MaintenancePage(this, T("nav.repair"), "Repair", includeOptimization: true),
                "history" => new HistoryPage(this),
                "settings" => new SettingsPage(this),
                _ => null
            };

            if (page != null)
            {
                _pages[tag] = page;
            }
        }

        if (page != null)
        {
            await page.OnNavigatedToAsync();
            _scrollViewer.Content = page;
        }

        SetStatus(T("common.ready"));
    }

    internal async Task ChangeLanguageAsync(AppLanguage language)
    {
        _localization.CurrentLanguage = language;
        _settings.Language = language;
        var saved = _settingsService.Save(_settings);
        if (_ipcClient.IsConnected)
        {
            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new SettingsPayload { Language = language.ToString() });
                await _ipcClient.SendRequestAsync("SaveSettings", payload);
            }
            catch { }
        }
        RefreshShellText();
        _pages.Clear();
        await NavigateAsync("settings");
        SetStatus(saved ? T("settings.saved") : F("settings.saveFailed", _settingsService.SettingsPath));
    }

    internal string Translate(string key) => T(key);
    internal string FormatTranslation(string key, params object[] args) => F(key, args);
    internal string TaskLabel_Internal(MaintenanceTask task) => TaskLabel(task);
    internal string TaskDescription_Internal(MaintenanceTask task) => TaskDescription(task);
    internal string TaskImpact_Internal(MaintenanceTask task) => TaskImpact(task);
    internal async Task ShowDialogAsync_Internal(string title, object content, string closeText) => await ShowDialogAsync(title, content, closeText);
    internal async Task ShowRunResultAsync_Internal(TaskRunResult result) => await ShowRunResultAsync(result);
    internal void SetStatusText(string text) => SetStatus(text);

    internal async Task PreviewTaskAsync(MaintenanceTask task)
    {
        SetStatus(F("status.scanningTask", TaskLabel(task)));
        var preview = await _cleanup.PreviewAsync(task);
        var panel = new StackPanel { Spacing = 8, MaxWidth = 720 };
        panel.Children.Add(new TextBlock { Text = preview.Summary, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var detailsPanel = new StackPanel { Spacing = 6 };
        var hasDetails = false;

        foreach (var warning in preview.Warnings)
        {
            detailsPanel.Children.Add(new TextBlock { Text = warning, Foreground = new SolidColorBrush(Colors.DarkOrange), TextWrapping = TextWrapping.Wrap });
            hasDetails = true;
        }

        foreach (var command in preview.PlannedCommands)
        {
            detailsPanel.Children.Add(new TextBlock { Text = command, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"), TextWrapping = TextWrapping.Wrap });
            hasDetails = true;
        }

        foreach (var target in preview.Targets.Take(100))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = F("preview.targetLine", target.Name, Formatters.FormatBytes(target.Bytes), target.FileCount, target.Status),
                TextWrapping = TextWrapping.Wrap,
                Opacity = target.Exists ? 0.9 : 0.55
            });
            hasDetails = true;
        }

        if (preview.Targets.Count > 100)
        {
            detailsPanel.Children.Add(new TextBlock { Text = F("preview.moreTargets", preview.Targets.Count - 100), Opacity = 0.65 });
            hasDetails = true;
        }

        if (hasDetails)
        {
            var scrollViewer = new ScrollViewer
            {
                Content = detailsPanel,
                MaxHeight = 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 8, 0, 0)
            };
            panel.Children.Add(scrollViewer);
        }

        SetStatus(T("common.ready"));
        await ShowDialogAsync(F("preview.title", TaskLabel(task)), panel, T("common.close"));
    }

    internal async Task RunTaskAsync(MaintenanceTask task)
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
        SetStatus(T("common.ready"));
        await ShowRunResultAsync(result);
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

        var logPanel = new StackPanel { Spacing = 6 };
        var hasDetails = false;

        foreach (var message in result.Messages.Where(message => !string.IsNullOrWhiteSpace(message)).Take(100))
        {
            logPanel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
            hasDetails = true;
        }

        foreach (var error in result.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).Take(100))
        {
            logPanel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.IndianRed) });
            hasDetails = true;
        }

        if (hasDetails)
        {
            var scrollViewer = new ScrollViewer
            {
                Content = logPanel,
                MaxHeight = 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 8, 0, 0)
            };
            panel.Children.Add(scrollViewer);
        }

        await ShowDialogAsync(LocalizeTaskLabel(result.TaskId, result.TaskLabel), panel, T("common.close"));
    }

    internal async Task ScanWingetAsync(StackPanel resultPanel)
    {
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

    private Border PackageRow(WingetPackage package)
    {
        return Card_Helper(package.Name, $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}", T("common.open"), (_, _) => { });
    }

    private static Border Card_Helper(string title, string body, string buttonText, RoutedEventHandler action)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.LightGray),
            Background = new SolidColorBrush(Color.FromArgb(18, 128, 128, 128))
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

    private static TextBlock SectionTitle(string text)
    {
        return new TextBlock { Text = text, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
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
        var brush = GetStatusBrush(text);
        _statusText.Foreground = brush;

        var isBusy = !string.IsNullOrEmpty(text) 
            && text != T("common.ready") 
            && text != T("settings.saved") 
            && !text.Contains(T("run.completed"))
            && !text.Contains(T("storage.scanCanceled"))
            && !text.Contains("Canceled")
            && !text.Contains("Hủy");

        _statusProgress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        _statusProgress.Foreground = brush;
    }

    private SolidColorBrush GetStatusBrush(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SolidColorBrush(Colors.Gray);
        }

        // Ready, Saved, Completed (e.g. Sẵn sàng, Đã lưu cài đặt, Đã hoàn tất)
        if (text == T("common.ready") || text == T("settings.saved") || text.Contains(T("run.completed")))
        {
            return new SolidColorBrush(Colors.MediumSeaGreen);
        }

        // Stopping (e.g. Đang dừng quét, Đang dừng...)
        if (text.Contains(T("storage.stopping")) || text.Contains("dừng") || text.Contains("Stopping") || text.Contains("Canceled") || text.Contains("Hủy"))
        {
            return new SolidColorBrush(Colors.DarkOrange);
        }

        // Scanning, Running, Loading, Cleaning (e.g. Đang tải..., Đang quét..., Đang chạy..., Đang dọn...)
        return new SolidColorBrush(Colors.DeepSkyBlue);
    }

    private static SolidColorBrush RiskBrush(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.Safe => new SolidColorBrush(Colors.SeaGreen),
            RiskLevel.Medium => new SolidColorBrush(Colors.DarkOrange),
            RiskLevel.High => new SolidColorBrush(Colors.IndianRed),
            _ => new SolidColorBrush(Colors.Gray)
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
        SetStatus(T("common.ready"));

        foreach (var pair in _navItems)
        {
            pair.Value.Content = T(GetNavKey(pair.Key));
        }
    }

    internal void ApplyTheme_Internal(AppTheme theme) => ApplyTheme(theme);
    internal void ApplyWinUiStyle_Internal(AppWinUiStyle style) => ApplyWinUiStyle(style);

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

    private void AddNavItem(string tag, string localizationKey, Symbol symbol, bool isFooter = false)
    {
        var item = new NavigationViewItem
        {
            Content = T(localizationKey),
            Tag = tag,
            Icon = new SymbolIcon(symbol)
        };
        _navItems[tag] = item;
        if (isFooter)
        {
            _navigation.FooterMenuItems.Add(item);
        }
        else
        {
            _navigation.MenuItems.Add(item);
        }
    }

    internal static void OpenFolder_Internal(string path) => OpenFolder(path);
    internal static void OpenFile_Internal(string path) => OpenFile(path);
    internal static void OpenContainingFolder_Internal(string command) => OpenContainingFolder(command);

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
