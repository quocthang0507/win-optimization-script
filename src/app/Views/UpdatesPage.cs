using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public sealed partial class UpdatesPage : BasePage
{
    private StackPanel? _resultPanel;
    private TextBox? _searchBox;
    private ComboBox? _sourceFilterBox;
    private ComboBox? _sortBox;
    private Button? _scanButton;
    private Button? _upgradeAllButton;
    private List<WingetPackage> _packages = [];
    private readonly Dictionary<string, TextBlock> _packageStatusLabels = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpgrading;

    public UpdatesPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderUpdatesPage();
    }

    private void RenderUpdatesPage()
    {
        AddHeader(T("updates.title"), T("updates.subtitle"));

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _scanButton = ActionButton(T("updates.scanWinget"), Symbol.Find, async (_, _) =>
        {
            await ScanUpdatesAsync();
        });
        actions.Children.Add(_scanButton);

        _upgradeAllButton = ActionButton(T("updates.upgradeAll"), Symbol.Download, async (_, _) =>
        {
            await UpgradePackagesAsync(null);
        });
        actions.Children.Add(_upgradeAllButton);

        MainContent.Children.Add(actions);
        MainContent.Children.Add(UpdateOptionsPanel());
        MainContent.Children.Add(_resultPanel);
    }

    private StackPanel UpdateOptionsPanel()
    {
        var panel = new StackPanel { Spacing = 10 };

        var searchRow = new Grid { ColumnSpacing = 10 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition());
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new TextBox
        {
            PlaceholderText = T("updates.searchPlaceholder"),
            Height = 36
        };
        _searchBox.TextChanged += (_, _) => RenderPackages();
        searchRow.Children.Add(_searchBox);

        var resetButton = ActionButton(T("common.resetFilters"), Symbol.Refresh, (_, _) => ResetUpdateFilters());
        Grid.SetColumn(resetButton, 1);
        searchRow.Children.Add(resetButton);
        panel.Children.Add(searchRow);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _sourceFilterBox = new ComboBox
        {
            Header = T("updates.sourceFilter"),
            MinWidth = 160
        };
        _sourceFilterBox.Items.Add(T("updates.allSources"));
        _sourceFilterBox.Items.Add(T("updates.wingetSource"));
        _sourceFilterBox.Items.Add(T("updates.msstoreSource"));
        _sourceFilterBox.SelectedIndex = 0;
        _sourceFilterBox.SelectionChanged += (_, _) => RenderPackages();
        filterRow.Children.Add(_sourceFilterBox);

        _sortBox = new ComboBox
        {
            Header = T("updates.sort"),
            MinWidth = 180
        };
        _sortBox.Items.Add(T("updates.sortName"));
        _sortBox.Items.Add(T("updates.sortId"));
        _sortBox.Items.Add(T("updates.sortSource"));
        _sortBox.Items.Add(T("updates.sortVersion"));
        _sortBox.SelectedIndex = 0;
        _sortBox.SelectionChanged += (_, _) => RenderPackages();
        filterRow.Children.Add(_sortBox);

        panel.Children.Add(filterRow);
        return panel;
    }

    private void ResetUpdateFilters()
    {
        if (_searchBox is not null)
        {
            _searchBox.Text = string.Empty;
        }

        if (_sourceFilterBox is not null)
        {
            _sourceFilterBox.SelectedIndex = 0;
        }

        if (_sortBox is not null)
        {
            _sortBox.SelectedIndex = 0;
        }

        RenderPackages();
    }

    private async Task ScanUpdatesAsync()
    {
        if (_resultPanel == null || _isUpgrading)
        {
            return;
        }

        MainWindow.SetStatusText(T("updates.scanning"));
        SetUpdateControlsEnabled(false);
        _resultPanel.Children.Clear();
        try
        {
            _packages = (await MainWindow.Winget.ScanAsync()).ToList();
            RenderPackages();
        }
        finally
        {
            SetUpdateControlsEnabled(true);
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private void RenderPackages()
    {
        if (_resultPanel == null)
        {
            return;
        }

        _resultPanel.Children.Clear();
        _packageStatusLabels.Clear();
        var packages = SortPackages(FilterPackages(_packages)).ToList();
        _resultPanel.Children.Add(SectionTitle(F("updates.packageUpdatesFiltered", packages.Count, _packages.Count)));

        if (packages.Count == 0)
        {
            _resultPanel.Children.Add(InfoBlock(T("common.noMatches")));
            return;
        }

        foreach (var package in packages)
        {
            _resultPanel.Children.Add(PackageRow(package));
        }
    }

    private IEnumerable<WingetPackage> FilterPackages(IEnumerable<WingetPackage> packages)
    {
        var query = _searchBox?.Text?.Trim() ?? string.Empty;
        var sourceIndex = _sourceFilterBox?.SelectedIndex ?? 0;

        foreach (var package in packages)
        {
            if (!MatchesPackageQuery(package, query))
            {
                continue;
            }

            if (sourceIndex == 1 && !package.Source.Equals("winget", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceIndex == 2 && !package.Source.Equals("msstore", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return package;
        }
    }

    private IEnumerable<WingetPackage> SortPackages(IEnumerable<WingetPackage> packages)
    {
        return (_sortBox?.SelectedIndex ?? 0) switch
        {
            1 => packages
                .OrderBy(package => package.Id, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => packages
                .OrderBy(package => package.Source, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase),
            3 => packages
                .OrderBy(package => package.AvailableVersion, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => packages.OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private static bool MatchesPackageQuery(WingetPackage package, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || package.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || package.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || package.InstalledVersion.Contains(query, StringComparison.OrdinalIgnoreCase)
            || package.AvailableVersion.Contains(query, StringComparison.OrdinalIgnoreCase)
            || package.Source.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private Border PackageRow(WingetPackage package)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var details = new StackPanel { Spacing = 4 };
        details.Children.Add(new TextBlock { Text = package.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        grid.Children.Add(details);

        var statusText = new TextBlock
        {
            Text = T("updates.pending"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center
        };
        _packageStatusLabels[PackageKey(package)] = statusText;
        Grid.SetColumn(statusText, 1);
        grid.Children.Add(statusText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var upgradeButton = IconButton(Symbol.Download, T("updates.upgradeOne"), async (_, _) =>
        {
            await UpgradePackagesAsync([package]);
        });
        actions.Children.Add(upgradeButton);

        var openButton = IconButton(Symbol.OpenFile, T("common.open"), (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://winget.run/pkg/{Uri.EscapeDataString(package.Id)}",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        });
        actions.Children.Add(openButton);

        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        border.Child = grid;
        return border;
    }

    private async Task UpgradePackagesAsync(IReadOnlyList<WingetPackage>? selectedPackages)
    {
        if (_resultPanel == null || _isUpgrading)
        {
            return;
        }

        if (_packages.Count == 0)
        {
            await ScanUpdatesAsync();
        }

        var packages = selectedPackages?.ToList() ?? _packages.ToList();
        if (packages.Count == 0)
        {
            MainWindow.SetStatusText(T("common.ready"));
            return;
        }

        if (!await ConfirmUpgradeAsync(packages.Count))
        {
            return;
        }

        _isUpgrading = true;
        SetUpdateControlsEnabled(false);

        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < packages.Count; index++)
            {
                var package = packages[index];
                var progressText = F("updates.upgradeProgress", index + 1, packages.Count, package.Name);
                MainWindow.SetStatusText(progressText);
                SetPackageStatus(package, progressText, Colors.DeepSkyBlue);

                WingetPackageUpgradeResult result;
                try
                {
                    result = await MainWindow.Winget.UpgradePackageAsync(package);
                }
                catch (Exception ex)
                {
                    result = new WingetPackageUpgradeResult(package, false, -1, string.Empty, ex.Message);
                }

                if (result.Success)
                {
                    succeeded++;
                    SetPackageStatus(package, T("updates.upgradeSucceeded"), Colors.MediumSeaGreen);
                }
                else
                {
                    failed++;
                    SetPackageStatus(package, F("updates.upgradeFailed", SummarizeFailure(result)), Colors.IndianRed);
                }
            }
        }
        finally
        {
            _isUpgrading = false;
            SetUpdateControlsEnabled(true);
        }

        MainWindow.SetStatusText(F("updates.upgradeSummary", succeeded, failed));
    }

    private async Task<bool> ConfirmUpgradeAsync(int packageCount)
    {
        var dialog = new ContentDialog
        {
            Title = F("updates.confirmUpgradeTitle", packageCount),
            Content = new TextBlock
            {
                Text = F("updates.confirmUpgradeBody", packageCount),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = T("updates.upgradeOne"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void SetUpdateControlsEnabled(bool isEnabled)
    {
        if (_scanButton != null)
        {
            _scanButton.IsEnabled = isEnabled;
        }

        if (_upgradeAllButton != null)
        {
            _upgradeAllButton.IsEnabled = isEnabled;
        }
    }

    private void SetPackageStatus(WingetPackage package, string text, Color color)
    {
        if (_packageStatusLabels.TryGetValue(PackageKey(package), out var statusText))
        {
            statusText.Text = text;
            statusText.Foreground = Brush(color);
        }
    }

    private static string PackageKey(WingetPackage package)
    {
        return $"{package.Source}|{package.Id}";
    }

    private static string SummarizeFailure(WingetPackageUpgradeResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.StandardError;
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? result.StandardOutput;
        }

        return $"Exit code {result.ExitCode}";
    }
}
