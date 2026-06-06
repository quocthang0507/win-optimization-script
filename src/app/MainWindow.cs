using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.Views;
using WinRT.Interop;

namespace WinOptimizationApp;

public sealed class MainWindow : Window
{
    private readonly IpcClient _ipcClient = new();
    private readonly Dictionary<string, NavigationViewItem> _navItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScrollViewer _scrollViewer;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _statusProgress;
    private bool _updateCheckStarted;

    private readonly Dictionary<string, BasePage> _pages = new(StringComparer.OrdinalIgnoreCase);
    private string _currentPageTag = "dashboard";

    internal PathService Paths { get; } = new();
    internal AppSettingsService SettingsService { get; } = new();
    internal CommandRunner Commands { get; } = new();
    internal MaintenanceCatalog Catalog { get; } = new();
    internal AppSettings Settings { get; }
    internal ReportService Reports { get; }
    internal CleanupService Cleanup { get; }
    internal SystemStatusService Status { get; }
    internal WingetService Winget { get; }
    internal GitHubUpdateService Updates { get; } = new();
    internal StartupService Startup { get; } = new();
    internal LocalizationService Localization { get; }
    internal MaintenanceExecutionService Execution { get; }
    internal DiskAnalysisService DiskAnalysis { get; } = new();
    internal StorageCleanupService StorageCleanup { get; }
    internal NavigationView Navigation_Internal { get; }

    public MainWindow()
    {
        Settings = SettingsService.Load();
        Localization = new LocalizationService(Settings.Language);
        Reports = new ReportService(Paths);
        Cleanup = new CleanupService(Commands);
        Status = new SystemStatusService(Commands, Reports);
        Winget = new WingetService(Commands);
        Execution = new MaintenanceExecutionService(Cleanup, Commands, Paths, Reports, new RestorePointService(Commands));
        StorageCleanup = new StorageCleanupService(Reports);

        if (ShouldConnectToRunner())
        {
            try
            {
                var connected = Task.Run(() => _ipcClient.ConnectAsync(2000)).GetAwaiter().GetResult();
                if (connected)
                {
                    Cleanup.Client = _ipcClient;
                    Status.Client = _ipcClient;
                    Winget.Client = _ipcClient;
                    Execution.Client = _ipcClient;
                    Startup.Client = _ipcClient;
                }
            }
            catch
            {
                // Fallback
            }
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
        var initialStatus = T("common.ready");
        if (SystemStatusService.IsAdministrator())
        {
            initialStatus += Settings.Language == AppLanguage.Vietnamese ? " (Quyền Admin)" : " (Admin)";
        }

        _statusText = new TextBlock
        {
            Text = initialStatus,
            Foreground = new SolidColorBrush(Colors.MediumSeaGreen),
            Opacity = 0.9,
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Navigation_Internal = new NavigationView
        {
            PaneTitle = T("app.paneTitle"),
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            Content = _scrollViewer
        };
        ApplyTheme(Settings.Theme ?? AppTheme.System);
        ApplyWinUiStyle(Settings.WinUiStyle ?? AppWinUiStyle.Default);

        AddNavItem("dashboard", "nav.dashboard", Symbol.Home);
        AddNavItem("cleanup", "nav.cleanup", Symbol.Delete);
        AddNavItem("storage", "nav.storage", Symbol.View);
        AddNavItem("startup", "nav.startup", Symbol.List);
        AddNavItem("updates", "nav.updates", Symbol.Download);
        AddNavItem("repair", "nav.repair", Symbol.Refresh);
        AddNavItem("history", "nav.history", Symbol.Document);
        AddNavItem("settings", "nav.settings", Symbol.Setting, isFooter: true);

        Navigation_Internal.SelectionChanged += async (sender, args) =>
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                try
                {
                    await NavigateAsync(tag);
                }
                catch (Exception ex)
                {
                    SetStatus($"Error: {ex.Message}");
                }
            }
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(Navigation_Internal, 0);
        rootGrid.Children.Add(Navigation_Internal);

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
        Navigation_Internal.SelectedItem = Navigation_Internal.MenuItems[0];
        Activated += async (_, _) => await CheckForUpdatesOnceAsync();
    }

    private async Task CheckForUpdatesOnceAsync()
    {
        if (_updateCheckStarted)
        {
            return;
        }

        _updateCheckStarted = true;
        try
        {
            SetStatus(T("update.checking"));
            var update = await Updates.CheckLatestReleaseAsync();
            if (update is null)
            {
                SetStatus(T("common.ready"));
                return;
            }

            if (!update.IsUpdateAvailable)
            {
                SetStatus(T("update.latest"));
                return;
            }

            SetStatus(F("update.availableStatus", update.TagName));
            await ShowUpdateDialogAsync(update);
            SetStatus(T("common.ready"));
        }
        catch
        {
            SetStatus(T("common.ready"));
        }
    }

    private async Task ShowUpdateDialogAsync(AppUpdateInfo update)
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 680 };
        panel.Children.Add(new TextBlock
        {
            Text = F("update.availableBody", update.LatestVersion, update.CurrentVersion),
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(update.AssetName))
        {
            panel.Children.Add(new TextBlock
            {
                Text = update.AssetName,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new ContentDialog
        {
            Title = F("update.availableTitle", update.TagName),
            Content = panel,
            PrimaryButtonText = string.IsNullOrWhiteSpace(update.AssetUrl) ? T("update.openRelease") : T("update.download"),
            SecondaryButtonText = string.IsNullOrWhiteSpace(update.AssetUrl) ? string.Empty : T("update.openRelease"),
            CloseButtonText = T("common.close"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Navigation_Internal.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            OpenUrl(string.IsNullOrWhiteSpace(update.AssetUrl) ? update.ReleaseUrl : update.AssetUrl);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OpenUrl(update.ReleaseUrl);
        }
    }

    internal async Task NavigateToTagAsync(string tag)
    {
        if (_navItems.TryGetValue(tag, out var item))
        {
            Navigation_Internal.SelectedItem = item;
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
        Localization.CurrentLanguage = language;
        Settings.Language = language;
        var saved = SettingsService.Save(Settings);
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
        SetStatus(saved ? T("settings.saved") : F("settings.saveFailed", SettingsService.SettingsPath));
    }

    internal string Translate(string key)
    {
        return T(key);
    }

    internal string FormatTranslation(string key, params object[] args)
    {
        return F(key, args);
    }

    internal string TaskLabel_Internal(MaintenanceTask task)
    {
        return TaskLabel(task);
    }

    internal string TaskDescription_Internal(MaintenanceTask task)
    {
        return TaskDescription(task);
    }

    internal string TaskImpact_Internal(MaintenanceTask task)
    {
        return TaskImpact(task);
    }

    internal async Task ShowDialogAsync_Internal(string title, object content, string closeText)
    {
        await ShowDialogAsync(title, content, closeText);
    }

    internal async Task ShowRunResultAsync_Internal(TaskRunResult result)
    {
        await ShowRunResultAsync(result);
    }

    internal void SetStatusText(string text)
    {
        SetStatus(text);
    }

    internal async Task PreviewTaskAsync(MaintenanceTask task)
    {
        SetStatus(F("status.scanningTask", TaskLabel(task)));
        var preview = await Cleanup.PreviewAsync(task);
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

    public static void ElevateApplication()
    {
        if (SystemStatusService.IsAdministrator())
        {
            return;
        }

        if (AppRestartService.ScheduleRestartAsAdministrator())
        {
            Application.Current.Exit();
        }
    }

    private static bool ShouldConnectToRunner()
    {
        var args = Environment.GetCommandLineArgs();
        return args.Any(arg => arg.Equals(AppProcessLauncher.ConnectRunnerArgument, StringComparison.OrdinalIgnoreCase)) &&
               !args.Any(arg => arg.Equals(AppProcessLauncher.StandaloneArgument, StringComparison.OrdinalIgnoreCase));
    }

    internal async Task RunTaskAsync(MaintenanceTask task)
    {
        if (task.RequiresAdmin && !SystemStatusService.IsAdministrator())
        {
            var dialog = new ContentDialog
            {
                Title = T("admin.title"),
                Content = new TextBlock
                {
                    Text = F("admin.message", TaskLabel(task)),
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = T("admin.elevateButton"),
                CloseButtonText = T("common.close"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Navigation_Internal.XamlRoot
            };

            var dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Primary)
            {
                ElevateApplication();
            }
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
        var result = await Execution.RunAsync(task);
        SetStatus(T("common.ready"));
        await ShowRunResultAsync(result);
    }

    private async Task<bool> ConfirmAsync(MaintenanceTask task)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = TaskDescription(task), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = F("confirm.risk", Localization.RiskName(task.RiskLevel)), Foreground = RiskBrush(task.RiskLevel) });
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
            XamlRoot = Navigation_Internal.XamlRoot
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
        var packages = await Winget.ScanAsync();
        resultPanel.Children.Add(SectionTitle(F("updates.packageUpdates", packages.Count)));
        foreach (var package in packages)
        {
            resultPanel.Children.Add(PackageRow(package));
        }
        SetStatus(T("common.ready"));
    }

    private Border PackageRow(WingetPackage package)
    {
        return Card_Helper(package.Name, $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}", T("common.open"), (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = $"https://winget.run/pkg/{package.Id}", UseShellExecute = true }); } catch { }
        });
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
            XamlRoot = Navigation_Internal.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void SetStatus(string text)
    {
        var displayText = text;
        if (text == T("common.ready"))
        {
            var isAdmin = SystemStatusService.IsAdministrator();
            if (isAdmin)
            {
                var isVi = Localization.CurrentLanguage == AppLanguage.Vietnamese;
                displayText += isVi ? " (Quyền Admin)" : " (Admin)";
            }
        }

        _statusText.Text = displayText;
        var brush = GetStatusBrush(text);
        _statusText.Foreground = brush;

        var isBusy = !string.IsNullOrEmpty(text)
            && text != T("common.ready")
            && text != T("settings.saved")
            && text != T("update.latest")
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
        if (text == T("common.ready") || text == T("settings.saved") || text == T("update.latest") || text.Contains(T("run.completed")))
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
        return Localization.Get(key);
    }

    private string F(string key, params object[] args)
    {
        return Localization.Format(key, args);
    }

    private string TaskLabel(MaintenanceTask task)
    {
        return Localization.TaskLabel(task.Id, task.Label);
    }

    private string TaskDescription(MaintenanceTask task)
    {
        return Localization.TaskDescription(task.Id, task.Description);
    }

    private string TaskImpact(MaintenanceTask task)
    {
        return Localization.TaskImpact(task.Id, task.EstimatedImpact);
    }

    private string LocalizeTaskLabel(string taskId, string fallback)
    {
        var key = $"task.{taskId}.label";
        var value = Localization.Get(key);
        return value == key ? fallback : value;
    }

    private void RefreshShellText()
    {
        Title = T("app.title");
        Navigation_Internal.PaneTitle = T("app.paneTitle");
        SetStatus(T("common.ready"));

        foreach (var pair in _navItems)
        {
            pair.Value.Content = T(GetNavKey(pair.Key));
        }
    }

    internal void ApplyTheme_Internal(AppTheme theme)
    {
        ApplyTheme(theme);
    }

    internal void ApplyWinUiStyle_Internal(AppWinUiStyle style)
    {
        ApplyWinUiStyle(style);
    }

    private void ApplyTheme(AppTheme theme)
    {
        Navigation_Internal.RequestedTheme = theme switch
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
            Navigation_Internal.FooterMenuItems.Add(item);
        }
        else
        {
            Navigation_Internal.MenuItems.Add(item);
        }
    }

    internal static void OpenFolder_Internal(string path)
    {
        OpenFolder(path);
    }

    internal static void OpenFile_Internal(string path)
    {
        OpenFile(path);
    }

    internal static void OpenContainingFolder_Internal(string command)
    {
        OpenContainingFolder(command);
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

        var dirPath = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dirPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = dirPath, UseShellExecute = true });
    }

    private static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
    }
}
