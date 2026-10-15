using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinOptimizationApp.Models;
using WinOptimizationApp.ViewModels;

namespace WinOptimizationApp.Views;

public sealed partial class StartupPage : BasePage
{
    private TextBox? _searchBox;
    private ComboBox? _statusFilterBox;
    private ComboBox? _impactFilterBox;
    private ComboBox? _sourceFilterBox;
    private ComboBox? _sortBox;
    private CheckBox? _actionableOnlyBox;
    private TextBlock? _countTitle;
    private TextBlock? _highMetricText;
    private TextBlock? _mediumMetricText;
    private TextBlock? _lowMetricText;
    private TextBlock? _emptyBlock;
    private ListView? _listView;
    private int _appliedRevision = -1;

    public StartupViewModel ViewModel { get; }

    public StartupPage(MainWindow mainWindow) : base(mainWindow)
    {
        ViewModel = new StartupViewModel(
            mainWindow.Startup,
            mainWindow.Operations,
            mainWindow.SaveOperationReportAsync,
            T,
            F);

        ViewModel.PropertyChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateSummaryAndHeaders();
            });
        };

        RenderStartupPage();
    }

    private void RenderStartupPage()
    {
        AddHeader(T("startup.title"), T("startup.subtitle"));

        var scanButton = ActionButton(T("startup.scan"), Symbol.Find, async (_, _) =>
        {
            await RefreshListAsync();
        });

        MainContent.Children.Add(scanButton);
        MainContent.Children.Add(StartupOptionsPanel());
        MainContent.Children.Add(InfoBlock(T("startup.previewOnly")));

        _countTitle = SectionTitle(string.Empty);
        _countTitle.Visibility = Visibility.Collapsed;
        MainContent.Children.Add(_countTitle);

        MainContent.Children.Add(CreateStartupSummaryCards());

        _emptyBlock = InfoBlock(T("common.noMatches"));
        _emptyBlock.Visibility = Visibility.Collapsed;
        MainContent.Children.Add(_emptyBlock);

        _listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            ItemsSource = ViewModel.FilteredEntries,
            MaxHeight = 650
        };

        _listView.ContainerContentChanging += (sender, args) =>
        {
            if (args.Item is StartupEntry entry)
            {
                if (args.ItemContainer.Content is not Border border)
                {
                    args.ItemContainer.Content = CreateStartupRow(entry);
                }
                else
                {
                    UpdateStartupRow(border, entry);
                }
            }
        };

        MainContent.Children.Add(_listView);
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!MainWindow.SessionState.StartupLoaded)
        {
            await MainWindow.RefreshStartupStateAsync();
        }

        if (_appliedRevision != MainWindow.SessionState.StartupRevision)
        {
            ApplyCachedStartupState();
        }
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
        ConfigureSearch(_searchBox, "startup-search", () =>
        {
            ViewModel.SearchQuery = _searchBox.Text ?? string.Empty;
        });
        searchRow.Children.Add(_searchBox);

        var resetButton = ActionButton(T("common.resetFilters"), Symbol.Refresh, (_, _) => ResetStartupFilters());
        Grid.SetColumn(resetButton, 1);
        searchRow.Children.Add(resetButton);
        panel.Children.Add(searchRow);

        var filterRow = new AdaptiveWrapPanel();

        _statusFilterBox = FilterBox(T("startup.statusFilter"), 148,
        [
            T("startup.allStatuses"),
            T("startup.enabled"),
            T("startup.disabled")
        ], index => ViewModel.StatusFilter = (StartupStatusFilter)index);

        _impactFilterBox = FilterBox(T("startup.impactFilter"), 150,
        [
            T("startup.allImpacts"),
            T("startup.highImpact"),
            T("startup.mediumImpact"),
            T("startup.lowImpact")
        ], index => ViewModel.ImpactFilter = (StartupImpactFilter)index);

        _sourceFilterBox = FilterBox(T("startup.sourceFilter"), 180,
        [
            T("startup.allSources"),
            T("startup.userSource"),
            T("startup.machineSource"),
            T("startup.startupFolderSource")
        ], index => ViewModel.SourceFilter = (StartupSourceFilter)index);

        _sortBox = FilterBox(T("startup.sort"), 180,
        [
            T("startup.sortImpact"),
            T("startup.sortName"),
            T("startup.sortSource"),
            T("startup.sortStatus")
        ], index => ViewModel.SortOrder = (StartupSortOrder)index);

        _actionableOnlyBox = new CheckBox
        {
            Content = T("startup.actionableOnly"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _actionableOnlyBox.Checked += (_, _) => ViewModel.ActionableOnly = true;
        _actionableOnlyBox.Unchecked += (_, _) => ViewModel.ActionableOnly = false;

        filterRow.Children.Add(_statusFilterBox);
        filterRow.Children.Add(_impactFilterBox);
        filterRow.Children.Add(_sourceFilterBox);
        filterRow.Children.Add(_sortBox);
        filterRow.Children.Add(_actionableOnlyBox);
        panel.Children.Add(filterRow);

        return panel;
    }

    private static ComboBox FilterBox(string header, double minWidth, IReadOnlyList<string> items, Action<int> onSelectionChanged)
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
        box.SelectionChanged += (_, _) => onSelectionChanged(box.SelectedIndex);
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

        CancelDebouncedUiAction("startup-search");
        ViewModel.ResetFilters();
    }

    private async Task RefreshListAsync()
    {
        if (ViewModel.IsRefreshing)
        {
            return;
        }

        MainWindow.SetStatusText(T("startup.scanning"));
        try
        {
            await MainWindow.RefreshStartupStateAsync();
            ApplyCachedStartupState();
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"), isBusy: false);
        }
    }

    private void ApplyCachedStartupState()
    {
        var state = MainWindow.SessionState;
        _appliedRevision = state.StartupRevision;
        ViewModel.LoadEntries(state.StartupEntries, state.StartupError);
        UpdateSummaryAndHeaders();
    }

    private void UpdateSummaryAndHeaders()
    {
        if (_countTitle != null)
        {
            if (ViewModel.TotalCount > 0)
            {
                _countTitle.Text = F("startup.entriesFiltered", ViewModel.FilteredCount, ViewModel.TotalCount);
                _countTitle.Visibility = Visibility.Visible;
            }
            else
            {
                _countTitle.Visibility = Visibility.Collapsed;
            }
        }

        if (_highMetricText != null)
        {
            var count = ViewModel.FilteredEntries.Count(e => e.Impact == StartupImpactLevel.High);
            _highMetricText.Text = count.ToString("N0");
        }

        if (_mediumMetricText != null)
        {
            var count = ViewModel.FilteredEntries.Count(e => e.Impact == StartupImpactLevel.Medium);
            _mediumMetricText.Text = count.ToString("N0");
        }

        if (_lowMetricText != null)
        {
            var count = ViewModel.FilteredEntries.Count(e => e.Impact == StartupImpactLevel.Low);
            _lowMetricText.Text = count.ToString("N0");
        }

        if (_emptyBlock != null)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                _emptyBlock.Text = F("startup.scanFailed", ViewModel.ErrorMessage);
                _emptyBlock.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyBlock.Text = T("common.noMatches");
                _emptyBlock.Visibility = ViewModel.FilteredCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private Border CreateStartupSummaryCards()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        _highMetricText = AddSummaryMetric(grid, 0, T("startup.highImpact"), "0", Colors.OrangeRed);
        _mediumMetricText = AddSummaryMetric(grid, 1, T("startup.mediumImpact"), "0", Colors.DarkOrange);
        _lowMetricText = AddSummaryMetric(grid, 2, T("startup.lowImpact"), "0", Colors.SeaGreen);

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

    private static TextBlock AddSummaryMetric(Grid grid, int column, string label, string value, Color color)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, Opacity = 0.7 });
        var textBlock = new TextBlock
        {
            Text = value,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(color)
        };
        stack.Children.Add(textBlock);

        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = Brush(Color.FromArgb(16, color.R, color.G, color.B)),
            Child = stack
        };

        Grid.SetColumn(card, column);
        grid.Children.Add(card);
        return textBlock;
    }

    private Border CreateStartupRow(StartupEntry entry)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = ImpactBadge(entry.Impact);
        Grid.SetColumn(badge, 0);
        row.Children.Add(badge);

        var info = new StackPanel { Spacing = 4 };
        var nameBlock = new TextBlock
        {
            Text = entry.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var sourceBlock = new TextBlock
        {
            Text = $"{entry.Source} / {(entry.Enabled ? T("startup.enabled") : T("startup.disabled"))}",
            Opacity = 0.72,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var cmdBlock = new TextBlock
        {
            Text = entry.Command,
            Opacity = 0.62,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        info.Children.Add(nameBlock);
        info.Children.Add(sourceBlock);
        info.Children.Add(cmdBlock);
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
        var recBlock = new TextBlock
        {
            Text = LocalizedStartupRecommendation(entry.Recommendation),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.82
        };
        recommendation.Children.Add(recBlock);
        Grid.SetColumn(recommendation, 2);
        row.Children.Add(recommendation);

        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var toggleBtn = new Button
        {
            Content = entry.Enabled ? T("startup.disable") : T("startup.enable"),
            MinWidth = 86,
            IsEnabled = StartupViewModel.CanToggleStartupEntry(entry)
        };

        toggleBtn.Click += async (_, _) =>
        {
            var currentEntry = (StartupEntry)((Button)toggleBtn).Tag;
            if (currentEntry.Enabled && currentEntry.Impact == StartupImpactLevel.High)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = T("startup.confirmDisableTitle"),
                    Content = new TextBlock
                    {
                        Text = F("startup.confirmDisableMessage", currentEntry.Name),
                        TextWrapping = TextWrapping.Wrap
                    },
                    PrimaryButtonText = T("startup.disable"),
                    CloseButtonText = T("common.cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                if (await MainWindow.ShowThemedDialogAsync(confirmDialog) != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var ok = await ViewModel.ToggleEntryAsync(currentEntry);
            if (ok)
            {
                await MainWindow.RefreshStartupStateAsync();
                ApplyCachedStartupState();
            }
            else
            {
                await MainWindow.ShowDialogAsync_Internal(
                    T("startup.actionFailedTitle"),
                    InfoBlock(T("startup.actionFailed")),
                    T("common.close"));
            }
        };
        toggleBtn.Tag = entry;
        actionsPanel.Children.Add(toggleBtn);

        var open = new Button
        {
            Content = T("common.openLocation"),
            MinWidth = 124,
            Tag = entry.Command
        };
        ToolTipService.SetToolTip(open, T("common.openLocation"));
        open.Click += (_, _) => MainWindow.OpenContainingFolder_Internal((string)open.Tag);
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
            Margin = new Thickness(0, 4, 0, 4),
            Child = row,
            Tag = entry
        };
    }

    private void UpdateStartupRow(Border border, StartupEntry entry)
    {
        border.Tag = entry;
        if (border.Child is not Grid row || row.Children.Count < 4)
        {
            return;
        }

        // Replace impact badge at column 0
        row.Children[0] = ImpactBadge(entry.Impact);
        Grid.SetColumn((FrameworkElement)row.Children[0], 0);

        // Update info at column 1
        if (row.Children[1] is StackPanel info && info.Children.Count >= 3)
        {
            if (info.Children[0] is TextBlock nameBlock) nameBlock.Text = entry.Name;
            if (info.Children[1] is TextBlock sourceBlock)
                sourceBlock.Text = $"{entry.Source} / {(entry.Enabled ? T("startup.enabled") : T("startup.disabled"))}";
            if (info.Children[2] is TextBlock cmdBlock) cmdBlock.Text = entry.Command;
            ToolTipService.SetToolTip(info, entry.Command);
        }

        // Update recommendation at column 2
        if (row.Children[2] is StackPanel rec && rec.Children.Count >= 2 && rec.Children[1] is TextBlock recBlock)
        {
            recBlock.Text = LocalizedStartupRecommendation(entry.Recommendation);
        }

        // Update actions at column 3
        if (row.Children[3] is StackPanel actionsPanel && actionsPanel.Children.Count >= 2)
        {
            if (actionsPanel.Children[0] is Button toggleBtn)
            {
                toggleBtn.Content = entry.Enabled ? T("startup.disable") : T("startup.enable");
                toggleBtn.IsEnabled = StartupViewModel.CanToggleStartupEntry(entry);
                toggleBtn.Tag = entry;
            }

            if (actionsPanel.Children[1] is Button openBtn)
            {
                openBtn.Tag = entry.Command;
            }
        }
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
