using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public sealed partial class StartupPage : BasePage
{
    private StackPanel? _resultPanel;
    private TextBox? _searchBox;
    private ComboBox? _statusFilterBox;
    private ComboBox? _impactFilterBox;
    private ComboBox? _sourceFilterBox;
    private ComboBox? _sortBox;
    private CheckBox? _actionableOnlyBox;
    private List<StartupEntry> _startupEntries = [];

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
        MainContent.Children.Add(StartupOptionsPanel());
        MainContent.Children.Add(InfoBlock(T("startup.previewOnly")));
        MainContent.Children.Add(_resultPanel);
    }

    private StackPanel StartupOptionsPanel()
    {
        var panel = new StackPanel { Spacing = 10 };

        var searchRow = new Grid { ColumnSpacing = 10 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition());
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new TextBox
        {
            PlaceholderText = T("startup.searchPlaceholder"),
            Height = 36
        };
        _searchBox.TextChanged += (_, _) => RenderStartupEntries();
        searchRow.Children.Add(_searchBox);

        var resetButton = ActionButton(T("common.resetFilters"), Symbol.Refresh, (_, _) => ResetStartupFilters());
        Grid.SetColumn(resetButton, 1);
        searchRow.Children.Add(resetButton);
        panel.Children.Add(searchRow);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _statusFilterBox = FilterBox(T("startup.statusFilter"), 148,
        [
            T("startup.allStatuses"),
            T("startup.enabled"),
            T("startup.disabled")
        ]);
        _impactFilterBox = FilterBox(T("startup.impactFilter"), 150,
        [
            T("startup.allImpacts"),
            T("startup.highImpact"),
            T("startup.mediumImpact"),
            T("startup.lowImpact")
        ]);
        _sourceFilterBox = FilterBox(T("startup.sourceFilter"), 180,
        [
            T("startup.allSources"),
            T("startup.userSource"),
            T("startup.machineSource"),
            T("startup.startupFolderSource")
        ]);
        _sortBox = FilterBox(T("startup.sort"), 180,
        [
            T("startup.sortImpact"),
            T("startup.sortName"),
            T("startup.sortSource"),
            T("startup.sortStatus")
        ]);

        _actionableOnlyBox = new CheckBox
        {
            Content = T("startup.actionableOnly"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _actionableOnlyBox.Checked += (_, _) => RenderStartupEntries();
        _actionableOnlyBox.Unchecked += (_, _) => RenderStartupEntries();

        filterRow.Children.Add(_statusFilterBox);
        filterRow.Children.Add(_impactFilterBox);
        filterRow.Children.Add(_sourceFilterBox);
        filterRow.Children.Add(_sortBox);
        filterRow.Children.Add(_actionableOnlyBox);
        panel.Children.Add(filterRow);

        return panel;
    }

    private ComboBox FilterBox(string header, double minWidth, IReadOnlyList<string> items)
    {
        var box = new ComboBox
        {
            Header = header,
            MinWidth = minWidth
        };

        foreach (var item in items)
        {
            box.Items.Add(item);
        }

        box.SelectedIndex = 0;
        box.SelectionChanged += (_, _) => RenderStartupEntries();
        return box;
    }

    private void ResetStartupFilters()
    {
        if (_searchBox is not null)
        {
            _searchBox.Text = string.Empty;
        }

        if (_statusFilterBox is not null)
        {
            _statusFilterBox.SelectedIndex = 0;
        }

        if (_impactFilterBox is not null)
        {
            _impactFilterBox.SelectedIndex = 0;
        }

        if (_sourceFilterBox is not null)
        {
            _sourceFilterBox.SelectedIndex = 0;
        }

        if (_sortBox is not null)
        {
            _sortBox.SelectedIndex = 0;
        }

        if (_actionableOnlyBox is not null)
        {
            _actionableOnlyBox.IsChecked = false;
        }

        RenderStartupEntries();
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
        _startupEntries = entries.ToList();
        RenderStartupEntries();
        MainWindow.SetStatusText(T("common.ready"));
    }

    private void RenderStartupEntries()
    {
        if (_resultPanel == null)
        {
            return;
        }

        _resultPanel.Children.Clear();
        var entries = SortStartupEntries(FilterStartupEntries(_startupEntries)).ToList();
        _resultPanel.Children.Add(SectionTitle(F("startup.entriesFiltered", entries.Count, _startupEntries.Count)));
        _resultPanel.Children.Add(StartupSummary(entries));

        if (entries.Count == 0)
        {
            _resultPanel.Children.Add(InfoBlock(T("common.noMatches")));
            return;
        }

        foreach (var entry in entries)
        {
            _resultPanel.Children.Add(StartupRow(entry));
        }
    }

    private IEnumerable<StartupEntry> FilterStartupEntries(IEnumerable<StartupEntry> entries)
    {
        var query = _searchBox?.Text?.Trim() ?? string.Empty;
        var statusIndex = _statusFilterBox?.SelectedIndex ?? 0;
        var impactIndex = _impactFilterBox?.SelectedIndex ?? 0;
        var sourceIndex = _sourceFilterBox?.SelectedIndex ?? 0;
        var actionableOnly = _actionableOnlyBox?.IsChecked == true;

        foreach (var entry in entries)
        {
            if (!MatchesStartupQuery(entry, query))
            {
                continue;
            }

            if (statusIndex == 1 && !entry.Enabled)
            {
                continue;
            }

            if (statusIndex == 2 && entry.Enabled)
            {
                continue;
            }

            if (impactIndex == 1 && entry.Impact != StartupImpactLevel.High)
            {
                continue;
            }

            if (impactIndex == 2 && entry.Impact != StartupImpactLevel.Medium)
            {
                continue;
            }

            if (impactIndex == 3 && entry.Impact != StartupImpactLevel.Low)
            {
                continue;
            }

            if (sourceIndex == 1 && !IsUserStartupSource(entry.Source))
            {
                continue;
            }

            if (sourceIndex == 2 && !IsMachineStartupSource(entry.Source))
            {
                continue;
            }

            if (sourceIndex == 3 && !entry.Source.Contains("Startup folder", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (actionableOnly && !CanToggleStartupEntry(entry))
            {
                continue;
            }

            yield return entry;
        }
    }

    private IEnumerable<StartupEntry> SortStartupEntries(IEnumerable<StartupEntry> entries)
    {
        return (_sortBox?.SelectedIndex ?? 0) switch
        {
            1 => entries.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => entries
                .OrderBy(entry => entry.Source, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
            3 => entries
                .OrderByDescending(entry => entry.Enabled)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => entries
                .OrderByDescending(entry => entry.Impact)
                .ThenByDescending(entry => entry.Enabled)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private static bool MatchesStartupQuery(StartupEntry entry, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.RiskHint.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Recommendation.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserStartupSource(string source)
    {
        return source.Contains("HKCU", StringComparison.OrdinalIgnoreCase)
            || source.Contains("User Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMachineStartupSource(string source)
    {
        return source.Contains("HKLM", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Common Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanToggleStartupEntry(StartupEntry entry)
    {
        return entry.Enabled ? entry.CanDisable : entry.CanRollback;
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
            IsEnabled = CanToggleStartupEntry(entry)
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
