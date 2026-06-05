using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.Graphics;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinRT.Interop;

namespace WinOptimizationApp;

public sealed class MainWindow : Window
{
    private readonly PathService _paths = new();
    private readonly CommandRunner _commands = new();
    private readonly MaintenanceCatalog _catalog = new();
    private readonly ReportService _reports;
    private readonly CleanupService _cleanup;
    private readonly SystemStatusService _status;
    private readonly WingetService _winget;
    private readonly StartupService _startup = new();
    private readonly MaintenanceExecutionService _execution;

    private readonly NavigationView _navigation;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _page;
    private readonly TextBlock _statusText;

    public MainWindow()
    {
        _reports = new ReportService(_paths);
        _cleanup = new CleanupService(_commands);
        _status = new SystemStatusService(_commands, _reports);
        _winget = new WingetService(_commands);
        _execution = new MaintenanceExecutionService(_cleanup, _commands, _paths, _reports, new RestorePointService(_commands));

        Title = "Windows System Maintenance";
        TryResize(1180, 760);

        _page = new StackPanel { Spacing = 16, Padding = new Thickness(28, 22, 28, 28) };
        _scrollViewer = new ScrollViewer { Content = _page };
        _statusText = new TextBlock { Text = "Ready", Opacity = 0.7, Margin = new Thickness(12, 0, 16, 0) };

        _navigation = new NavigationView
        {
            PaneTitle = "Maintenance",
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            Content = _scrollViewer,
            FooterMenuItems = { _statusText }
        };

        AddNavItem("dashboard", "Dashboard", Symbol.Home);
        AddNavItem("cleanup", "Cleanup", Symbol.Delete);
        AddNavItem("startup", "Startup", Symbol.List);
        AddNavItem("updates", "Updates", Symbol.Download);
        AddNavItem("repair", "Repair", Symbol.Refresh);
        AddNavItem("history", "History", Symbol.Document);
        AddNavItem("settings", "Settings", Symbol.Setting);

        _navigation.SelectionChanged += (sender, args) =>
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                var ignored = NavigateAsync(tag);
            }
        };

        Content = _navigation;
        _navigation.SelectedItem = _navigation.MenuItems[0];
        _ = NavigateAsync("dashboard");
    }

    private async Task NavigateAsync(string tag)
    {
        _page.Children.Clear();
        SetStatus("Loading...");

        switch (tag)
        {
            case "dashboard":
                await RenderDashboardAsync();
                break;
            case "cleanup":
                RenderTaskPage("Cleanup", "Cleanup", includePrivacy: true);
                break;
            case "startup":
                RenderStartupPage();
                break;
            case "updates":
                RenderUpdatesPage();
                break;
            case "repair":
                RenderTaskPage("Repair", "Repair", includeOptimization: true);
                break;
            case "history":
                RenderHistoryPage();
                break;
            case "settings":
                RenderSettingsPage();
                break;
        }

        SetStatus("Ready");
    }

    private async Task RenderDashboardAsync()
    {
        AddHeader("Dashboard", "Machine health, safety status and recent maintenance.");
        var status = await _status.GetAsync();

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        AddMetric(grid, 0, 0, "Windows", status.WindowsVersion, status.PendingReboot ? "Pending reboot" : "No reboot pending", status.PendingReboot ? Colors.OrangeRed : Colors.SeaGreen);
        AddMetric(grid, 0, 1, "Administrator", status.IsAdministrator ? "Elevated" : "Standard user", status.IsAdministrator ? "High-risk tasks enabled" : "High-risk tasks need admin", status.IsAdministrator ? Colors.SeaGreen : Colors.DarkOrange);
        AddMetric(grid, 1, 0, "System drive", $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} free", $"{status.SystemDrive} of {Formatters.FormatBytes(status.SystemDriveTotalBytes)}", Colors.SteelBlue);
        AddMetric(grid, 1, 1, "Uptime", Formatters.FormatDuration(status.Uptime), status.WingetAvailable ? "WinGet available" : "WinGet not found", status.WingetAvailable ? Colors.SeaGreen : Colors.Gray);
        _page.Children.Add(grid);

        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        quick.Children.Add(ActionButton("Scan Cleanup", Symbol.Find, async (_, _) => await NavigateAsync("cleanup")));
        quick.Children.Add(ActionButton("Scan Updates", Symbol.Download, async (_, _) => await ScanWingetAsync()));
        quick.Children.Add(ActionButton("Open Logs", Symbol.OpenFile, (_, _) => OpenFolder(_paths.LogsDirectory)));
        _page.Children.Add(quick);

        if (!string.IsNullOrWhiteSpace(status.LastReportPath))
        {
            _page.Children.Add(Card("Last Report", status.LastReportPath, "Open", (_, _) => OpenFile(status.LastReportPath)));
        }
    }

    private void RenderTaskPage(string title, string group, bool includePrivacy = false, bool includeOptimization = false)
    {
        AddHeader(title, "Preview first, then run selected tasks with risk-aware confirmation.");

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
            _page.Children.Add(SectionTitle(groupName));
            foreach (var task in _catalog.ByGroup(groupName))
            {
                AddTaskRow(task);
            }
        }
    }

    private void RenderStartupPage()
    {
        AddHeader("Startup", "Read-only inventory for startup entries.");
        var resultPanel = new StackPanel { Spacing = 8 };
        var scanButton = ActionButton("Scan Startup", Symbol.Find, async (_, _) =>
        {
            SetStatus("Scanning startup entries...");
            resultPanel.Children.Clear();
            var entries = await _startup.ScanAsync();
            resultPanel.Children.Add(SectionTitle($"{entries.Count} entries"));
            foreach (var entry in entries)
            {
                resultPanel.Children.Add(StartupRow(entry));
            }
            SetStatus("Ready");
        });

        _page.Children.Add(scanButton);
        _page.Children.Add(resultPanel);
    }

    private void RenderUpdatesPage()
    {
        AddHeader("Updates", "Preview WinGet packages before upgrading.");
        var resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(ActionButton("Scan WinGet", Symbol.Find, async (_, _) =>
        {
            await ScanWingetAsync(resultPanel);
        }));
        actions.Children.Add(ActionButton("Upgrade All", Symbol.Download, async (_, _) =>
        {
            var task = _catalog.GetById("software.winget");
            await RunTaskAsync(task);
        }));
        _page.Children.Add(actions);
        _page.Children.Add(resultPanel);
    }

    private void RenderHistoryPage()
    {
        AddHeader("History", "Reports saved after task execution.");

        if (!Directory.Exists(_paths.LogsDirectory))
        {
            _page.Children.Add(InfoBlock("No reports yet."));
            return;
        }

        foreach (var report in Directory.GetFiles(_paths.LogsDirectory, "maintenance-*.json").OrderByDescending(File.GetLastWriteTime).Take(30))
        {
            _page.Children.Add(Card(Path.GetFileName(report), report, "Open", (_, _) => OpenFile(report)));
        }
    }

    private void RenderSettingsPage()
    {
        AddHeader("Settings", "Paths and Windows entry points.");
        _page.Children.Add(Card("CLI script", _paths.CliScriptPath, "Launch", async (_, _) => await RunTaskAsync(_catalog.GetById("cli.launch"))));
        _page.Children.Add(Card("Storage Sense", "Open Windows Storage Sense settings.", "Open", async (_, _) => await RunTaskAsync(_catalog.GetById("settings.storage"))));
        _page.Children.Add(Card("Logs", _paths.LogsDirectory, "Open", (_, _) => OpenFolder(_paths.LogsDirectory)));
        _page.Children.Add(Card("Repository", _paths.RepositoryRoot, "Open", (_, _) => OpenFolder(_paths.RepositoryRoot)));
    }

    private async Task ScanWingetAsync(StackPanel? resultPanel = null)
    {
        resultPanel ??= new StackPanel { Spacing = 8 };
        if (!_page.Children.Contains(resultPanel))
        {
            _page.Children.Add(resultPanel);
        }

        SetStatus("Scanning WinGet...");
        resultPanel.Children.Clear();
        var packages = await _winget.ScanAsync();
        resultPanel.Children.Add(SectionTitle($"{packages.Count} package updates"));
        foreach (var package in packages)
        {
            resultPanel.Children.Add(PackageRow(package));
        }
        SetStatus("Ready");
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
        text.Children.Add(new TextBlock { Text = task.Label, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = task.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        text.Children.Add(new TextBlock { Text = task.EstimatedImpact, TextWrapping = TextWrapping.Wrap, Opacity = 0.65 });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var risk = RiskBadge(task.RiskLevel);
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
        SetStatus($"Scanning {task.Label}...");
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
                Text = $"{target.Name}: {Formatters.FormatBytes(target.Bytes)} / {target.FileCount:N0} files / {target.Status}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = target.Exists ? 0.9 : 0.55
            });
        }

        if (preview.Targets.Count > 20)
        {
            panel.Children.Add(new TextBlock { Text = $"+ {preview.Targets.Count - 20} more target(s)", Opacity = 0.65 });
        }

        await ShowDialogAsync($"{task.Label} preview", panel, "Close");
        SetStatus("Ready");
    }

    private async Task RunTaskAsync(MaintenanceTask task)
    {
        if (task.RequiresAdmin && !SystemStatusService.IsAdministrator())
        {
            await ShowDialogAsync("Administrator required", new TextBlock
            {
                Text = $"{task.Label} needs an elevated app session.",
                TextWrapping = TextWrapping.Wrap
            }, "Close");
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

        SetStatus($"Running {task.Label}...");
        var result = await _execution.RunAsync(task);
        await ShowRunResultAsync(result);
        SetStatus("Ready");
    }

    private async Task<bool> ConfirmAsync(MaintenanceTask task)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = task.Description, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"Risk: {task.RiskLevel}", Foreground = RiskBrush(task.RiskLevel) });
        panel.Children.Add(new TextBlock { Text = task.EstimatedImpact, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        if (task.CanRollback)
        {
            panel.Children.Add(new TextBlock { Text = "A restore point will be requested before running when possible.", TextWrapping = TextWrapping.Wrap });
        }

        var dialog = new ContentDialog
        {
            Title = $"Run {task.Label}?",
            Content = panel,
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _navigation.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowRunResultAsync(TaskRunResult result)
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 720 };
        panel.Children.Add(new TextBlock { Text = result.Success ? "Completed" : "Completed with warnings", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Freed {Formatters.FormatBytes(result.FreedBytes)}. Removed {result.FilesRemoved:N0}, skipped {result.FilesSkipped:N0}.", TextWrapping = TextWrapping.Wrap });

        foreach (var message in result.Messages.Where(message => !string.IsNullOrWhiteSpace(message)).Take(8))
        {
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        }

        foreach (var error in result.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).Take(8))
        {
            panel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Foreground = Brush(Colors.IndianRed) });
        }

        await ShowDialogAsync(result.TaskLabel, panel, "Close");
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

    private static FrameworkElement StartupRow(StartupEntry entry)
    {
        return Card(entry.Name, $"{entry.Source} / {(entry.Enabled ? "Enabled" : "Disabled")}\n{entry.Command}\n{entry.RiskHint}", "Open", (_, _) => OpenContainingFolder(entry.Command));
    }

    private static FrameworkElement PackageRow(WingetPackage package)
    {
        return Card(package.Name, $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}", "Details", (_, _) => { });
    }

    private static Border RiskBadge(RiskLevel risk)
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
            Child = new TextBlock { Text = risk.ToString(), Foreground = Brush(color), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
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

    private void AddNavItem(string tag, string text, Symbol symbol)
    {
        _navigation.MenuItems.Add(new NavigationViewItem
        {
            Content = text,
            Tag = tag,
            Icon = new SymbolIcon(symbol)
        });
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
