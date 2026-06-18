using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed class MiniToolbarWindow : Window
{
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    private readonly MainWindow _mainWindow;
    private readonly DispatcherTimer _timer;
    private readonly IntPtr _windowHandle;

    private TextBlock? _cpuText;
    private TextBlock? _ramText;
    private TextBlock? _diskText;
    private TextBlock? _networkText;
    private ProgressBar? _cpuProgress;
    private ProgressBar? _ramProgress;

    public MiniToolbarWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        Title = T("widget.title");
        _windowHandle = WindowNative.GetWindowHandle(this);

        ApplySystemBackdrop();
        Content = CreateLayout();
        ConfigureWindow();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await UpdateMetricsAsync();
        _timer.Start();

        Closed += (_, _) => _timer.Stop();
        _ = UpdateMetricsAsync();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void ApplySystemBackdrop()
    {
        SystemBackdrop = _mainWindow.Settings.WinUiStyle switch
        {
            AppWinUiStyle.Mica => new MicaBackdrop(),
            AppWinUiStyle.Acrylic => new DesktopAcrylicBackdrop(),
            AppWinUiStyle.Solid => null,
            _ => new DesktopAcrylicBackdrop()
        };
    }

    private Grid CreateLayout()
    {
        ResetMetricControls();

        var root = new Grid
        {
            Padding = new Thickness(10),
            RequestedTheme = MainWindow.CurrentElementTheme,
            Background = new SolidColorBrush(Colors.Transparent)
        };

        var surface = new Border
        {
            Padding = new Thickness(12, 9, 12, 11),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = WidgetBorderBrush(),
            Background = WidgetSurfaceBrush()
        };

        var content = new Grid { RowSpacing = 9 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        content.Children.Add(CreateHeader());

        var metrics = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        if (_mainWindow.Settings.WidgetShowCpu)
        {
            (_cpuText, _cpuProgress) = AddPercentMetric(metrics, T("widget.cpu"), Colors.DodgerBlue);
        }

        if (_mainWindow.Settings.WidgetShowRam)
        {
            (_ramText, _ramProgress) = AddPercentMetric(metrics, T("widget.ram"), Colors.MediumPurple);
        }

        if (_mainWindow.Settings.WidgetShowDisk)
        {
            _diskText = AddTextMetric(metrics, T("widget.disk"), "N/A", Symbol.Save);
        }

        if (_mainWindow.Settings.WidgetShowNetwork)
        {
            _networkText = AddTextMetric(metrics, T("widget.network"), "D 0 B/s   U 0 B/s", Symbol.Sync);
        }

        if (metrics.Children.Count == 0)
        {
            metrics.Children.Add(new TextBlock
            {
                Text = T("widget.noMetrics"),
                Margin = new Thickness(8, 12, 8, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetRow(metrics, 1);
        content.Children.Add(metrics);

        var actions = CreateActions();
        Grid.SetRow(actions, 2);
        content.Children.Add(actions);

        surface.Child = content;
        root.Children.Add(surface);
        return root;
    }

    private Grid CreateHeader()
    {
        var header = new Grid
        {
            MinHeight = 28,
            Background = new SolidColorBrush(Colors.Transparent)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.PointerPressed += BeginWindowDrag;

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Colors.MediumSeaGreen)
        });
        title.Children.Add(new TextBlock
        {
            Text = T("widget.title"),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(title);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var settings = HeaderButton(Symbol.Setting, T("widget.configure"));
        settings.Flyout = CreateSettingsFlyout();
        controls.Children.Add(settings);

        var close = HeaderButton(Symbol.Cancel, T("common.close"));
        close.Click += (_, _) => Close();
        controls.Children.Add(close);

        Grid.SetColumn(controls, 1);
        header.Children.Add(controls);
        return header;
    }

    private Grid CreateActions()
    {
        var actions = new Grid
        {
            Padding = new Thickness(4),
            ColumnSpacing = 5,
            Background = MetricCardBrush()
        };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());

        AddAction(actions, 0, Symbol.Refresh, T("widget.healthCheck"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("dashboard");
        });
        AddAction(actions, 1, Symbol.Delete, T("widget.scan"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("cleanup");
        });
        AddAction(actions, 2, Symbol.View, T("widget.storage"), async () =>
        {
            _mainWindow.BringToForeground();
            await _mainWindow.NavigateToTagAsync("storage");
        });
        AddAction(actions, 3, Symbol.Sync, T("widget.flushDns"), async () =>
        {
            await _mainWindow.NetworkOptimizer.FlushDnsAsync();
        });

        return actions;
    }

    private void AddAction(Grid panel, int column, Symbol symbol, string label, Func<Task> action)
    {
        var button = new Button
        {
            Height = 34,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new SymbolIcon(symbol)
        };
        ToolTipService.SetToolTip(button, label);
        button.Click += async (_, _) => await action();
        Grid.SetColumn(button, column);
        panel.Children.Add(button);
    }

    private (TextBlock Value, ProgressBar Progress) AddPercentMetric(StackPanel panel, string label, Color color)
    {
        var stack = new StackPanel { Spacing = 5 };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.72 });

        var value = new TextBlock
        {
            Text = "0%",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        Grid.SetColumn(value, 1);
        header.Children.Add(value);
        stack.Children.Add(header);

        var progress = new ProgressBar
        {
            Height = 5,
            Maximum = 100,
            CornerRadius = new CornerRadius(3),
            Foreground = new SolidColorBrush(color)
        };
        stack.Children.Add(progress);

        panel.Children.Add(new Border
        {
            Padding = new Thickness(10, 7, 10, 8),
            CornerRadius = new CornerRadius(9),
            Background = MetricCardBrush(),
            Child = stack
        });
        return (value, progress);
    }

    private TextBlock AddTextMetric(StackPanel panel, string label, string initialValue, Symbol symbol)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new SymbolIcon(symbol) { Width = 16, Height = 16, Opacity = 0.7 });
        var title = new TextBlock { Text = label, FontSize = 11, Opacity = 0.72, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 1);
        row.Children.Add(title);

        var value = new TextBlock
        {
            Text = initialValue,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(value, 2);
        row.Children.Add(value);

        panel.Children.Add(new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(9),
            Background = MetricCardBrush(),
            Child = row
        });
        return value;
    }

    private Flyout CreateSettingsFlyout()
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 190 };
        panel.Children.Add(new TextBlock { Text = T("widget.configure"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(MetricToggle(T("widget.cpu"), _mainWindow.Settings.WidgetShowCpu, value => _mainWindow.Settings.WidgetShowCpu = value));
        panel.Children.Add(MetricToggle(T("widget.ram"), _mainWindow.Settings.WidgetShowRam, value => _mainWindow.Settings.WidgetShowRam = value));
        panel.Children.Add(MetricToggle(T("widget.disk"), _mainWindow.Settings.WidgetShowDisk, value => _mainWindow.Settings.WidgetShowDisk = value));
        panel.Children.Add(MetricToggle(T("widget.network"), _mainWindow.Settings.WidgetShowNetwork, value => _mainWindow.Settings.WidgetShowNetwork = value));

        var flyout = new Flyout { Content = panel };
        flyout.Closed += (_, _) =>
        {
            Content = CreateLayout();
            _ = UpdateMetricsAsync();
        };
        return flyout;
    }

    private ToggleSwitch MetricToggle(string label, bool isOn, Action<bool> update)
    {
        var toggle = new ToggleSwitch { Header = label, IsOn = isOn };
        toggle.Toggled += (_, _) =>
        {
            update(toggle.IsOn);
            _mainWindow.SettingsService.Save(_mainWindow.Settings);
        };
        return toggle;
    }

    private static Button HeaderButton(Symbol symbol, string label)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new SymbolIcon(symbol) { Width = 13, Height = 13 }
        };
        ToolTipService.SetToolTip(button, label);
        return button;
    }

    private void BeginWindowDrag(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (!args.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(_windowHandle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
    }

    private void ConfigureWindow()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(360, 292));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
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
        catch
        {
            // Window icon is best-effort.
        }
    }

    private async Task UpdateMetricsAsync()
    {
        try
        {
            var metrics = await _mainWindow.PerformanceMonitoring.GetMetricsAsync();
            if (_cpuText != null && _cpuProgress != null)
            {
                _cpuText.Text = $"{metrics.CpuUsagePercent:F0}%";
                _cpuProgress.Value = metrics.CpuUsagePercent;
            }

            if (_ramText != null && _ramProgress != null)
            {
                _ramText.Text = $"{metrics.RamUsagePercent:F0}%";
                _ramProgress.Value = metrics.RamUsagePercent;
            }

            if (_diskText != null)
            {
                _diskText.Text = Formatters.FormatBytes(metrics.DiskFreeBytes);
            }

            if (_networkText != null)
            {
                _networkText.Text = $"D {FormatRate(metrics.DownloadBytesPerSecond)}   U {FormatRate(metrics.UploadBytesPerSecond)}";
            }
        }
        catch
        {
            // Background polling must not interrupt widget actions.
        }
    }

    private void ResetMetricControls()
    {
        _cpuText = null;
        _ramText = null;
        _diskText = null;
        _networkText = null;
        _cpuProgress = null;
        _ramProgress = null;
    }

    private static string FormatRate(double bytesPerSecond)
    {
        return $"{Formatters.FormatBytes((long)Math.Max(0, bytesPerSecond))}/s";
    }

    private static SolidColorBrush WidgetSurfaceBrush()
    {
        return MainWindow.CurrentElementTheme == ElementTheme.Dark
            ? new SolidColorBrush(Color.FromArgb(238, 25, 27, 31))
            : new SolidColorBrush(Color.FromArgb(242, 249, 250, 252));
    }

    private static SolidColorBrush WidgetBorderBrush()
    {
        return MainWindow.CurrentElementTheme == ElementTheme.Dark
            ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
    }

    private static SolidColorBrush MetricCardBrush()
    {
        return MainWindow.CurrentElementTheme == ElementTheme.Dark
            ? new SolidColorBrush(Color.FromArgb(24, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
    }

    private string T(string key) => _mainWindow.Translate(key);
}
