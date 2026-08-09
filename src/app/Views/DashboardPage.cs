using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class DashboardPage : BasePage
{
    private readonly StackPanel _wingetResultPanel;
    private int _renderedRevision = -1;
    private bool _isRefreshing;

    public DashboardPage(MainWindow mainWindow) : base(mainWindow)
    {
        _wingetResultPanel = new StackPanel { Spacing = 8 };
    }

    public override Task OnNavigatedToAsync()
    {
        if (_renderedRevision == MainWindow.SessionState.DashboardRevision && MainContent.Children.Count > 0)
        {
            return Task.CompletedTask;
        }

        RenderCachedDashboard();
        return Task.CompletedTask;
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
            MainWindow.SetStatusText(T("common.ready"));
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

        // Hardware & Resources Section
        MainContent.Children.Add(SectionTitle(T("dashboard.hardware")));

        var hardwareCard = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
        };

        var hardwareStack = new StackPanel { Spacing = 20 };

        // 1. CPU Processor Name
        var cpuGrid = new Grid { ColumnSpacing = 16 };
        cpuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cpuGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var cpuIcon = new FontIcon
        {
            Glyph = "\uE950", // CPU
            FontSize = 26,
            Foreground = Brush(Colors.MediumPurple),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(cpuIcon, 0);
        cpuGrid.Children.Add(cpuIcon);

        var cpuInfo = new StackPanel { Spacing = 2 };
        cpuInfo.Children.Add(new TextBlock { Text = T("dashboard.cpu"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        cpuInfo.Children.Add(new TextBlock { Text = status.CpuName, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(cpuInfo, 1);
        cpuGrid.Children.Add(cpuInfo);
        hardwareStack.Children.Add(cpuGrid);

        // 2. RAM Memory capacity progress bar
        var ramUsed = status.TotalRamBytes - status.AvailableRamBytes;
        double ramPercent = status.TotalRamBytes > 0 ? (ramUsed * 100.0 / status.TotalRamBytes) : 0;

        var ramGrid = new Grid { ColumnSpacing = 16 };
        ramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ramGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var ramIcon = new FontIcon
        {
            Glyph = "\uE964", // RAM
            FontSize = 26,
            Foreground = Brush(Colors.DarkCyan),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(ramIcon, 0);
        ramGrid.Children.Add(ramIcon);

        var ramInfo = new StackPanel { Spacing = 6 };
        var ramHeader = new Grid();
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition());
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ramTitle = new TextBlock { Text = T("dashboard.ram"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var ramText = new TextBlock
        {
            Text = $"{Formatters.FormatBytes((long)ramUsed)} / {Formatters.FormatBytes((long)status.TotalRamBytes)} ({ramPercent:F1}%)",
            Opacity = 0.8
        };
        Grid.SetColumn(ramTitle, 0);
        Grid.SetColumn(ramText, 1);
        ramHeader.Children.Add(ramTitle);
        ramHeader.Children.Add(ramText);

        var ramProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = ramPercent,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = Brush(ramPercent > 85 ? Colors.OrangeRed : ramPercent > 70 ? Colors.Goldenrod : Colors.DarkCyan)
        };

        ramInfo.Children.Add(ramHeader);
        ramInfo.Children.Add(ramProgress);
        Grid.SetColumn(ramInfo, 1);
        ramGrid.Children.Add(ramInfo);
        hardwareStack.Children.Add(ramGrid);

        // 3. Disk Space capacity progress bar
        var diskTotal = status.SystemDriveTotalBytes;
        var diskFree = status.SystemDriveFreeBytes;
        var diskUsed = diskTotal - diskFree;
        double diskPercent = diskTotal > 0 ? (diskUsed * 100.0 / diskTotal) : 0;

        var diskGrid = new Grid { ColumnSpacing = 16 };
        diskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        diskGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var diskIcon = new FontIcon
        {
            Glyph = "\uE7F1", // Disk Storage
            FontSize = 26,
            Foreground = Brush(Colors.SteelBlue),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(diskIcon, 0);
        diskGrid.Children.Add(diskIcon);

        var diskInfo = new StackPanel { Spacing = 6 };
        var diskHeader = new Grid();
        diskHeader.ColumnDefinitions.Add(new ColumnDefinition());
        diskHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var diskTitle = new TextBlock { Text = $"{T("dashboard.storage")} ({status.SystemDrive})", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var diskText = new TextBlock
        {
            Text = $"{Formatters.FormatBytes(diskUsed)} / {Formatters.FormatBytes(diskTotal)} ({diskPercent:F1}%)",
            Opacity = 0.8
        };
        Grid.SetColumn(diskTitle, 0);
        Grid.SetColumn(diskText, 1);
        diskHeader.Children.Add(diskTitle);
        diskHeader.Children.Add(diskText);

        var diskProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = diskPercent,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = Brush(diskPercent > 85 ? Colors.OrangeRed : diskPercent > 70 ? Colors.Goldenrod : Colors.SteelBlue)
        };

        diskInfo.Children.Add(diskHeader);
        diskInfo.Children.Add(diskProgress);
        Grid.SetColumn(diskInfo, 1);
        diskGrid.Children.Add(diskInfo);
        hardwareStack.Children.Add(diskGrid);

        hardwareCard.Child = hardwareStack;
        MainContent.Children.Add(hardwareCard);
        MainContent.Children.Add(SystemDetailsPanel(status));
        MainContent.Children.Add(DriveOverviewPanel(status));

        // Quick Actions
        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        quick.Children.Add(ActionButton(T("dashboard.healthCheck"), Symbol.Refresh, async (_, _) => await RefreshDashboardAsync()));
        quick.Children.Add(ActionButton(T("dashboard.scanCleanup"), Symbol.Find, async (_, _) => await MainWindow.NavigateToTagAsync("cleanup")));
        quick.Children.Add(ActionButton(T("dashboard.analyzeStorage"), Symbol.View, async (_, _) => await MainWindow.NavigateToTagAsync("storage")));
        quick.Children.Add(ActionButton(T("dashboard.scanUpdates"), Symbol.Download, async (_, _) => await MainWindow.ScanWingetAsync(_wingetResultPanel)));
        quick.Children.Add(ActionButton(T("dashboard.openLogs"), Symbol.OpenFile, (_, _) => MainWindow.OpenFolder_Internal(MainWindow.Paths.LogsDirectory)));
        quick.Children.Add(ActionButton(T("dashboard.export"), Symbol.Save, async (_, _) => await ExportDashboardAsync(status)));
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

                if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
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
        await dialog.ShowAsync();
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
            MainWindow.SetStatusText(F("dashboard.exported", path));
            await MainWindow.ShowDialogAsync_Internal(T("dashboard.exportedTitle"), InfoBlock(path), T("common.close"));
        }
        catch (Exception ex)
        {
            await MainWindow.ShowDialogAsync_Internal(T("dashboard.exportFailed"), InfoBlock(ex.Message), T("common.close"));
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
}
