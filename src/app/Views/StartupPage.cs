using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public sealed partial class StartupPage : BasePage
{
    private StackPanel? _resultPanel;

    public StartupPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderStartupPage();
    }

    private void RenderStartupPage()
    {
        AddHeader(T("startup.title"), T("startup.subtitle"));

        _resultPanel = new StackPanel { Spacing = 8 };
        var scanButton = ActionButton(T("startup.scan"), Symbol.Find, async (_, _) =>
        {
            await RefreshListAsync();
        });

        MainContent.Children.Add(scanButton);
        MainContent.Children.Add(InfoBlock(T("startup.previewOnly")));
        MainContent.Children.Add(_resultPanel);
    }

    private async Task RefreshListAsync()
    {
        if (_resultPanel == null)
        {
            return;
        }

        MainWindow.SetStatusText(T("startup.scanning"));
        _resultPanel.Children.Clear();
        var entries = await MainWindow.Startup.ScanAsync();
        _resultPanel.Children.Add(SectionTitle(F("startup.entries", entries.Count)));
        _resultPanel.Children.Add(StartupSummary(entries));
        foreach (var entry in entries)
        {
            _resultPanel.Children.Add(StartupRow(entry));
        }
        MainWindow.SetStatusText(T("common.ready"));
    }

    private Border StartupRow(StartupEntry entry)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(ImpactBadge(entry.Impact));

        var info = new StackPanel { Spacing = 4 };
        info.Children.Add(new TextBlock
        {
            Text = entry.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{entry.Source} / {(entry.Enabled ? T("startup.enabled") : T("startup.disabled"))}",
            Opacity = 0.72,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        info.Children.Add(new TextBlock
        {
            Text = entry.Command,
            Opacity = 0.62,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        ToolTipService.SetToolTip(info, entry.Command);
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        var recommendation = new StackPanel { Spacing = 4 };
        recommendation.Children.Add(new TextBlock
        {
            Text = T("startup.recommendation"),
            Opacity = 0.66,
            FontSize = 12
        });
        recommendation.Children.Add(new TextBlock
        {
            Text = LocalizedStartupRecommendation(entry.Recommendation),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.82
        });
        Grid.SetColumn(recommendation, 2);
        row.Children.Add(recommendation);

        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        // Enable/Disable Button
        var toggleBtn = new Button
        {
            Content = entry.Enabled ? T("startup.disable") : T("startup.enable"),
            MinWidth = 86,
            IsEnabled = entry.CanDisable
        };

        toggleBtn.Click += async (_, _) =>
        {
            if (entry.Enabled)
            {
                if (entry.Impact == StartupImpactLevel.High)
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = T("startup.confirmDisableTitle"),
                        Content = new TextBlock
                        {
                            Text = F("startup.confirmDisableMessage", entry.Name),
                            TextWrapping = TextWrapping.Wrap
                        },
                        PrimaryButtonText = T("startup.disable"),
                        CloseButtonText = T("common.cancel"),
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = XamlRoot
                    };

                    var res = await confirmDialog.ShowAsync();
                    if (res != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                MainWindow.SetStatusText(T("common.loading"));
                var ok = await MainWindow.Startup.DisableAsync(entry);
                if (ok)
                {
                    await RefreshListAsync();
                }
                else
                {
                    MainWindow.SetStatusText(T("common.ready"));
                }
            }
            else
            {
                MainWindow.SetStatusText(T("common.loading"));
                var ok = await MainWindow.Startup.EnableAsync(entry);
                if (ok)
                {
                    await RefreshListAsync();
                }
                else
                {
                    MainWindow.SetStatusText(T("common.ready"));
                }
            }
        };
        actionsPanel.Children.Add(toggleBtn);

        // Open Location Button
        var open = new Button
        {
            Content = T("common.openLocation"),
            MinWidth = 124
        };
        ToolTipService.SetToolTip(open, T("common.openLocation"));
        open.Click += (_, _) => MainWindow.OpenContainingFolder_Internal(entry.Command);
        actionsPanel.Children.Add(open);

        Grid.SetColumn(actionsPanel, 3);
        row.Children.Add(actionsPanel);

        return new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = row
        };
    }

    private string LocalizedStartupRecommendation(string recommendation)
    {
        var key = recommendation switch
        {
            "Already disabled" => "startup.recommendation.alreadyDisabled",
            "Keep enabled unless you know this Windows component is unnecessary" => "startup.recommendation.keepWindows",
            "Review carefully; script-based startup from a user-writable path can slow boot or be unsafe" => "startup.recommendation.reviewScript",
            "Consider delaying or disabling if you do not need it immediately after sign-in" => "startup.recommendation.delayHeavy",
            "Review publisher and purpose before changing machine-wide startup" => "startup.recommendation.reviewMachineWide",
            "Review user-profile startup entries before disabling" => "startup.recommendation.reviewUserProfile",
            "Low startup impact; keep enabled unless it is unwanted" => "startup.recommendation.lowImpact",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(key))
        {
            return recommendation;
        }

        var value = T(key);
        return value == key ? recommendation : value;
    }

    private Border StartupSummary(IReadOnlyList<StartupEntry> entries)
    {
        var high = entries.Count(entry => entry.Impact == StartupImpactLevel.High);
        var medium = entries.Count(entry => entry.Impact == StartupImpactLevel.Medium);
        var low = entries.Count(entry => entry.Impact == StartupImpactLevel.Low);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddSummaryMetric(grid, 0, T("startup.highImpact"), high.ToString("N0"), Colors.OrangeRed);
        AddSummaryMetric(grid, 1, T("startup.mediumImpact"), medium.ToString("N0"), Colors.DarkOrange);
        AddSummaryMetric(grid, 2, T("startup.lowImpact"), low.ToString("N0"), Colors.SeaGreen);

        return new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = grid
        };
    }

    private static void AddSummaryMetric(Grid grid, int column, string label, string value, Color color)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, Opacity = 0.7 });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(color)
        });
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private Border ImpactBadge(StartupImpactLevel impact)
    {
        var color = impact switch
        {
            StartupImpactLevel.High => Colors.OrangeRed,
            StartupImpactLevel.Medium => Colors.DarkOrange,
            StartupImpactLevel.Low => Colors.SeaGreen,
            _ => Colors.Gray
        };

        var label = impact switch
        {
            StartupImpactLevel.High => T("startup.highImpact"),
            StartupImpactLevel.Medium => T("startup.mediumImpact"),
            StartupImpactLevel.Low => T("startup.lowImpact"),
            _ => impact.ToString()
        };

        return new Border
        {
            Padding = new Thickness(0, 5, 0, 5),
            CornerRadius = new CornerRadius(6),
            Background = Brush(Color.FromArgb(38, color.R, color.G, color.B)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = label,
                Foreground = Brush(color),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
