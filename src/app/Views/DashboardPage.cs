using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class DashboardPage : BasePage
{
    private readonly StackPanel _wingetResultPanel;
    private int _renderedRevision = -1;
    private bool _isRefreshing;

    private DispatcherTimer? _liveMetricsTimer;
    private bool _isUpdatingMetrics;
    private TextBlock? _cpuValueText;
    private ProgressBar? _cpuProgressBar;
    private TextBlock? _ramPercentText;
    private TextBlock? _ramValueText;
    private ProgressBar? _ramProgressBar;
    private TextBlock? _diskPercentText;
    private TextBlock? _diskValueText;
    private ProgressBar? _diskProgressBar;
    private TextBlock? _networkDownText;
    private TextBlock? _networkUpText;
    private readonly List<Microsoft.UI.Xaml.Shapes.Rectangle> _cpuSparklineBars = [];
    private readonly Queue<double> _cpuHistory = new();
    private const int SparklineCapacity = 20;

    public DashboardPage(MainWindow mainWindow) : base(mainWindow)
    {
        _wingetResultPanel = new StackPanel { Spacing = 8 };
        Unloaded += (_, _) => StopLiveMonitoring();
    }

    public override Task OnNavigatedToAsync()
    {
        if (_renderedRevision != MainWindow.SessionState.DashboardRevision || MainContent.Children.Count == 0)
        {
            RenderCachedDashboard();
        }

        StartLiveMonitoring();
        return Task.CompletedTask;
    }

    public override void OnNavigatedFrom()
    {
        StopLiveMonitoring();
    }

    private void StartLiveMonitoring()
    {
        if (_liveMetricsTimer != null)
        {
            _liveMetricsTimer.Stop();
        }

        _liveMetricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _liveMetricsTimer.Tick += async (_, _) => await UpdateLiveMetricsAsync();
        _liveMetricsTimer.Start();

        _ = UpdateLiveMetricsAsync();
    }

    private void StopLiveMonitoring()
    {
        if (_liveMetricsTimer != null)
        {
            _liveMetricsTimer.Stop();
            _liveMetricsTimer = null;
        }
    }

    private async Task UpdateLiveMetricsAsync()
    {
        if (_isUpdatingMetrics || _liveMetricsTimer == null)
        {
            return;
        }

        _isUpdatingMetrics = true;
        try
        {
            var metrics = await MainWindow.PerformanceMonitoring.GetMetricsAsync();
            if (_liveMetricsTimer == null)
            {
                return;
            }

            UpdateLiveTelemetryControls(metrics);
        }
        catch
        {
            // Best effort telemetry update; never crash UI
        }
        finally
        {
            _isUpdatingMetrics = false;
        }
    }

    private void UpdateLiveTelemetryControls(SystemMetrics metrics)
    {
        if (_cpuValueText != null)
        {
            _cpuValueText.Text = $"{metrics.CpuUsagePercent:F1}%";
        }
        if (_cpuProgressBar != null)
        {
            _cpuProgressBar.Value = Math.Clamp(metrics.CpuUsagePercent, 0, 100);
            _cpuProgressBar.Foreground = Brush(metrics.CpuUsagePercent > 85 ? Colors.OrangeRed : metrics.CpuUsagePercent > 65 ? Colors.Goldenrod : Colors.MediumPurple);
        }
        UpdateCpuSparkline(metrics.CpuUsagePercent);

        if (_ramPercentText != null)
        {
            _ramPercentText.Text = $"{metrics.RamUsagePercent:F1}%";
        }
        if (_ramValueText != null)
        {
            _ramValueText.Text = $"{Formatters.FormatBytes(metrics.RamUsedBytes)} / {Formatters.FormatBytes(metrics.RamTotalBytes)}";
        }
        if (_ramProgressBar != null)
        {
            _ramProgressBar.Value = Math.Clamp(metrics.RamUsagePercent, 0, 100);
            _ramProgressBar.Foreground = Brush(metrics.RamUsagePercent > 85 ? Colors.OrangeRed : metrics.RamUsagePercent > 70 ? Colors.Goldenrod : Colors.DarkCyan);
        }

        if (_diskPercentText != null)
        {
            _diskPercentText.Text = $"{metrics.DiskUsagePercent:F1}%";
        }
        if (_diskValueText != null)
        {
            var used = metrics.DiskTotalBytes - metrics.DiskFreeBytes;
            _diskValueText.Text = $"{Formatters.FormatBytes(used)} / {Formatters.FormatBytes(metrics.DiskTotalBytes)}";
        }
        if (_diskProgressBar != null)
        {
            _diskProgressBar.Value = Math.Clamp(metrics.DiskUsagePercent, 0, 100);
            _diskProgressBar.Foreground = Brush(metrics.DiskUsagePercent > 90 ? Colors.OrangeRed : metrics.DiskUsagePercent > 75 ? Colors.Goldenrod : Colors.SteelBlue);
        }

        if (_networkDownText != null)
        {
            _networkDownText.Text = $"↓ {FormatSpeed(metrics.DownloadBytesPerSecond)}";
        }
        if (_networkUpText != null)
        {
            _networkUpText.Text = $"↑ {FormatSpeed(metrics.UploadBytesPerSecond)}";
        }
    }

    private void UpdateCpuSparkline(double cpu)
    {
        if (_cpuSparklineBars.Count == 0) return;

        _cpuHistory.Enqueue(cpu);
        while (_cpuHistory.Count > _cpuSparklineBars.Count)
        {
            _cpuHistory.Dequeue();
        }

        var historyArray = _cpuHistory.ToArray();
        var offset = _cpuSparklineBars.Count - historyArray.Length;

        for (int i = 0; i < offset; i++)
        {
            _cpuSparklineBars[i].Height = 2;
            _cpuSparklineBars[i].Opacity = 0.25;
        }

        for (int i = 0; i < historyArray.Length; i++)
        {
            var barIndex = offset + i;
            var val = historyArray[i];
            var height = Math.Max(2.0, Math.Min(28.0, val * 28.0 / 100.0));
            _cpuSparklineBars[barIndex].Height = height;
            _cpuSparklineBars[barIndex].Opacity = 0.35 + (0.65 * (i + 1) / historyArray.Length);
            _cpuSparklineBars[barIndex].Fill = Brush(val > 85 ? Colors.OrangeRed : val > 65 ? Colors.Goldenrod : Colors.MediumPurple);
        }
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        return $"{Formatters.FormatBytes((long)Math.Max(0, bytesPerSec))}/s";
    }

    private void RenderCachedDashboard()
    {
        MainContent.Children.Clear();
        _wingetResultPanel.Children.Clear();
        AddHeader(T("dashboard.title"), T("dashboard.subtitle"));

        var state = MainWindow.SessionState;
        if (state.SystemOverview is not null && state.HealthMetrics is not null)
        {
            var health = DashboardHealthCheckService.Analyze(state.SystemOverview, state.HealthMetrics);
            RenderDashboardData(state.SystemOverview, health);
        }
        else
        {
            var error = state.SystemOverviewError ?? state.HealthMetricsError ?? T("dashboard.loadUnavailable");
            MainContent.Children.Add(new TextBlock
            {
                Text = F("dashboard.loadFailed", error),
                Foreground = Brush(Colors.OrangeRed),
                Margin = new Thickness(0, 16, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            MainContent.Children.Add(ActionButton(
                T("dashboard.healthCheck"),
                Symbol.Refresh,
                async (_, _) => await RefreshDashboardAsync()));
        }

        _renderedRevision = state.DashboardRevision;
    }

    private async Task RefreshDashboardAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        MainWindow.SetStatusText(T("common.loading"));
        try
        {
            await MainWindow.RefreshDashboardStateAsync();
            RenderCachedDashboard();
        }
        finally
        {
            _isRefreshing = false;
            MainWindow.SetStatusText(T("common.ready"), isBusy: false);
        }
    }

    private void RenderDashboardData(DashboardStatus status, HealthCheckResult health)
    {
        MainContent.Children.Add(HealthCheckPanel(health));

        // System Health Section
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var card1 = CreateMetricCardWithIcon(T("dashboard.windows"), status.WindowsVersion, status.PendingReboot ? T("dashboard.pendingReboot") : T("dashboard.noRebootPending"), status.PendingReboot ? Colors.OrangeRed : Colors.SeaGreen, "\uE7F4");
        Grid.SetRow(card1, 0); Grid.SetColumn(card1, 0);
        grid.Children.Add(card1);

        var card2 = CreateMetricCardWithIcon(T("dashboard.administrator"), status.IsAdministrator ? T("dashboard.elevated") : T("dashboard.standardUser"), status.IsAdministrator ? T("dashboard.highRiskEnabled") : T("dashboard.highRiskNeedAdmin"), status.IsAdministrator ? Colors.SeaGreen : Colors.DarkOrange, "\uEA18");
        Grid.SetRow(card2, 0); Grid.SetColumn(card2, 1);
        grid.Children.Add(card2);

        var card3 = CreateMetricCardWithIcon(T("dashboard.systemDrive"), $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} {T("dashboard.free")}", $"{status.SystemDrive} of {Formatters.FormatBytes(status.SystemDriveTotalBytes)}", Colors.SteelBlue, "\uE7F1");
        Grid.SetRow(card3, 1); Grid.SetColumn(card3, 0);
        grid.Children.Add(card3);

        var card4 = CreateMetricCardWithIcon(T("dashboard.uptime"), Formatters.FormatDuration(status.Uptime, MainWindow.Localization.CurrentLanguage), status.WingetAvailable ? T("dashboard.wingetAvailable") : T("dashboard.wingetNotFound"), status.WingetAvailable ? Colors.SeaGreen : Colors.Gray, "\uE916");
        Grid.SetRow(card4, 1); Grid.SetColumn(card4, 1);
        grid.Children.Add(card4);

        MainContent.Children.Add(grid);

        // Live System Telemetry (B3)
        MainContent.Children.Add(LiveMonitorPanel(status));

        // Optimization History & Impact (B3)
        MainContent.Children.Add(OptimizationHistoryPanel());

        MainContent.Children.Add(SystemDetailsPanel(status));
        MainContent.Children.Add(DriveOverviewPanel(status));

        // Quick Actions
        var quick = new AdaptiveWrapPanel { Spacing = 10 };
        quick.Children.Add(ActionButton(T("dashboard.healthCheck"), Symbol.Refresh, async (_, _) => await RefreshDashboardAsync()));
        quick.Children.Add(ActionButton(T("dashboard.scanCleanup"), Symbol.Find, async (_, _) => await MainWindow.NavigateToTagAsync("cleanup")));
        quick.Children.Add(ActionButton(T("dashboard.analyzeStorage"), Symbol.View, async (_, _) => await MainWindow.NavigateToTagAsync("storage")));
        quick.Children.Add(ActionButton(T("dashboard.scanUpdates"), Symbol.Download, async (_, _) => await MainWindow.ScanWingetAsync(_wingetResultPanel)));
        quick.Children.Add(ActionButton(T("dashboard.openLogs"), Symbol.OpenFile, (_, _) => MainWindow.OpenFolder_Internal(MainWindow.Paths.LogsDirectory)));
        quick.Children.Add(ActionButton(T("dashboard.export"), Symbol.Save, async (_, _) => await ExportDashboardAsync(status)));
        quick.Children.Add(ActionButton(T("dashboard.copySpecs"), Symbol.Copy, (_, _) => CopySpecsToClipboard(status)));
        MainContent.Children.Add(quick);

        MainContent.Children.Add(_wingetResultPanel);

        if (!string.IsNullOrWhiteSpace(status.LastReportPath))
        {
            var border = new Border
            {
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = ThemeBorderBrush(),
                Background = ThemeCardBackground()
            };

            var cardGrid = new Grid { ColumnSpacing = 12 };
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition());
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { Spacing = 4 };
            textStack.Children.Add(new TextBlock { Text = T("dashboard.lastReport"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            textStack.Children.Add(new TextBlock { Text = status.LastReportPath, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
            cardGrid.Children.Add(textStack);

            var openBtn = new Button { Content = T("common.open"), MinWidth = 86 };
            ToolTipService.SetToolTip(openBtn, T("common.open"));
            openBtn.Click += (_, _) => MainWindow.OpenFile_Internal(status.LastReportPath);
            Grid.SetColumn(openBtn, 1);
            cardGrid.Children.Add(openBtn);

            var deleteBtn = new Button { Content = T("common.delete"), MinWidth = 86 };
            ToolTipService.SetToolTip(deleteBtn, T("common.delete"));
            deleteBtn.Click += async (_, _) =>
            {
                var confirmDialog = new ContentDialog
                {
                    Title = T("dashboard.deleteConfirmTitle"),
                    Content = new TextBlock { Text = T("dashboard.deleteConfirmBody"), TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = T("common.delete"),
                    CloseButtonText = T("common.cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = MainWindow.Navigation_Internal.XamlRoot
                };

                if (await MainWindow.ShowThemedDialogAsync(confirmDialog) == ContentDialogResult.Primary)
                {
                    try
                    {
                        var logFile = Path.ChangeExtension(status.LastReportPath, ".log");
                        if (File.Exists(status.LastReportPath))
                        {
                            File.Delete(status.LastReportPath);
                        }
                        if (File.Exists(logFile))
                        {
                            File.Delete(logFile);
                        }
                        MainWindow.SetStatusText(T("settings.saved"));
                        await RefreshDashboardAsync();
                    }
                    catch (Exception ex)
                    {
                        await MainWindow.ShowDialogAsync_Internal(T("common.delete"), InfoBlock(ex.Message), T("common.close"));
                    }
                }
            };
            Grid.SetColumn(deleteBtn, 2);
            cardGrid.Children.Add(deleteBtn);

            border.Child = cardGrid;
            MainContent.Children.Add(border);
        }
    }

    private Border HealthCheckPanel(HealthCheckResult health)
    {
        var color = health.Status switch
        {
            "Good" => Colors.SeaGreen,
            "Attention" => Colors.DarkOrange,
            "Critical" => Colors.OrangeRed,
            _ => Colors.Gray
        };

        var root = new Grid { ColumnSpacing = 20 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var scoreStack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        scoreStack.Children.Add(new TextBlock
        {
            Text = T("dashboard.healthCheck"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 18
        });
        scoreStack.Children.Add(new TextBlock
        {
            Text = $"{health.Score:N0}",
            FontSize = 42,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(color)
        });
        scoreStack.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = health.Score,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = Brush(color)
        });
        scoreStack.Children.Add(new TextBlock
        {
            Text = LocalizedHealthStatus(health.Status),
            Foreground = Brush(color),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        if (health.Metrics is not null)
        {
            scoreStack.Children.Add(new TextBlock
            {
                Text = F("dashboard.healthMetrics", Formatters.FormatBytes(health.Metrics.CleanupBytes), health.Metrics.AvailableUpdates, health.Metrics.HighImpactStartupItems),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72
            });
        }
        var reviewButton = new Button { Content = T("dashboard.reviewAll"), HorizontalAlignment = HorizontalAlignment.Left };
        reviewButton.Click += async (_, _) => await ShowHealthReviewAsync(health);
        scoreStack.Children.Add(reviewButton);
        Grid.SetColumn(scoreStack, 0);
        root.Children.Add(scoreStack);

        var details = new StackPanel { Spacing = 10 };
        details.Children.Add(new TextBlock
        {
            Text = T("dashboard.healthRecommendations"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        if (health.Recommendations.Count == 0)
        {
            details.Children.Add(InfoBlock(T("dashboard.healthNoRecommendations")));
        }
        else
        {
            foreach (var recommendation in health.Recommendations.Take(4))
            {
                details.Children.Add(HealthRecommendationRow(recommendation));
            }
        }

        if (health.Findings.Count > 0)
        {
            details.Children.Add(new TextBlock
            {
                Text = F("dashboard.healthFindingCount", health.Findings.Count),
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(details, 1);
        root.Children.Add(details);

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(color),
            Background = Brush(Color.FromArgb(18, color.R, color.G, color.B)),
            Child = root
        };
    }

    private async Task ShowHealthReviewAsync(HealthCheckResult health)
    {
        var panel = new StackPanel { Spacing = 10 };
        if (health.Findings.Count == 0)
        {
            panel.Children.Add(InfoBlock(T("dashboard.healthNoRecommendations")));
        }
        else
        {
            panel.Children.Add(SectionTitle(F("dashboard.healthFindingCount", health.Findings.Count)));
            foreach (var finding in health.Findings)
            {
                var text = new StackPanel { Spacing = 3 };
                text.Children.Add(new TextBlock { Text = LocalizedHealthFindingTitle(finding), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                text.Children.Add(new TextBlock { Text = LocalizedHealthFindingDetail(finding), Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
                text.Children.Add(new TextBlock { Text = $"{MainWindow.Localization.RiskName(finding.Severity)} • {finding.Source}", Opacity = 0.62 });
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = ThemeBorderBrush(),
                    Child = text
                });
            }
        }

        var dialog = new ContentDialog
        {
            Title = T("dashboard.reviewCenter"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 520, MaxWidth = 720 },
            CloseButtonText = T("common.close"),
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };
        await MainWindow.ShowThemedDialogAsync(dialog);
    }

    private Grid HealthRecommendationRow(HealthCheckRecommendation recommendation)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 42 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(RiskBadge(recommendation.Risk, MainWindow.Localization.RiskName(recommendation.Risk)));

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = LocalizedHealthRecommendationTitle(recommendation),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = LocalizedHealthRecommendationDetail(recommendation),
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        var action = new Button
        {
            Content = HealthActionLabel(recommendation.ActionTag, recommendation.ActionLabel),
            MinWidth = 112
        };
        ToolTipService.SetToolTip(action, HealthActionLabel(recommendation.ActionTag, recommendation.ActionLabel));
        action.Click += async (_, _) => await HandleHealthActionAsync(recommendation.ActionTag);
        Grid.SetColumn(action, 2);
        row.Children.Add(action);

        return row;
    }

    private string LocalizedHealthRecommendationTitle(HealthCheckRecommendation recommendation)
    {
        return LocalizedOrFallback($"health.recommendation.{recommendation.Id}.title", recommendation.Title);
    }

    private string LocalizedHealthFindingTitle(HealthCheckFinding finding)
    {
        return LocalizedOrFallback($"health.finding.{finding.Id}.title", finding.Title);
    }

    private string LocalizedHealthFindingDetail(HealthCheckFinding finding)
    {
        return LocalizedOrFallback($"health.finding.{finding.Id}.detail", finding.Detail);
    }

    private string LocalizedHealthRecommendationDetail(HealthCheckRecommendation recommendation)
    {
        return LocalizedOrFallback($"health.recommendation.{recommendation.Id}.detail", recommendation.Detail);
    }

    private string LocalizedOrFallback(string key, string fallback)
    {
        var value = T(key);
        return value == key ? fallback : value;
    }

    private async Task HandleHealthActionAsync(string actionTag)
    {
        switch (actionTag)
        {
            case "cleanup":
            case "storage":
            case "startup":
            case "updates":
                await MainWindow.NavigateToTagAsync(actionTag);
                break;
            case "restartGuidance":
                await MainWindow.ShowDialogAsync_Internal(T("dashboard.restartGuidance"),
                    InfoBlock(T("dashboard.restartInstructions")), T("common.close"));
                break;
            default:
                await MainWindow.ShowDialogAsync_Internal(T("dashboard.healthCheck"), InfoBlock(T("dashboard.healthManualAction")), T("common.close"));
                break;
        }
    }

    private string HealthActionLabel(string actionTag, string fallback)
    {
        return actionTag switch
        {
            "cleanup" => T("dashboard.scanCleanup"),
            "storage" => T("dashboard.analyzeStorage"),
            "startup" => T("startup.scan"),
            "updates" => T("dashboard.scanUpdates"),
            "restartGuidance" => T("dashboard.restartGuidance"),
            _ => fallback
        };
    }

    private string LocalizedHealthStatus(string status)
    {
        return status switch
        {
            "Good" => T("dashboard.healthGood"),
            "Attention" => T("dashboard.healthAttention"),
            "Critical" => T("dashboard.healthCritical"),
            _ => status
        };
    }

    private async Task ExportDashboardAsync(WinOptimizationApp.Models.DashboardStatus status)
    {
        try
        {
            var path = await DashboardExportService.SaveMarkdownAsync(status, MainWindow.Paths.LogsDirectory, MainWindow.Localization.CurrentLanguage);
            MainWindow.SetStatusText(F("dashboard.exported", path), isBusy: false);
            await MainWindow.ShowDialogAsync_Internal(T("dashboard.exportedTitle"), InfoBlock(path), T("common.close"));
        }
        catch (Exception ex)
        {
            await MainWindow.ShowDialogAsync_Internal(T("dashboard.exportFailed"), InfoBlock(ex.Message), T("common.close"));
        }
    }

    private void CopySpecsToClipboard(WinOptimizationApp.Models.DashboardStatus status)
    {
        try
        {
            var summary = DashboardExportService.FormatSystemSpecsSummary(status, MainWindow.Localization.CurrentLanguage);
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(summary);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            MainWindow.SetStatusText(T("dashboard.copiedSpecs"), isBusy: false);
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText(F("dashboard.copyFailed", ex.Message), isBusy: false);
        }
    }

    private Border SystemDetailsPanel(WinOptimizationApp.Models.DashboardStatus status)
    {
        var grid = new Grid { ColumnSpacing = 20, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        AddInfoPair(grid, 0, 0, T("dashboard.machine"), status.MachineName);
        AddInfoPair(grid, 0, 1, T("dashboard.user"), status.UserName);
        AddInfoPair(grid, 1, 0, T("dashboard.runtime"), status.DotNetRuntime);
        AddInfoPair(grid, 1, 1, T("dashboard.architecture"), $"{status.ProcessArchitecture} / {status.OSArchitecture}");
        AddInfoPair(grid, 2, 0, T("dashboard.processors"), status.ProcessorCount.ToString("N0"));
        AddInfoPair(grid, 2, 1, T("dashboard.memoryLoad"), $"{status.MemoryLoadPercent:N0}%");
        AddInfoPair(grid, 3, 0, T("dashboard.pageFile"), $"{Formatters.FormatBytes((long)(status.TotalPageFileBytes - status.AvailablePageFileBytes))} / {Formatters.FormatBytes((long)status.TotalPageFileBytes)}");
        AddInfoPair(grid, 3, 1, T("dashboard.lastReport"), status.LastReportPath ?? T("common.none"));

        return SectionPanel(T("dashboard.systemDetails"), grid);
    }

    private Border DriveOverviewPanel(WinOptimizationApp.Models.DashboardStatus status)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var drive in status.Drives)
        {
            panel.Children.Add(DriveRow(drive));
        }

        if (status.Drives.Count == 0)
        {
            panel.Children.Add(InfoBlock(T("common.none")));
        }

        return SectionPanel(T("dashboard.drives"), panel);
    }

    private static Grid DriveRow(WinOptimizationApp.Models.DashboardDriveStatus drive)
    {
        var used = Math.Max(0, drive.TotalBytes - drive.FreeBytes);
        var percent = drive.TotalBytes > 0 ? used * 100d / drive.TotalBytes : 0;
        var row = new Grid { ColumnSpacing = 12, MinHeight = 42 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        var name = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(drive.Label) ? drive.Name : $"{drive.Name} {drive.Label}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(name, $"{drive.Name} / {drive.DriveType} / {drive.Format}");
        row.Children.Add(name);

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = percent,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(percent > 85 ? Colors.OrangeRed : percent > 70 ? Colors.Goldenrod : Colors.SteelBlue)
        };
        Grid.SetColumn(progress, 1);
        row.Children.Add(progress);

        var size = new TextBlock
        {
            Text = $"{Formatters.FormatBytes(used)} / {Formatters.FormatBytes(drive.TotalBytes)}",
            Opacity = 0.82,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(size, 2);
        row.Children.Add(size);

        return row;
    }

    private static Border SectionPanel(string title, UIElement content)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(SectionTitle(title));
        stack.Children.Add(content);

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = stack
        };
    }

    private static void AddInfoPair(Grid grid, int row, int column, string label, string value)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = label, Opacity = 0.66, FontSize = 12 });
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.9
        });

        ToolTipService.SetToolTip(stack, value);
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static Border CreateMetricCardWithIcon(
        string title,
        string value,
        string detail,
        Color color,
        string glyph)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(16, color.R, color.G, color.B))
        };

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 28,
            Foreground = Brush(color),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var stack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock { Text = title, Opacity = 0.7, FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = detail, Foreground = Brush(color), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        card.Child = grid;
        return card;
    }

    private Border LiveMonitorPanel(DashboardStatus status)
    {
        var panel = new StackPanel { Spacing = 16 };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = T("dashboard.liveMonitor"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = T("dashboard.liveMonitorSubtitle"),
            Opacity = 0.7,
            FontSize = 12
        });
        Grid.SetColumn(titleStack, 0);
        headerGrid.Children.Add(titleStack);

        var liveBadge = new Border
        {
            Background = Brush(Color.FromArgb(24, 60, 179, 113)),
            BorderBrush = Brush(Colors.MediumSeaGreen),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Microsoft.UI.Xaml.Shapes.Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = Brush(Colors.MediumSeaGreen)
                    },
                    new TextBlock
                    {
                        Text = "LIVE",
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = Brush(Colors.MediumSeaGreen)
                    }
                }
            }
        };
        Grid.SetColumn(liveBadge, 1);
        headerGrid.Children.Add(liveBadge);

        panel.Children.Add(headerGrid);

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var cpuCard = CreateLiveCpuCard(status.CpuName);
        Grid.SetRow(cpuCard, 0); Grid.SetColumn(cpuCard, 0);
        grid.Children.Add(cpuCard);

        var ramUsed = status.TotalRamBytes - status.AvailableRamBytes;
        double ramPercent = status.TotalRamBytes > 0 ? (ramUsed * 100.0 / status.TotalRamBytes) : 0;
        var ramCard = CreateLiveRamCard(ramUsed, status.TotalRamBytes, ramPercent);
        Grid.SetRow(ramCard, 0); Grid.SetColumn(ramCard, 1);
        grid.Children.Add(ramCard);

        var diskUsed = status.SystemDriveTotalBytes - status.SystemDriveFreeBytes;
        double diskPercent = status.SystemDriveTotalBytes > 0 ? (diskUsed * 100.0 / status.SystemDriveTotalBytes) : 0;
        var diskCard = CreateLiveDiskCard(status.SystemDrive, diskUsed, status.SystemDriveTotalBytes, diskPercent);
        Grid.SetRow(diskCard, 1); Grid.SetColumn(diskCard, 0);
        grid.Children.Add(diskCard);

        var networkCard = CreateLiveNetworkCard();
        Grid.SetRow(networkCard, 1); Grid.SetColumn(networkCard, 1);
        grid.Children.Add(networkCard);

        panel.Children.Add(grid);

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = panel
        };
    }

    private Border CreateLiveCpuCard(string cpuName)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(14, Colors.MediumPurple.R, Colors.MediumPurple.G, Colors.MediumPurple.B))
        };

        var rootGrid = new Grid { RowSpacing = 8 };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = "\uE950",
            FontSize = 22,
            Foreground = Brush(Colors.MediumPurple),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new StackPanel { Spacing = 1 };
        titleStack.Children.Add(new TextBlock { Text = T("dashboard.cpu"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        titleStack.Children.Add(new TextBlock { Text = cpuName, Opacity = 0.7, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        var midGrid = new Grid { ColumnSpacing = 12, Height = 34 };
        midGrid.ColumnDefinitions.Add(new ColumnDefinition());
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _cpuValueText = new TextBlock
        {
            Text = "--%",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(Colors.MediumPurple),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_cpuValueText, 0);
        midGrid.Children.Add(_cpuValueText);

        var sparklineStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _cpuSparklineBars.Clear();
        for (int i = 0; i < SparklineCapacity; i++)
        {
            var bar = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = 3,
                Height = 2,
                RadiusX = 1,
                RadiusY = 1,
                Fill = Brush(Colors.MediumPurple),
                Opacity = 0.25,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _cpuSparklineBars.Add(bar);
            sparklineStack.Children.Add(bar);
        }
        Grid.SetColumn(sparklineStack, 1);
        midGrid.Children.Add(sparklineStack);
        Grid.SetRow(midGrid, 1);
        rootGrid.Children.Add(midGrid);

        _cpuProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Foreground = Brush(Colors.MediumPurple)
        };
        Grid.SetRow(_cpuProgressBar, 2);
        rootGrid.Children.Add(_cpuProgressBar);

        card.Child = rootGrid;
        return card;
    }

    private Border CreateLiveRamCard(ulong usedBytes, ulong totalBytes, double initialPercent)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(14, Colors.DarkCyan.R, Colors.DarkCyan.G, Colors.DarkCyan.B))
        };

        var rootGrid = new Grid { RowSpacing = 8 };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = "\uE964",
            FontSize = 22,
            Foreground = Brush(Colors.DarkCyan),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new StackPanel { Spacing = 1 };
        titleStack.Children.Add(new TextBlock { Text = T("dashboard.ram"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        _ramValueText = new TextBlock
        {
            Text = $"{Formatters.FormatBytes((long)usedBytes)} / {Formatters.FormatBytes((long)totalBytes)}",
            Opacity = 0.7,
            FontSize = 11
        };
        titleStack.Children.Add(_ramValueText);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        var midGrid = new Grid { Height = 34 };
        _ramPercentText = new TextBlock
        {
            Text = $"{initialPercent:F1}%",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(Colors.DarkCyan),
            VerticalAlignment = VerticalAlignment.Center
        };
        midGrid.Children.Add(_ramPercentText);
        Grid.SetRow(midGrid, 1);
        rootGrid.Children.Add(midGrid);

        _ramProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = initialPercent,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Foreground = Brush(initialPercent > 85 ? Colors.OrangeRed : initialPercent > 70 ? Colors.Goldenrod : Colors.DarkCyan)
        };
        Grid.SetRow(_ramProgressBar, 2);
        rootGrid.Children.Add(_ramProgressBar);

        card.Child = rootGrid;
        return card;
    }

    private Border CreateLiveDiskCard(string driveLetter, long usedBytes, long totalBytes, double initialPercent)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(14, Colors.SteelBlue.R, Colors.SteelBlue.G, Colors.SteelBlue.B))
        };

        var rootGrid = new Grid { RowSpacing = 8 };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = "\uE7F1",
            FontSize = 22,
            Foreground = Brush(Colors.SteelBlue),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new StackPanel { Spacing = 1 };
        titleStack.Children.Add(new TextBlock { Text = $"{T("dashboard.storage")} ({driveLetter})", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        _diskValueText = new TextBlock
        {
            Text = $"{Formatters.FormatBytes(usedBytes)} / {Formatters.FormatBytes(totalBytes)}",
            Opacity = 0.7,
            FontSize = 11
        };
        titleStack.Children.Add(_diskValueText);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        var midGrid = new Grid { Height = 34 };
        _diskPercentText = new TextBlock
        {
            Text = $"{initialPercent:F1}%",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(Colors.SteelBlue),
            VerticalAlignment = VerticalAlignment.Center
        };
        midGrid.Children.Add(_diskPercentText);
        Grid.SetRow(midGrid, 1);
        rootGrid.Children.Add(midGrid);

        _diskProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = initialPercent,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Foreground = Brush(initialPercent > 90 ? Colors.OrangeRed : initialPercent > 75 ? Colors.Goldenrod : Colors.SteelBlue)
        };
        Grid.SetRow(_diskProgressBar, 2);
        rootGrid.Children.Add(_diskProgressBar);

        card.Child = rootGrid;
        return card;
    }

    private Border CreateLiveNetworkCard()
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(14, Colors.MediumSeaGreen.R, Colors.MediumSeaGreen.G, Colors.MediumSeaGreen.B))
        };

        var rootGrid = new Grid { RowSpacing = 8 };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = "\uE839",
            FontSize = 22,
            Foreground = Brush(Colors.MediumSeaGreen),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new StackPanel { Spacing = 1 };
        titleStack.Children.Add(new TextBlock { Text = T("dashboard.networkActivity"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        titleStack.Children.Add(new TextBlock { Text = "Realtime I/O", Opacity = 0.7, FontSize = 11 });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        var speedGrid = new Grid { Height = 34, ColumnSpacing = 8 };
        speedGrid.ColumnDefinitions.Add(new ColumnDefinition());
        speedGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var downStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        downStack.Children.Add(new TextBlock { Text = T("dashboard.networkDownload"), Opacity = 0.65, FontSize = 10 });
        _networkDownText = new TextBlock
        {
            Text = "↓ 0 B/s",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(Colors.MediumSeaGreen)
        };
        downStack.Children.Add(_networkDownText);
        Grid.SetColumn(downStack, 0);
        speedGrid.Children.Add(downStack);

        var upStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        upStack.Children.Add(new TextBlock { Text = T("dashboard.networkUpload"), Opacity = 0.65, FontSize = 10 });
        _networkUpText = new TextBlock
        {
            Text = "↑ 0 B/s",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(Colors.DeepSkyBlue)
        };
        upStack.Children.Add(_networkUpText);
        Grid.SetColumn(upStack, 1);
        speedGrid.Children.Add(upStack);

        Grid.SetRow(speedGrid, 1);
        rootGrid.Children.Add(speedGrid);

        var pulseLine = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Height = 3,
            RadiusX = 1.5,
            RadiusY = 1.5,
            Fill = Brush(Colors.MediumSeaGreen),
            Opacity = 0.5
        };
        Grid.SetRow(pulseLine, 2);
        rootGrid.Children.Add(pulseLine);

        card.Child = rootGrid;
        return card;
    }

    private Border OptimizationHistoryPanel()
    {
        var summary = MainWindow.Reports.GetHistorySummary();
        var recentReports = MainWindow.Reports.GetRecentReports(3);

        var panel = new StackPanel { Spacing = 14 };

        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = T("dashboard.historyTrend"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = T("dashboard.historySubtitle"),
            Opacity = 0.7,
            FontSize = 12
        });
        panel.Children.Add(titleStack);

        var metricsGrid = new Grid { ColumnSpacing = 12 };
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var freedCard = CreateSummaryStatCard(
            T("dashboard.totalFreed"),
            Formatters.FormatBytes(summary.TotalFreedBytes),
            T("dashboard.totalFreedDesc"),
            Colors.MediumSeaGreen,
            "\uE74E");
        Grid.SetColumn(freedCard, 0);
        metricsGrid.Children.Add(freedCard);

        var filesCard = CreateSummaryStatCard(
            T("dashboard.totalCleanedFiles"),
            $"{summary.TotalFilesRemoved:N0}",
            T("dashboard.totalCleanedFilesDesc"),
            Colors.MediumPurple,
            "\uE74D");
        Grid.SetColumn(filesCard, 1);
        metricsGrid.Children.Add(filesCard);

        var runsCard = CreateSummaryStatCard(
            T("dashboard.totalRuns"),
            $"{summary.TotalRuns:N0}",
            summary.LastRunAt.HasValue
                ? $"{T("dashboard.lastRun")}: {summary.LastRunAt.Value.LocalDateTime:MM/dd HH:mm}"
                : T("dashboard.totalRunsDesc"),
            Colors.SteelBlue,
            "\uE9D9");
        Grid.SetColumn(runsCard, 2);
        metricsGrid.Children.Add(runsCard);

        panel.Children.Add(metricsGrid);

        if (recentReports.Count > 0)
        {
            var runsList = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var report in recentReports)
            {
                runsList.Children.Add(CreateRecentRunRow(report));
            }
            panel.Children.Add(runsList);
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = T("dashboard.noHistory"),
                Opacity = 0.65,
                FontSize = 13,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = panel
        };
    }

    private static Border CreateSummaryStatCard(string title, string value, string detail, Color color, string glyph)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(14, color.R, color.G, color.B))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 24,
            Foreground = Brush(color),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = title, Opacity = 0.7, FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = Brush(color) });
        stack.Children.Add(new TextBlock { Text = detail, Opacity = 0.65, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        border.Child = grid;
        return border;
    }

    private Border CreateRecentRunRow(TaskRunResult result)
    {
        var row = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            Background = Brush(Color.FromArgb(8, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusDot = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brush(result.Success ? Colors.MediumSeaGreen : Colors.DarkOrange),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusDot, 0);
        grid.Children.Add(statusDot);

        var titleStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock { Text = result.TaskLabel, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        titleStack.Children.Add(new TextBlock { Text = $"{result.FinishedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} • {result.FilesRemoved:N0} files", Opacity = 0.65, FontSize = 11 });
        Grid.SetColumn(titleStack, 1);
        grid.Children.Add(titleStack);

        var freedBadge = new Border
        {
            Background = Brush(Color.FromArgb(20, Colors.MediumSeaGreen.R, Colors.MediumSeaGreen.G, Colors.MediumSeaGreen.B)),
            BorderBrush = Brush(Colors.MediumSeaGreen),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = result.FreedBytes > 0 ? $"+{Formatters.FormatBytes(result.FreedBytes)}" : "0 B",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush(Colors.MediumSeaGreen)
            }
        };
        Grid.SetColumn(freedBadge, 2);
        grid.Children.Add(freedBadge);

        row.Child = grid;
        return row;
    }
}
