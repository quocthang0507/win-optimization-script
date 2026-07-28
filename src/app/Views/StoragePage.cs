using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace WinOptimizationApp.Views;

public sealed partial class StoragePage : BasePage
{
    private CancellationTokenSource? _diskScanCts;
    private DiskScanResult? _lastDiskScan;
    private TextBox? _storageRootBox;
    private CheckBox? _includeHiddenBox;
    private CheckBox? _includeSystemBox;
    private CheckBox? _followLinksBox;
    private StackPanel? _storageResultPanel;
    private ProgressBar? _storageProgress;
    private TextBlock? _storageProgressText;
    private Button? _storageScanButton;
    private Button? _storageStopButton;
    private Border? _storageProgressCard;
    private TextBox? _diskItemSearchBox;
    private ComboBox? _diskItemTypeFilterBox;
    private ComboBox? _diskItemSizeFilterBox;
    private DiskItemSortColumn _diskItemSortColumn = DiskItemSortColumn.Size;
    private bool _diskItemSortAscending;
    private DateTimeOffset _lastPartialRenderAt = DateTimeOffset.MinValue;

    public StoragePage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderStorageAnalyzerPage();
    }

    private enum DiskItemSortColumn
    {
        Name,
        Size,
        PercentOfParent,
        Files,
        Modified
    }

    private sealed record StorageChartSlice(string Name, string FullPath, long Size, double Percent, Color Color);

    private void RenderStorageAnalyzerPage()
    {
        AddHeader(T("storage.title"), T("storage.subtitle"));

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        _storageRootBox = new TextBox
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
                _storageRootBox.Text = selected[..3];
            }
        };

        _includeHiddenBox = new CheckBox { Content = T("common.hidden"), VerticalAlignment = VerticalAlignment.Bottom };
        _includeSystemBox = new CheckBox { Content = T("common.system"), VerticalAlignment = VerticalAlignment.Bottom };
        _followLinksBox = new CheckBox { Content = T("common.followLinks"), VerticalAlignment = VerticalAlignment.Bottom };
        ToolTipService.SetToolTip(_followLinksBox, T("storage.followTooltip"));

        _storageResultPanel = new StackPanel { Spacing = 14 };
        _storageProgress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 5, Visibility = Visibility.Collapsed };
        _storageProgressText = new TextBlock { Opacity = 0.72, TextWrapping = TextWrapping.Wrap };

        _storageScanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await StartDiskScanAsync(_storageRootBox.Text);
        });
        _storageScanButton.VerticalAlignment = VerticalAlignment.Bottom;

        _storageStopButton = ActionButton(T("common.stop"), Symbol.Stop, (_, _) =>
        {
            _diskScanCts?.Cancel();
            _storageStopButton?.IsEnabled = false;

            _storageProgressText?.Text = T("storage.stopping");

            MainWindow.SetStatusText(T("storage.stopping"));
        });
        _storageStopButton.IsEnabled = false;

        var browseButton = ActionButton(T("common.browse"), Symbol.OpenFile, async (_, _) =>
        {
            var folder = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _storageRootBox.Text = folder;
            }
        });
        browseButton.VerticalAlignment = VerticalAlignment.Bottom;

        var commandGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition());
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_storageRootBox, 0);
        commandGrid.Children.Add(_storageRootBox);
        Grid.SetColumn(driveBox, 1);
        commandGrid.Children.Add(driveBox);
        Grid.SetColumn(_includeHiddenBox, 2);
        commandGrid.Children.Add(_includeHiddenBox);
        Grid.SetColumn(_includeSystemBox, 3);
        commandGrid.Children.Add(_includeSystemBox);
        Grid.SetColumn(_followLinksBox, 4);
        commandGrid.Children.Add(_followLinksBox);
        Grid.SetColumn(browseButton, 5);
        commandGrid.Children.Add(browseButton);
        Grid.SetColumn(_storageScanButton, 6);
        commandGrid.Children.Add(_storageScanButton);

        if (!SystemStatusService.IsAdministrator())
        {
            MainContent.Children.Add(CreateAdminWarningBanner(T("storage.adminRequiredTitle"), T("storage.adminRequiredDesc")));
        }

        _storageProgressCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Visibility = Visibility.Collapsed
        };

        var progressStack = new StackPanel { Spacing = 10 };
        progressStack.Children.Add(new TextBlock { Text = T("storage.scanning"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        progressStack.Children.Add(_storageProgress);
        progressStack.Children.Add(_storageProgressText);

        var progressButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        progressButtons.Children.Add(_storageStopButton);
        progressStack.Children.Add(progressButtons);

        _storageProgressCard.Child = progressStack;

        MainContent.Children.Add(commandGrid);
        MainContent.Children.Add(CloudSourcesPanel(CloudStorageDetector.Detect()));
        MainContent.Children.Add(StorageResultFilterPanel());
        MainContent.Children.Add(_storageProgressCard);
        MainContent.Children.Add(_storageResultPanel);

        if (_lastDiskScan is not null)
        {
            RenderStorageResults(_storageResultPanel, _lastDiskScan);
        }
    }

    private StackPanel StorageResultFilterPanel()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(SectionTitle(T("storage.resultFilters")));

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _diskItemSearchBox = new TextBox
        {
            PlaceholderText = T("storage.searchPlaceholder"),
            Height = 36,
            MinWidth = 300
        };
        _diskItemSearchBox.TextChanged += (_, _) => RenderLastStorageResults();
        Grid.SetColumn(_diskItemSearchBox, 0);
        grid.Children.Add(_diskItemSearchBox);

        _diskItemTypeFilterBox = new ComboBox
        {
            Header = T("storage.itemType"),
            MinWidth = 150
        };
        _diskItemTypeFilterBox.Items.Add(T("storage.allItems"));
        _diskItemTypeFilterBox.Items.Add(T("storage.foldersOnly"));
        _diskItemTypeFilterBox.Items.Add(T("storage.filesOnly"));
        _diskItemTypeFilterBox.SelectedIndex = 0;
        _diskItemTypeFilterBox.SelectionChanged += (_, _) => RenderLastStorageResults();
        Grid.SetColumn(_diskItemTypeFilterBox, 1);
        grid.Children.Add(_diskItemTypeFilterBox);

        _diskItemSizeFilterBox = new ComboBox
        {
            Header = T("storage.minSize"),
            MinWidth = 150
        };
        _diskItemSizeFilterBox.Items.Add(T("storage.minSizeAll"));
        _diskItemSizeFilterBox.Items.Add(T("storage.minSize1Mb"));
        _diskItemSizeFilterBox.Items.Add(T("storage.minSize100Mb"));
        _diskItemSizeFilterBox.Items.Add(T("storage.minSize1Gb"));
        _diskItemSizeFilterBox.SelectedIndex = 0;
        _diskItemSizeFilterBox.SelectionChanged += (_, _) => RenderLastStorageResults();
        Grid.SetColumn(_diskItemSizeFilterBox, 2);
        grid.Children.Add(_diskItemSizeFilterBox);

        var resetButton = ActionButton(T("common.resetFilters"), Symbol.Refresh, (_, _) => ResetStorageResultFilters());
        Grid.SetColumn(resetButton, 3);
        grid.Children.Add(resetButton);

        panel.Children.Add(grid);
        return panel;
    }

    private void ResetStorageResultFilters()
    {
        if (_diskItemSearchBox is not null)
        {
            _diskItemSearchBox.Text = string.Empty;
        }

        if (_diskItemTypeFilterBox is not null)
        {
            _diskItemTypeFilterBox.SelectedIndex = 0;
        }

        if (_diskItemSizeFilterBox is not null)
        {
            _diskItemSizeFilterBox.SelectedIndex = 0;
        }

        RenderLastStorageResults();
    }

    private void RenderLastStorageResults()
    {
        if (_lastDiskScan is not null && _storageResultPanel is not null)
        {
            RenderStorageResults(_storageResultPanel, _lastDiskScan);
        }
    }

    private Border CloudSourcesPanel(IReadOnlyList<CloudStorageLocation> locations)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(SectionTitle(T("storage.cloudSources")));
        stack.Children.Add(InfoBlock(T("storage.cloudSourcesDesc")));

        foreach (var location in locations)
        {
            stack.Children.Add(CloudSourceRow(location));
        }

        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = stack
        };
    }

    private Grid CloudSourceRow(CloudStorageLocation location)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 38 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badgeColor = location.IsDetected ? Colors.SeaGreen : Colors.Gray;
        var badge = new Border
        {
            Padding = new Thickness(0, 5, 0, 5),
            CornerRadius = new CornerRadius(6),
            Background = Brush(Color.FromArgb(38, badgeColor.R, badgeColor.G, badgeColor.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = location.IsDetected ? T("storage.detected") : T("storage.notFound"),
                Foreground = Brush(badgeColor),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        row.Children.Add(badge);

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = location.DisplayName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = location.IsDetected ? location.Path : T("storage.cloudNotFoundDetail"),
            Opacity = 0.72,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        ToolTipService.SetToolTip(text, location.IsDetected ? location.Path : T("storage.cloudNotFoundDetail"));
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        var useButton = new Button
        {
            Content = T("storage.useSource"),
            MinWidth = 96,
            IsEnabled = location.IsDetected
        };
        ToolTipService.SetToolTip(useButton, T("storage.useSource"));
        useButton.Click += (_, _) =>
        {
            _storageRootBox?.Text = location.Path;
        };
        Grid.SetColumn(useButton, 2);
        row.Children.Add(useButton);

        return row;
    }

    private async Task StartDiskScanAsync(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            await MainWindow.ShowDialogAsync_Internal(T("storage.cleanupReview"), InfoBlock(T("storage.selectAtLeastOne")), T("common.close"));
            return;
        }

        if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
        {
            await MainWindow.ShowDialogAsync_Internal(T("storage.pathNotFound"), InfoBlock(rootPath), T("common.close"));
            return;
        }

        _storageRootBox?.Text = rootPath;

        _diskScanCts?.Cancel();
        _diskScanCts = new CancellationTokenSource();
        _storageScanButton!.IsEnabled = false;
        _storageStopButton!.IsEnabled = true;
        _storageProgressCard!.Visibility = Visibility.Visible;
        _storageProgress!.Visibility = Visibility.Visible;
        _storageProgress.IsIndeterminate = true;
        _storageResultPanel!.Children.Clear();
        _lastPartialRenderAt = DateTimeOffset.MinValue;
        MainWindow.SetStatusText(T("storage.scanning"));

        var options = new DiskScanOptions(
            rootPath,
            _includeHiddenBox?.IsChecked == true,
            _includeSystemBox?.IsChecked == true,
            _followLinksBox?.IsChecked == true,
            ExcludedPaths: MainWindow.Settings.ProtectedPaths);

        var scanFinished = false;
        var scanProgress = new Progress<DiskScanProgress>(value =>
        {
            if (scanFinished)
            {
                return;
            }

            _storageProgressText!.Text = F("storage.progress", Formatters.FormatBytes(value.TotalBytes), value.FileCount, value.FolderCount, value.SkippedCount, value.CurrentPath);
            if (value.PartialResult is not null && ShouldRenderPartialResult())
            {
                _lastDiskScan = value.PartialResult;
                RenderStorageResults(_storageResultPanel, value.PartialResult);
            }
        });

        try
        {
            var result = await MainWindow.DiskAnalysis.ScanAsync(options, scanProgress, _diskScanCts.Token);
            scanFinished = true;
            _lastDiskScan = result;
            RenderStorageResults(_storageResultPanel, result);
            _storageProgressText!.Text = result.IsPartial
                ? T("storage.partialResult")
                : F("storage.completedIn", (result.FinishedAt - result.StartedAt).TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _storageProgressText!.Text = T("storage.scanCanceled");
            _storageResultPanel.Children.Add(InfoBlock(T("storage.scanCanceledDetail")));
        }
        catch (Exception ex)
        {
            _storageProgressText!.Text = ex.Message;
            _storageResultPanel.Children.Add(InfoBlock(F("storage.scanFailed", ex.Message)));
        }
        finally
        {
            scanFinished = true;
            _storageProgress.IsIndeterminate = false;
            _storageProgress.Visibility = Visibility.Collapsed;
            _storageProgressCard.Visibility = Visibility.Collapsed;
            _storageScanButton.IsEnabled = true;
            _storageStopButton.IsEnabled = false;
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private bool ShouldRenderPartialResult()
    {
        var now = DateTimeOffset.Now;
        if ((now - _lastPartialRenderAt).TotalMilliseconds < 600)
        {
            return false;
        }

        _lastPartialRenderAt = now;
        return true;
    }

    private void RenderStorageResults(StackPanel resultPanel, DiskScanResult result)
    {
        resultPanel.Children.Clear();

        var summary = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.RowDefinitions.Add(new RowDefinition());
        var largestFolders = MainWindow.DiskAnalysis.GetLargestDirectories(result, 1);
        var largestFolder = largestFolders.Count > 0 ? largestFolders[0] : null;
        AddMetric(summary, 0, 0, T("storage.scanned"), Formatters.FormatBytes(result.TotalBytes), F("storage.filesFolders", result.FileCount, result.FolderCount), Colors.SteelBlue);
        AddMetric(summary, 0, 1, T("storage.largestFolder"), largestFolder is null ? T("common.none") : Formatters.FormatBytes(largestFolder.Size), largestFolder?.FullPath ?? result.Root.FullPath, Colors.DarkCyan);
        AddMetric(summary, 0, 2, T("storage.skipped"), $"{result.SkippedCount:N0}", F("storage.errors", result.Errors.Count), result.Errors.Count > 0 ? Colors.DarkOrange : Colors.SeaGreen);
        resultPanel.Children.Add(summary);
        resultPanel.Children.Add(StorageResultTabs(result));
    }

    private TabView StorageResultTabs(DiskScanResult result)
    {
        var tabs = new TabView
        {
            IsAddTabButtonVisible = false,
            CanDragTabs = false,
            TabWidthMode = TabViewWidthMode.SizeToContent,
            MinHeight = 420
        };

        tabs.TabItems.Add(new TabViewItem
        {
            Header = T("storage.chart"),
            IsClosable = false,
            Content = StorageChartTab(result)
        });

        tabs.TabItems.Add(new TabViewItem
        {
            Header = T("storage.details"),
            IsClosable = false,
            Content = StorageDetailsTab(result)
        });

        tabs.TabItems.Add(new TabViewItem
        {
            Header = T("storage.discover"),
            IsClosable = false,
            Content = StorageDiscoverTab(result)
        });

        return tabs;
    }

    private StackPanel StorageChartTab(DiskScanResult result)
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 12, 0, 0) };
        var slices = BuildChartSlices(result);
        if (slices.Count == 0)
        {
            panel.Children.Add(InfoBlock(T("storage.chartEmpty")));
            return panel;
        }

        var chartGrid = new Grid { ColumnSpacing = 24, RowSpacing = 12 };
        chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var chartView = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Width = 340,
            Height = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = StorageDonutChart(slices, result.TotalBytes, T("storage.total"))
        };
        chartGrid.Children.Add(chartView);

        var legend = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var slice in slices)
        {
            legend.Children.Add(StorageChartLegendRow(slice));
        }

        var legendScroll = new ScrollViewer
        {
            Content = legend,
            MaxHeight = 340,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Grid.SetColumn(legendScroll, 1);
        chartGrid.Children.Add(legendScroll);
        panel.Children.Add(chartGrid);
        return panel;
    }

    private List<StorageChartSlice> BuildChartSlices(DiskScanResult result)
    {
        var palette = StorageChartPalette();
        var visibleItems = MainWindow.DiskAnalysis.FlattenVisibleTree(result.Root, 24)
            .Where(item => item.Size > 0)
            .OrderByDescending(item => item.Size)
            .ToList();
        if (visibleItems.Count == 0 || result.TotalBytes <= 0)
        {
            return [];
        }

        var maxPrimarySlices = Math.Min(10, Math.Max(1, palette.Count - 1));
        var slices = visibleItems
            .Take(maxPrimarySlices)
            .Select((item, index) => new StorageChartSlice(
                item.Name,
                item.FullPath,
                item.Size,
                item.Size * 100d / result.TotalBytes,
                palette[index % palette.Count]))
            .ToList();

        var shownBytes = slices.Sum(slice => slice.Size);
        var remainingBytes = Math.Max(0, result.TotalBytes - shownBytes);
        if (remainingBytes > 0)
        {
            slices.Add(new StorageChartSlice(
                T("storage.other"),
                result.Root.FullPath,
                remainingBytes,
                remainingBytes * 100d / result.TotalBytes,
                Colors.Gray));
        }

        return slices;
    }

    private static Canvas StorageDonutChart(IReadOnlyList<StorageChartSlice> slices, long totalBytes, string totalLabel)
    {
        const double size = 320;
        const double center = size / 2;
        const double outerRadius = 142;
        const double innerRadius = 86;
        var canvas = new Canvas
        {
            Width = size,
            Height = size
        };

        var track = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = outerRadius * 2,
            Height = outerRadius * 2,
            Stroke = Brush(Color.FromArgb(24, 128, 128, 128)),
            StrokeThickness = outerRadius - innerRadius,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(track, center - outerRadius);
        Canvas.SetTop(track, center - outerRadius);
        canvas.Children.Add(track);

        var startAngle = -90d;
        var consumedAngle = 0d;
        var sliceTotal = Math.Max(1d, slices.Sum(slice => slice.Size));
        for (var index = 0; index < slices.Count; index++)
        {
            var slice = slices[index];
            var sweepAngle = index == slices.Count - 1
                ? Math.Max(0.1, 360d - consumedAngle)
                : Math.Max(0.4, Math.Min(359.99, slice.Size * 360d / sliceTotal));
            sweepAngle = Math.Min(sweepAngle, 359.99);
            var path = StorageDonutSlice(center, outerRadius, innerRadius, startAngle, sweepAngle, slice.Color, index);
            ToolTipService.SetToolTip(path, $"{slice.Name}\n{Formatters.FormatBytes(slice.Size)} ({slice.Percent:N1}%)");
            canvas.Children.Add(path);
            startAngle += sweepAngle;
            consumedAngle += sweepAngle;
        }

        var total = new TextBlock
        {
            Text = Formatters.FormatBytes(totalBytes),
            Width = 150,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(total, center - 75);
        Canvas.SetTop(total, center - 20);
        canvas.Children.Add(total);

        var label = new TextBlock
        {
            Text = totalLabel,
            Width = 150,
            Opacity = 0.68,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(label, center - 75);
        Canvas.SetTop(label, center + 12);
        canvas.Children.Add(label);

        return canvas;
    }

    private static Microsoft.UI.Xaml.Shapes.Path StorageDonutSlice(double center, double outerRadius, double innerRadius, double startAngle, double sweepAngle, Color color, int index)
    {
        var outerStart = PointOnCircle(center, outerRadius, startAngle);
        var outerEnd = PointOnCircle(center, outerRadius, startAngle + sweepAngle);
        var innerEnd = PointOnCircle(center, innerRadius, startAngle + sweepAngle);
        var innerStart = PointOnCircle(center, innerRadius, startAngle);
        var figure = new PathFigure
        {
            StartPoint = outerStart,
            IsClosed = true
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = outerEnd,
            Size = new Windows.Foundation.Size(outerRadius, outerRadius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        figure.Segments.Add(new LineSegment { Point = innerEnd });
        figure.Segments.Add(new ArcSegment
        {
            Point = innerStart,
            Size = new Windows.Foundation.Size(innerRadius, innerRadius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Counterclockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var transform = new ScaleTransform
        {
            ScaleX = 0.94,
            ScaleY = 0.94,
            CenterX = center,
            CenterY = center
        };
        var path = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Fill = Brush(color),
            Stroke = ThemeChartStrokeBrush(),
            StrokeThickness = 2,
            Opacity = 0,
            RenderTransform = transform
        };

        var storyboard = new Storyboard();
        var beginTime = TimeSpan.FromMilliseconds(index * 70);
        AddSliceAnimation(storyboard, path, "Opacity", 0, 1, beginTime);
        AddSliceAnimation(storyboard, transform, "ScaleX", 0.94, 1, beginTime);
        AddSliceAnimation(storyboard, transform, "ScaleY", 0.94, 1, beginTime);
        path.Loaded += (_, _) => storyboard.Begin();
        return path;
    }

    private static Grid StorageChartLegendRow(StorageChartSlice slice)
    {
        var row = new Grid { ColumnSpacing = 10, MinHeight = 44 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        ToolTipService.SetToolTip(row, slice.FullPath);

        var swatch = new Rectangle
        {
            Width = 14,
            Height = 14,
            RadiusX = 3,
            RadiusY = 3,
            Fill = Brush(slice.Color),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(swatch);

        var labelStack = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        labelStack.Children.Add(new TextBlock
        {
            Text = slice.Name,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        labelStack.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Max(0, Math.Min(100, slice.Percent)),
            Height = 6,
            IsHitTestVisible = false,
            Foreground = Brush(slice.Color)
        });
        Grid.SetColumn(labelStack, 1);
        row.Children.Add(labelStack);
        row.Children.Add(CellText(Formatters.FormatBytes(slice.Size), 2));
        row.Children.Add(CellText($"{slice.Percent:N1}%", 3));
        return row;
    }

    private static void AddSliceAnimation(Storyboard storyboard, DependencyObject target, string property, double from, double to, TimeSpan beginTime)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(280)),
            BeginTime = beginTime,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private static Windows.Foundation.Point PointOnCircle(double center, double radius, double angleDegrees)
    {
        var angleRadians = angleDegrees * Math.PI / 180d;
        return new Windows.Foundation.Point(
            center + (radius * Math.Cos(angleRadians)),
            center + (radius * Math.Sin(angleRadians)));
    }

    private static IReadOnlyList<Color> StorageChartPalette()
    {
        return
        [
            Colors.SteelBlue,
            Colors.SeaGreen,
            Colors.DarkOrange,
            Colors.IndianRed,
            Colors.DarkCyan,
            Colors.Goldenrod,
            Colors.MediumOrchid,
            Colors.CornflowerBlue,
            Colors.Teal,
            Colors.Peru,
            Colors.SlateBlue,
            Colors.RosyBrown
        ];
    }

    private StackPanel StorageDetailsTab(DiskScanResult result)
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 12, 0, 0) };

        var candidatePanel = new StackPanel { Spacing = 8 };
        var candidates = StorageCleanupService.CreateCandidates(result)
            .Where(candidate => StorageCleanupService.IsSafeCandidate(candidate, out _, MainWindow.Settings.ProtectedPaths))
            .ToList();
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

            panel.Children.Add(candidatePanel);
        }

        var diskItems = MainWindow.DiskAnalysis.FlattenVisibleTree(result.Root, 400);
        var filteredDiskItems = FilterDiskItems(diskItems).ToList();
        panel.Children.Add(SectionTitle(F("storage.folderTreeCount", filteredDiskItems.Count, diskItems.Count)));
        panel.Children.Add(StorageDiskItemHeaderRow());
        foreach (var item in SortDiskItems(filteredDiskItems, result.Root).Take(160))
        {
            panel.Children.Add(StorageDiskItemRow(item, result.Root));
        }

        if (filteredDiskItems.Count == 0)
        {
            panel.Children.Add(InfoBlock(T("common.noMatches")));
        }
        else if (filteredDiskItems.Count > 160)
        {
            panel.Children.Add(new TextBlock { Text = F("preview.moreTargets", filteredDiskItems.Count - 160), Opacity = 0.65, Margin = new Thickness(0, 6, 0, 0) });
        }

        panel.Children.Add(SectionTitle(T("storage.fileTypes")));
        panel.Children.Add(StorageHeaderRow(T("storage.extension"), T("storage.size"), T("storage.count"), T("storage.lastModified"), T("storage.largest")));
        foreach (var type in result.FileTypes.Take(40))
        {
            panel.Children.Add(StorageFileTypeRow(type));
        }

        if (result.Errors.Count > 0)
        {
            panel.Children.Add(SectionTitle(T("storage.skippedErrors")));
            foreach (var error in result.Errors.Take(20))
            {
                panel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            }
        }

        return panel;
    }

    private StackPanel StorageDiscoverTab(DiskScanResult result)
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(InfoBlock(T("storage.discoverDescription")));

        panel.Children.Add(SectionTitle(T("storage.fileAge")));
        var ageTotalBytes = Math.Max(1L, result.FileAgeSummaries.Sum(summary => summary.TotalBytes));
        foreach (var summary in result.FileAgeSummaries)
        {
            panel.Children.Add(StorageFileAgeRow(summary, ageTotalBytes));
        }

        if (result.FileAgeSummaries.Count == 0)
        {
            panel.Children.Add(InfoBlock(T("storage.noDiscoveryData")));
        }

        var discoveries = new TabView
        {
            IsAddTabButtonVisible = false,
            CanDragTabs = false,
            TabWidthMode = TabViewWidthMode.SizeToContent,
            MinHeight = 360
        };
        discoveries.TabItems.Add(DiscoveryTab(T("storage.largestFiles"), result.LargestFiles, result));
        discoveries.TabItems.Add(DiscoveryTab(T("storage.newestFiles"), result.NewestFiles, result));
        discoveries.TabItems.Add(DiscoveryTab(T("storage.oldestFiles"), result.OldestFiles, result));
        discoveries.TabItems.Add(DiscoveryTab(T("storage.developerArtifacts"), result.DeveloperArtifacts, result));
        panel.Children.Add(discoveries);
        return panel;
    }

    private TabViewItem DiscoveryTab(string header, IReadOnlyList<DiskItem> source, DiskScanResult result)
    {
        var content = new StackPanel { Spacing = 4, Padding = new Thickness(0, 10, 0, 0) };
        var filtered = FilterDiskItems(source).Take(40).ToList();
        content.Children.Add(StorageDiscoveryHeaderRow());
        foreach (var item in filtered)
        {
            content.Children.Add(StorageDiscoveryRow(item, result.Root));
        }

        if (filtered.Count == 0)
        {
            content.Children.Add(InfoBlock(T("common.noMatches")));
        }

        return new TabViewItem
        {
            Header = header,
            IsClosable = false,
            Content = content
        };
    }

    private Grid StorageFileAgeRow(FileAgeSummary summary, long totalBytes)
    {
        var row = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(8, 7, 8, 7),
            Background = ThemeCardBackground()
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        row.Children.Add(CellText(FileAgeLabel(summary.Range), 0));
        var percentage = Math.Max(0, Math.Min(100, summary.TotalBytes * 100d / totalBytes));
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = percentage,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        row.Children.Add(CellText(Formatters.FormatBytes(summary.TotalBytes), 2));
        row.Children.Add(CellText(F("storage.fileCount", summary.Count), 3));
        return row;
    }

    private string FileAgeLabel(FileAgeRange range)
    {
        return range switch
        {
            FileAgeRange.Last7Days => T("storage.ageLast7Days"),
            FileAgeRange.Last30Days => T("storage.ageLast30Days"),
            FileAgeRange.LastYear => T("storage.ageLastYear"),
            FileAgeRange.Older => T("storage.ageOlder"),
            _ => T("storage.ageUnknown")
        };
    }

    private Grid StorageDiscoveryHeaderRow()
    {
        var row = StorageDiscoveryGrid();
        row.Children.Add(DiscoveryHeaderText(T("storage.name"), 0));
        row.Children.Add(DiscoveryHeaderText(T("storage.size"), 1));
        row.Children.Add(DiscoveryHeaderText(T("storage.modified"), 2));
        row.Children.Add(DiscoveryHeaderText(T("storage.action"), 3));
        return row;
    }

    private Grid StorageDiscoveryRow(DiskItem item, DiskItem root)
    {
        var row = StorageDiscoveryGrid();
        row.Children.Add(CellText(item.Name, 0, item.FullPath));
        row.Children.Add(CellText(Formatters.FormatBytes(item.Size), 1));
        row.Children.Add(CellText(FormatStorageDate(item.LastModified), 2));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("storage.openInExplorer"), (_, _) => OpenDiskItemInExplorer(item)));
        var manualCandidate = CreateManualCandidate(item);
        if (StorageCleanupService.IsSafeCandidate(manualCandidate, out _, MainWindow.Settings.ProtectedPaths))
        {
            actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
            {
                await ReviewStorageCandidatesAsync([manualCandidate]);
            }));
        }

        Grid.SetColumn(actions, 3);
        row.Children.Add(actions);
        row.ContextFlyout = CreateDiskItemContextMenu(item, root);
        return row;
    }

    private static TextBlock DiscoveryHeaderText(string text, int column)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(block, column);
        return block;
    }

    private Grid StorageDiskItemHeaderRow()
    {
        var row = StorageDiskItemGrid();
        row.Children.Add(SortHeaderButton(T("storage.name"), DiskItemSortColumn.Name, 0));
        row.Children.Add(SortHeaderButton(T("storage.size"), DiskItemSortColumn.Size, 1));
        row.Children.Add(SortHeaderButton(T("storage.percentOfParent"), DiskItemSortColumn.PercentOfParent, 2));
        row.Children.Add(SortHeaderButton(T("storage.files"), DiskItemSortColumn.Files, 3));
        row.Children.Add(SortHeaderButton(T("storage.modified"), DiskItemSortColumn.Modified, 4));
        row.Children.Add(HeaderText(T("storage.action"), 5));
        return row;

        static TextBlock HeaderText(string text, int column)
        {
            var block = new TextBlock
            {
                Text = text,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.75,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(block, column);
            return block;
        }
    }

    private Button SortHeaderButton(string text, DiskItemSortColumn column, int gridColumn)
    {
        var isActive = _diskItemSortColumn == column;
        var button = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = $"{text}{(isActive ? (_diskItemSortAscending ? " ^" : " v") : string.Empty)}"
        };
        ToolTipService.SetToolTip(button, F("storage.sortBy", text));
        button.Click += (_, _) =>
        {
            if (_diskItemSortColumn == column)
            {
                _diskItemSortAscending = !_diskItemSortAscending;
            }
            else
            {
                _diskItemSortColumn = column;
                _diskItemSortAscending = column is DiskItemSortColumn.Name or DiskItemSortColumn.Modified;
            }

            if (_lastDiskScan is not null && _storageResultPanel is not null)
            {
                RenderStorageResults(_storageResultPanel, _lastDiskScan);
            }
        };

        Grid.SetColumn(button, gridColumn);
        return button;
    }

    private IEnumerable<DiskItem> FilterDiskItems(IEnumerable<DiskItem> items)
    {
        var query = _diskItemSearchBox?.Text?.Trim() ?? string.Empty;
        var typeIndex = _diskItemTypeFilterBox?.SelectedIndex ?? 0;
        var minimumSize = (_diskItemSizeFilterBox?.SelectedIndex ?? 0) switch
        {
            1 => 1024L * 1024L,
            2 => 100L * 1024L * 1024L,
            3 => 1024L * 1024L * 1024L,
            _ => 0L
        };

        return items.Where(item =>
            MatchesDiskItemQuery(item, query)
            && MatchesDiskItemType(item, typeIndex)
            && item.Size >= minimumSize);
    }

    private static bool MatchesDiskItemQuery(DiskItem item, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Extension.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.ScanStatus.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDiskItemType(DiskItem item, int typeIndex)
    {
        return typeIndex switch
        {
            1 => item.IsDirectory,
            2 => !item.IsDirectory,
            _ => true
        };
    }

    private IEnumerable<DiskItem> SortDiskItems(IReadOnlyList<DiskItem> items, DiskItem root)
    {
        return _diskItemSortColumn switch
        {
            DiskItemSortColumn.Name => _diskItemSortAscending
                ? items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                : items.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
            DiskItemSortColumn.Size => _diskItemSortAscending
                ? items.OrderBy(item => item.Size)
                : items.OrderByDescending(item => item.Size),
            DiskItemSortColumn.PercentOfParent => _diskItemSortAscending
                ? items.OrderBy(item => GetPercentOfParent(item, root))
                : items.OrderByDescending(item => GetPercentOfParent(item, root)),
            DiskItemSortColumn.Files => _diskItemSortAscending
                ? items.OrderBy(item => item.FileCount)
                : items.OrderByDescending(item => item.FileCount),
            DiskItemSortColumn.Modified => _diskItemSortAscending
                ? items.OrderBy(item => item.LastModified)
                : items.OrderByDescending(item => item.LastModified),
            _ => items.OrderByDescending(item => item.Size)
        };
    }

    private static Grid StorageHeaderRow(string first, string second, string third, string fourth, string fifth)
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

    private Grid StorageDiskItemRow(DiskItem item, DiskItem root)
    {
        var row = StorageDiskItemGrid();
        var percentOfParent = GetPercentOfParent(item, root);
        var nameCell = CellText($"{(item.IsDirectory ? "[D]" : "[F]")} {item.Name}", 0, item.FullPath);
        if (item.FullPath != root.FullPath && percentOfParent >= 50)
        {
            nameCell.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            ToolTipService.SetToolTip(nameCell, $"{item.FullPath}\n{T("storage.dominantItem")}");
        }
        row.Children.Add(nameCell);
        row.Children.Add(CellText(Formatters.FormatBytes(item.Size), 1));
        row.Children.Add(PercentOfParentCell(percentOfParent, 2));
        row.Children.Add(CellText(item.FileCount.ToString("N0"), 3));
        row.Children.Add(CellText(FormatStorageDate(item.LastModified), 4));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("storage.openInExplorer"), (_, _) => OpenDiskItemInExplorer(item)));
        if (item.IsDirectory)
        {
            actions.Children.Add(IconButton(Symbol.Find, T("storage.analyzeThisFolder"), async (_, _) => await AnalyzeDiskItemAsync(item)));
        }
        var manualCandidate = CreateManualCandidate(item);
        if (item.FullPath != root.FullPath && StorageCleanupService.IsSafeCandidate(manualCandidate, out _, MainWindow.Settings.ProtectedPaths))
        {
            actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
            {
                await ReviewStorageCandidatesAsync([manualCandidate]);
            }));
        }
        Grid.SetColumn(actions, 5);
        row.Children.Add(actions);
        row.ContextFlyout = CreateDiskItemContextMenu(item, root);
        if (item.IsDirectory)
        {
            row.DoubleTapped += async (_, _) => await AnalyzeDiskItemAsync(item);
        }
        return row;
    }

    private MenuFlyout CreateDiskItemContextMenu(DiskItem item, DiskItem root)
    {
        var flyout = new MenuFlyout();

        var openItem = new MenuFlyoutItem
        {
            Text = T("storage.openInExplorer"),
            Icon = new SymbolIcon(Symbol.OpenFile)
        };
        openItem.Click += (_, _) => OpenDiskItemInExplorer(item);
        flyout.Items.Add(openItem);

        if (item.IsDirectory)
        {
            var analyzeItem = new MenuFlyoutItem
            {
                Text = T("storage.analyzeThisFolder"),
                Icon = new SymbolIcon(Symbol.Find)
            };
            analyzeItem.Click += async (_, _) => await AnalyzeDiskItemAsync(item);
            flyout.Items.Add(analyzeItem);
        }

        var manualCandidate = CreateManualCandidate(item);
        if (item.FullPath != root.FullPath && StorageCleanupService.IsSafeCandidate(manualCandidate, out _, MainWindow.Settings.ProtectedPaths))
        {
            var cleanupItem = new MenuFlyoutItem
            {
                Text = T("common.addCleanupReview"),
                Icon = new SymbolIcon(Symbol.Add)
            };
            cleanupItem.Click += async (_, _) => await ReviewStorageCandidatesAsync([manualCandidate]);
            flyout.Items.Add(cleanupItem);
        }

        var copyPathItem = new MenuFlyoutItem
        {
            Text = T("storage.copyPath"),
            Icon = new SymbolIcon(Symbol.Copy)
        };
        copyPathItem.Click += (_, _) => CopyPathToClipboard(item.FullPath);
        flyout.Items.Add(copyPathItem);

        return flyout;
    }

    private async Task AnalyzeDiskItemAsync(DiskItem item)
    {
        if (item.IsDirectory)
        {
            await StartDiskScanAsync(item.FullPath);
        }
    }

    private static void OpenDiskItemInExplorer(DiskItem item)
    {
        if (item.IsDirectory)
        {
            MainWindow.OpenFolder_Internal(item.FullPath);
        }
        else
        {
            MainWindow.OpenContainingFolder_Internal(item.FullPath);
        }
    }

    private static void CopyPathToClipboard(string path)
    {
        var package = new DataPackage();
        package.SetText(path);
        Clipboard.SetContent(package);
    }

    private static Grid StorageFileTypeRow(FileTypeSummary summary)
    {
        var row = StorageGrid();
        row.Children.Add(CellText(summary.Extension, 0));
        row.Children.Add(CellText(Formatters.FormatBytes(summary.TotalBytes), 1));
        row.Children.Add(CellText(summary.Count.ToString("N0"), 2));
        row.Children.Add(CellText(FormatStorageDate(summary.LastModified), 3));
        row.Children.Add(CellText(summary.LargestItemPath, 4));
        return row;
    }

    private static Grid PercentOfParentCell(double percent, int column)
    {
        var boundedPercent = Math.Max(0, Math.Min(100, percent));
        var cell = new Grid
        {
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnSpacing = 8
        };
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        cell.ColumnDefinitions.Add(new ColumnDefinition());

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = boundedPercent,
            Height = 12,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        cell.Children.Add(bar);

        var label = new TextBlock
        {
            Text = $"{boundedPercent:N1}%",
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.86
        };
        Grid.SetColumn(label, 1);
        cell.Children.Add(label);

        Grid.SetColumn(cell, column);
        return cell;
    }

    private static double GetPercentOfParent(DiskItem item, DiskItem root)
    {
        return item.FullPath == root.FullPath ? 100 : item.PercentOfParent;
    }

    private Grid StorageCandidateRow(StorageCleanupCandidate candidate, CheckBox checkBox)
    {
        var row = StorageGrid();
        Grid.SetColumn(checkBox, 0);
        row.Children.Add(checkBox);
        row.Children.Add(CellText(candidate.Label, 0, candidate.SourcePath, new Thickness(34, 0, 0, 0)));
        row.Children.Add(CellText(Formatters.FormatBytes(candidate.EstimatedBytes), 1));
        var risk = RiskBadge(candidate.RiskLevel, MainWindow.Localization.RiskName(candidate.RiskLevel));
        Grid.SetColumn(risk, 2);
        row.Children.Add(risk);
        row.Children.Add(CellText(candidate.Reason, 3));
        row.Children.Add(CellText(candidate.CleanupMode == StorageCleanupMode.MoveToRecycleBin ? T("common.recycleBin") : T("common.delete"), 4));
        return row;
    }

    private async Task ReviewStorageCandidatesAsync(List<StorageCleanupCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            await MainWindow.ShowDialogAsync_Internal(T("storage.cleanupReview"), InfoBlock(T("storage.selectAtLeastOne")), T("common.close"));
            return;
        }

        var panel = new StackPanel { Spacing = 10, MaxWidth = 760 };
        panel.Children.Add(new TextBlock
        {
            Text = F("storage.itemSummary", candidates.Count, Formatters.FormatBytes(candidates.Sum(candidate => candidate.EstimatedBytes))),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var listPanel = new StackPanel { Spacing = 6 };
        foreach (var candidate in candidates.Take(100))
        {
            listPanel.Children.Add(new TextBlock
            {
                Text = $"{candidate.Label} / {Formatters.FormatBytes(candidate.EstimatedBytes)} / {candidate.RiskLevel}\n{candidate.SourcePath}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.82
            });
        }

        if (candidates.Count > 100)
        {
            listPanel.Children.Add(new TextBlock { Text = F("storage.moreItems", candidates.Count - 100), Opacity = 0.65 });
        }

        var scrollViewer = new ScrollViewer
        {
            Content = listPanel,
            MaxHeight = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 8, 0, 0)
        };
        panel.Children.Add(scrollViewer);

        var dialog = new ContentDialog
        {
            Title = T("storage.moveQuestion"),
            Content = panel,
            PrimaryButtonText = T("common.move"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        MainWindow.SetStatusText(T("storage.cleaning"));
        var result = await MainWindow.StorageCleanup.CleanupAsync(candidates, MainWindow.Settings.ProtectedPaths);
        await MainWindow.ShowRunResultAsync_Internal(result);
        MainWindow.SetStatusText(T("common.ready"));
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

    private Task<string?> PickFolderAsync()
    {
        var path = FolderPickerHelper.PickFolder(WindowNative.GetWindowHandle(MainWindow));
        return Task.FromResult(path);
    }

    private static Grid StorageGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 7, 8, 7),
            Background = ThemeCardBackground()
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        return grid;
    }

    private static Grid StorageDiskItemGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 7, 8, 7),
            Background = ThemeCardBackground()
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(174) });
        return grid;
    }

    private static Grid StorageDiscoveryGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 7, 8, 7),
            Background = ThemeCardBackground()
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
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
}
