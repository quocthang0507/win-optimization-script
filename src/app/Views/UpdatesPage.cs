using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public sealed partial class UpdatesPage : BasePage
{
    private StackPanel? _resultPanel;
    private TextBox? _searchBox;
    private ComboBox? _sourceFilterBox;
    private ComboBox? _sortBox;
    private List<WingetPackage> _packages = [];

    public UpdatesPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderUpdatesPage();
    }

    private void RenderUpdatesPage()
    {
        AddHeader(T("updates.title"), T("updates.subtitle"));

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        actions.Children.Add(ActionButton(T("updates.scanWinget"), Symbol.Find, async (_, _) =>
        {
            await ScanUpdatesAsync();
        }));

        actions.Children.Add(ActionButton(T("updates.upgradeAll"), Symbol.Download, async (_, _) =>
        {
            var task = MainWindow.Catalog.GetById("software.winget");
            await MainWindow.RunTaskAsync(task);
        }));

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
        if (_resultPanel == null)
        {
            return;
        }

        MainWindow.SetStatusText(T("updates.scanning"));
        _resultPanel.Children.Clear();
        _packages = (await MainWindow.Winget.ScanAsync()).ToList();
        RenderPackages();
        MainWindow.SetStatusText(T("common.ready"));
    }

    private void RenderPackages()
    {
        if (_resultPanel == null)
        {
            return;
        }

        _resultPanel.Children.Clear();
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
        return Card(package.Name, $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}", T("common.open"), (_, _) =>
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
    }
}
