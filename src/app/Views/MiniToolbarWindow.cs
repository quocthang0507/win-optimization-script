using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed class MiniToolbarWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly DispatcherTimer _timer;

    private TextBlock? _cpuText;
    private TextBlock? _ramText;
    private TextBlock? _diskText;
    private ProgressBar? _cpuProgress;
    private ProgressBar? _ramProgress;

    public MiniToolbarWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        Title = T("widget.title");

        ApplySystemBackdrop();

        Content = CreateLayout();

        ConfigureWindow();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (s, e) => await UpdateMetricsAsync();
        _timer.Start();

        Closed += (s, e) =>
        {
            _timer.Stop();
        };

        _ = UpdateMetricsAsync();
    }

    private void ApplySystemBackdrop()
    {
        SystemBackdrop = _mainWindow.Settings.WinUiStyle switch
        {
            AppWinUiStyle.Mica => new MicaBackdrop(),
            AppWinUiStyle.Acrylic => new DesktopAcrylicBackdrop(),
            AppWinUiStyle.Solid => null,
            _ => new MicaBackdrop()
        };
    }

    private Grid CreateLayout()
    {
        var rootGrid = new Grid
        {
            Padding = new Thickness(12),
            ColumnSpacing = 12
        };
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition());
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var metricsPanel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        var cpuHeader = new Grid();
        cpuHeader.ColumnDefinitions.Add(new ColumnDefinition());
        cpuHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cpuTitle = new TextBlock { Text = T("widget.cpu"), FontSize = 11, Opacity = 0.7 };
        _cpuText = new TextBlock { Text = "0%", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(cpuTitle, 0);
        Grid.SetColumn(_cpuText, 1);
        cpuHeader.Children.Add(cpuTitle);
        cpuHeader.Children.Add(_cpuText);
        metricsPanel.Children.Add(cpuHeader);

        _cpuProgress = new ProgressBar { Height = 4, Maximum = 100, CornerRadius = new CornerRadius(2) };
        metricsPanel.Children.Add(_cpuProgress);

        var ramHeader = new Grid();
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition());
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ramTitle = new TextBlock { Text = T("widget.ram"), FontSize = 11, Opacity = 0.7 };
        _ramText = new TextBlock { Text = "0%", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(ramTitle, 0);
        Grid.SetColumn(_ramText, 1);
        ramHeader.Children.Add(ramTitle);
        ramHeader.Children.Add(_ramText);
        metricsPanel.Children.Add(ramHeader);

        _ramProgress = new ProgressBar { Height = 4, Maximum = 100, CornerRadius = new CornerRadius(2) };
        metricsPanel.Children.Add(_ramProgress);

        var diskHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var diskTitle = new TextBlock { Text = T("widget.disk") + ":", FontSize = 11, Opacity = 0.7 };
        _diskText = new TextBlock { Text = "N/A", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        diskHeader.Children.Add(diskTitle);
        diskHeader.Children.Add(_diskText);
        metricsPanel.Children.Add(diskHeader);

        Grid.SetColumn(metricsPanel, 0);
        rootGrid.Children.Add(metricsPanel);

        var actionsPanel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        actionsPanel.Children.Add(QuickActionButton(Symbol.Refresh, T("widget.healthCheck"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("dashboard");
        }));

        actionsPanel.Children.Add(QuickActionButton(Symbol.Delete, T("widget.scan"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("cleanup");
        }));

        actionsPanel.Children.Add(QuickActionButton(Symbol.View, T("widget.storage"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("storage");
        }));

        Grid.SetColumn(actionsPanel, 1);
        rootGrid.Children.Add(actionsPanel);

        return rootGrid;
    }

    private Button QuickActionButton(Symbol symbol, string label, Action action)
    {
        var button = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Content = new SymbolIcon(symbol)
        };
        ToolTipService.SetToolTip(button, label);
        button.Click += (s, e) => action();
        return button;
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new Windows.Graphics.SizeInt32(290, 140));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch { }
    }

    private async Task UpdateMetricsAsync()
    {
        try
        {
            var metrics = await _mainWindow.PerformanceMonitoring.GetMetricsAsync();

            _cpuText!.Text = $"{metrics.CpuUsagePercent:F0}%";
            _cpuProgress!.Value = metrics.CpuUsagePercent;

            _ramText!.Text = $"{metrics.RamUsagePercent:F0}%";
            _ramProgress!.Value = metrics.RamUsagePercent;

            _diskText!.Text = Formatters.FormatBytes(metrics.DiskFreeBytes);
        }
        catch
        {
            // Ignore background polling exceptions
        }
    }

    private string T(string key)
    {
        return _mainWindow.Translate(key);
    }
}
