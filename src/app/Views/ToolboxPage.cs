using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class ToolboxPage : BasePage
{
    private CancellationTokenSource? _scanCts;

    // Registry controls
    private StackPanel? _registryResultsPanel;
    private Button? _registryScanBtn;
    private Button? _registryCleanBtn;
    private List<RegistryIssue> _registryIssues = [];

    // Network controls
    private StackPanel? _networkAdaptersPanel;
    private Button? _flushDnsBtn;
    private Button? _resetWinsockBtn;
    private Button? _renewIpBtn;
    private Button? _refreshAdaptersBtn;

    // Uninstaller controls
    private StackPanel? _appsPanel;
    private Button? _appsScanBtn;
    private TextBox? _searchBox;
    private ComboBox? _appsSourceFilterBox;
    private ComboBox? _appsSortBox;
    private CheckBox? _appsWithLocationOnlyBox;
    private List<InstalledApp> _installedApps = [];

    public ToolboxPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderToolboxPage();
    }

    private void RenderToolboxPage()
    {
        AddHeader(T("toolbox.title"), T("toolbox.subtitle"));

        if (!SystemStatusService.IsAdministrator())
        {
            MainContent.Children.Add(CreateAdminWarningBanner(
                T("admin.title"),
                T("admin.bannerDesc")
            ));
        }

        var pivot = new Pivot();

        // 1. Registry Tab
        var registryTab = new PivotItem
        {
            Header = T("toolbox.tab.registry"),
            Content = CreateRegistryLayout()
        };
        pivot.Items.Add(registryTab);

        // 2. Network Tab
        var networkTab = new PivotItem
        {
            Header = T("toolbox.tab.network"),
            Content = CreateNetworkLayout()
        };
        pivot.Items.Add(networkTab);

        // 3. Uninstaller Tab
        var uninstallerTab = new PivotItem
        {
            Header = T("toolbox.tab.uninstaller"),
            Content = CreateUninstallerLayout()
        };
        pivot.Items.Add(uninstallerTab);

        MainContent.Children.Add(pivot);
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
        // Load network adapters automatically
        await RefreshNetworkAdaptersAsync();
    }

    #region Layout Creation Helpers

    private ScrollViewer CreateRegistryLayout()
    {
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(0, 12, 0, 12) };

        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _registryScanBtn = ActionButton(T("registry.scan"), Symbol.Find, async (_, _) => await ScanRegistryAsync());
        _registryCleanBtn = ActionButton(T("registry.clean"), Symbol.Delete, async (_, _) => await CleanRegistryAsync());
        _registryCleanBtn.IsEnabled = false;

        actionPanel.Children.Add(_registryScanBtn);
        actionPanel.Children.Add(_registryCleanBtn);
        panel.Children.Add(actionPanel);

        _registryResultsPanel = new StackPanel { Spacing = 8 };
        panel.Children.Add(_registryResultsPanel);

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private ScrollViewer CreateNetworkLayout()
    {
        var panel = new StackPanel { Spacing = 16, Padding = new Thickness(0, 12, 0, 12) };

        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _flushDnsBtn = ActionButton(T("network.flushDns"), Symbol.Sync, async (_, _) => await RunNetworkActionAsync("FlushDns"));
        _resetWinsockBtn = ActionButton(T("network.resetWinsock"), Symbol.Refresh, async (_, _) => await RunNetworkActionAsync("ResetWinsock"));
        _renewIpBtn = ActionButton(T("network.renewIp"), Symbol.Target, async (_, _) => await RunNetworkActionAsync("RenewIp"));

        actionPanel.Children.Add(_flushDnsBtn);
        actionPanel.Children.Add(_resetWinsockBtn);
        actionPanel.Children.Add(_renewIpBtn);
        panel.Children.Add(actionPanel);

        var adaptersHeader = new Grid();
        adaptersHeader.ColumnDefinitions.Add(new ColumnDefinition());
        adaptersHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = SectionTitle(T("network.adaptersTitle"));
        Grid.SetColumn(title, 0);
        adaptersHeader.Children.Add(title);

        _refreshAdaptersBtn = IconButton(Symbol.Refresh, T("network.adaptersTitle"), async (_, _) => await RefreshNetworkAdaptersAsync());
        _refreshAdaptersBtn.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(_refreshAdaptersBtn, 1);
        adaptersHeader.Children.Add(_refreshAdaptersBtn);

        panel.Children.Add(adaptersHeader);

        _networkAdaptersPanel = new StackPanel { Spacing = 8 };
        panel.Children.Add(_networkAdaptersPanel);

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private ScrollViewer CreateUninstallerLayout()
    {
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(0, 12, 0, 12) };
        var controls = new StackPanel { Spacing = 10 };

        var searchGrid = new Grid { ColumnSpacing = 12 };
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new TextBox
        {
            PlaceholderText = T("uninstaller.searchPlaceholder"),
            Height = 36
        };
        _searchBox.TextChanged += (s, e) => FilterAppsList();
        Grid.SetColumn(_searchBox, 0);
        searchGrid.Children.Add(_searchBox);

        var resetButton = ActionButton(T("common.resetFilters"), Symbol.Refresh, (_, _) => ResetAppFilters());
        Grid.SetColumn(resetButton, 1);
        searchGrid.Children.Add(resetButton);

        _appsScanBtn = ActionButton(T("uninstaller.scan"), Symbol.Find, async (_, _) => await ScanAppsAsync());
        Grid.SetColumn(_appsScanBtn, 2);
        searchGrid.Children.Add(_appsScanBtn);

        controls.Children.Add(searchGrid);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _appsSourceFilterBox = new ComboBox
        {
            Header = T("uninstaller.sourceFilter"),
            MinWidth = 160
        };
        _appsSourceFilterBox.Items.Add(T("uninstaller.allSources"));
        _appsSourceFilterBox.Items.Add(T("uninstaller.registrySource"));
        _appsSourceFilterBox.Items.Add(T("uninstaller.wingetSource"));
        _appsSourceFilterBox.SelectedIndex = 0;
        _appsSourceFilterBox.SelectionChanged += (_, _) => FilterAppsList();
        filterRow.Children.Add(_appsSourceFilterBox);

        _appsSortBox = new ComboBox
        {
            Header = T("uninstaller.sort"),
            MinWidth = 180
        };
        _appsSortBox.Items.Add(T("uninstaller.sortName"));
        _appsSortBox.Items.Add(T("uninstaller.sortPublisher"));
        _appsSortBox.Items.Add(T("uninstaller.sortSource"));
        _appsSortBox.Items.Add(T("uninstaller.sortLocation"));
        _appsSortBox.SelectedIndex = 0;
        _appsSortBox.SelectionChanged += (_, _) => FilterAppsList();
        filterRow.Children.Add(_appsSortBox);

        _appsWithLocationOnlyBox = new CheckBox
        {
            Content = T("uninstaller.withLocationOnly"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _appsWithLocationOnlyBox.Checked += (_, _) => FilterAppsList();
        _appsWithLocationOnlyBox.Unchecked += (_, _) => FilterAppsList();
        filterRow.Children.Add(_appsWithLocationOnlyBox);

        controls.Children.Add(filterRow);
        panel.Children.Add(controls);

        _appsPanel = new StackPanel { Spacing = 8 };
        panel.Children.Add(_appsPanel);

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private void ResetAppFilters()
    {
        if (_searchBox is not null)
        {
            _searchBox.Text = string.Empty;
        }

        if (_appsSourceFilterBox is not null)
        {
            _appsSourceFilterBox.SelectedIndex = 0;
        }

        if (_appsSortBox is not null)
        {
            _appsSortBox.SelectedIndex = 0;
        }

        if (_appsWithLocationOnlyBox is not null)
        {
            _appsWithLocationOnlyBox.IsChecked = false;
        }

        FilterAppsList();
    }

    #endregion

    #region Registry Operations

    private async Task ScanRegistryAsync()
    {
        if (_registryScanBtn == null || _registryResultsPanel == null || _registryCleanBtn == null)
        {
            return;
        }

        ResetCts();
        MainWindow.SetStatusText(T("registry.scanning"));
        _registryResultsPanel.Children.Clear();
        _registryCleanBtn.IsEnabled = false;

        try
        {
            _registryIssues = (await MainWindow.RegistryCleaner.ScanAsync()).ToList();

            if (_registryIssues.Count == 0)
            {
                _registryResultsPanel.Children.Add(new TextBlock { Text = T("registry.noIssues"), Margin = new Thickness(0, 12, 0, 0) });
            }
            else
            {
                _registryResultsPanel.Children.Add(SectionTitle(F("registry.issuesFound", _registryIssues.Count)));

                foreach (var issue in _registryIssues)
                {
                    _registryResultsPanel.Children.Add(CreateRegistryIssueRow(issue));
                }

                UpdateRegistryCleanButtonState();
            }
        }
        catch (Exception ex)
        {
            _registryResultsPanel.Children.Add(new TextBlock { Text = $"Error scanning registry: {ex.Message}", Foreground = Brush(Colors.IndianRed) });
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private Border CreateRegistryIssueRow(RegistryIssue issue)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox { IsChecked = issue.IsSelected, VerticalAlignment = VerticalAlignment.Center };
        check.Checked += (s, e) => { issue.IsSelected = true; UpdateRegistryCleanButtonState(); };
        check.Unchecked += (s, e) => { issue.IsSelected = false; UpdateRegistryCleanButtonState(); };
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var infoPanel = new StackPanel { Spacing = 4 };
        infoPanel.Children.Add(new TextBlock
        {
            Text = issue.Category,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = issue.Description,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.82
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = issue.KeyPath,
            FontFamily = new FontFamily("Cascadia Mono"),
            FontSize = 11,
            Opacity = 0.58,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(infoPanel, 1);
        grid.Children.Add(infoPanel);

        Grid.SetColumn(grid, 1);

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = grid,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private void UpdateRegistryCleanButtonState()
    {
        _registryCleanBtn?.IsEnabled = _registryIssues.Any(i => i.IsSelected);
    }

    private async Task CleanRegistryAsync()
    {
        var selected = _registryIssues.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var confirmDialog = new ContentDialog
        {
            Title = T("registry.confirmCleanTitle"),
            Content = new TextBlock
            {
                Text = T("registry.confirmCleanMessage"),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = T("registry.clean"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var dialogResult = await confirmDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
        {
            return;
        }

        MainWindow.SetStatusText(T("registry.cleaning"));
        var started = DateTimeOffset.Now;
        try
        {
            var success = await MainWindow.RegistryCleaner.CleanAsync(selected);
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                "registry.cleanup",
                "Registry Cleanup",
                started,
                DateTimeOffset.Now,
                success,
                0,
                success ? selected.Count : 0,
                success ? 0 : selected.Count,
                success ? selected.Select(issue => issue.Id).ToList() : [],
                success ? [] : ["One or more registry entries were not changed; backups were retained."]));
            if (success)
            {
                var completeDialog = new ContentDialog
                {
                    Title = T("run.completed"),
                    Content = new TextBlock { Text = T("registry.completed") },
                    CloseButtonText = T("common.close"),
                    XamlRoot = XamlRoot
                };
                await completeDialog.ShowAsync();
                await ScanRegistryAsync();
            }
            else
            {
                var failedDialog = new ContentDialog
                {
                    Title = T("registry.failedTitle"),
                    Content = new TextBlock { Text = T("registry.failed"), TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = T("common.close"),
                    XamlRoot = XamlRoot
                };
                await failedDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                "registry.cleanup",
                "Registry Cleanup",
                started,
                DateTimeOffset.Now,
                false,
                0,
                0,
                selected.Count,
                [],
                [ex.Message]));
            await MainWindow.ShowDialogAsync_Internal(
                T("registry.failedTitle"),
                InfoBlock(ex.Message),
                T("common.close"));
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    #endregion

    #region Network Operations

    private async Task RunNetworkActionAsync(string actionName)
    {
        if (actionName is "ResetWinsock" or "RenewIp")
        {
            var confirmation = new ContentDialog
            {
                Title = T("network.confirmTitle"),
                Content = new TextBlock
                {
                    Text = T(actionName == "ResetWinsock" ? "network.confirmWinsock" : "network.confirmRenewIp"),
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = T("common.run"),
                CloseButtonText = T("common.cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        MainWindow.SetStatusText(T("network.runningAction"));
        DisableNetworkButtons(false);
        var started = DateTimeOffset.Now;

        try
        {
            bool success = actionName switch
            {
                "FlushDns" => await MainWindow.NetworkOptimizer.FlushDnsAsync(),
                "ResetWinsock" => await MainWindow.NetworkOptimizer.ResetWinsockAsync(),
                "RenewIp" => await MainWindow.NetworkOptimizer.RenewIpAsync(),
                _ => false
            };

            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                $"network.{actionName.ToLowerInvariant()}",
                actionName,
                started,
                DateTimeOffset.Now,
                success,
                0,
                success ? 1 : 0,
                success ? 0 : 1,
                success ? [$"{actionName} completed."] : [],
                success ? [] : [$"{actionName} failed."]));

            if (success)
            {
                var msgKey = actionName switch
                {
                    "FlushDns" => "network.flushed",
                    "ResetWinsock" => "network.winsockReset",
                    "RenewIp" => "network.ipRenewed",
                    _ => string.Empty
                };

                var dialog = new ContentDialog
                {
                    Title = T("run.completed"),
                    Content = new TextBlock { Text = T(msgKey) },
                    CloseButtonText = T("common.close"),
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = T("network.actionFailed"),
                    Content = new TextBlock { Text = T("network.actionFailed") },
                    CloseButtonText = T("common.close"),
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                $"network.{actionName.ToLowerInvariant()}",
                actionName,
                started,
                DateTimeOffset.Now,
                false,
                0,
                0,
                1,
                [],
                [ex.Message]));
            await MainWindow.ShowDialogAsync_Internal(
                T("network.actionFailed"),
                InfoBlock(ex.Message),
                T("common.close"));
        }
        finally
        {
            DisableNetworkButtons(true);
            MainWindow.SetStatusText(T("common.ready"));
            await RefreshNetworkAdaptersAsync();
        }
    }

    private void DisableNetworkButtons(bool enable)
    {
        _flushDnsBtn?.IsEnabled = enable;
        _resetWinsockBtn?.IsEnabled = enable;
        _renewIpBtn?.IsEnabled = enable;
        _refreshAdaptersBtn?.IsEnabled = enable;
    }

    private async Task RefreshNetworkAdaptersAsync()
    {
        if (_networkAdaptersPanel == null)
        {
            return;
        }

        _networkAdaptersPanel.Children.Clear();
        _networkAdaptersPanel.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center });

        try
        {
            var adapters = await MainWindow.NetworkOptimizer.GetAdaptersAsync();
            _networkAdaptersPanel.Children.Clear();

            foreach (var adapter in adapters)
            {
                _networkAdaptersPanel.Children.Add(CreateNetworkAdapterRow(adapter));
            }
        }
        catch (Exception ex)
        {
            _networkAdaptersPanel.Children.Clear();
            _networkAdaptersPanel.Children.Add(new TextBlock { Text = $"Error loading network cards: {ex.Message}", Foreground = Brush(Colors.IndianRed) });
        }
    }

    private Border CreateNetworkAdapterRow(NetworkAdapterInfo adapter)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoPanel = new StackPanel { Spacing = 4 };
        infoPanel.Children.Add(new TextBlock { Text = adapter.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        infoPanel.Children.Add(new TextBlock { Text = adapter.Description, Opacity = 0.72, TextWrapping = TextWrapping.Wrap });

        var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        detailsPanel.Children.Add(new TextBlock { Text = $"{T("network.adapter.speed")}: {adapter.Speed}", FontSize = 12, Opacity = 0.62 });
        detailsPanel.Children.Add(new TextBlock { Text = $"{T("network.adapter.mac")}: {adapter.MacAddress}", FontSize = 12, Opacity = 0.62 });
        detailsPanel.Children.Add(new TextBlock { Text = $"{T("network.adapter.ip")}: {adapter.IpAddress}", FontSize = 12, Opacity = 0.62 });
        infoPanel.Children.Add(detailsPanel);
        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        var isUp = adapter.Status.Equals("Up", StringComparison.OrdinalIgnoreCase);
        var statusColor = isUp ? Colors.SeaGreen : Colors.IndianRed;

        var statusBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Background = Brush(Color.FromArgb(30, statusColor.R, statusColor.G, statusColor.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = adapter.Status,
                Foreground = Brush(statusColor),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12
            }
        };
        Grid.SetColumn(statusBadge, 1);
        grid.Children.Add(statusBadge);

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = grid,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    #endregion

    #region Uninstaller Operations

    private async Task ScanAppsAsync()
    {
        if (_appsScanBtn == null || _appsPanel == null)
        {
            return;
        }

        ResetCts();
        MainWindow.SetStatusText(T("uninstaller.scanning"));
        _appsPanel.Children.Clear();
        _appsPanel.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center });

        try
        {
            _installedApps = (await MainWindow.Uninstaller.ScanInstalledAppsAsync(_scanCts!.Token)).ToList();
            FilterAppsList();
        }
        catch (OperationCanceledException)
        {
            _appsPanel.Children.Clear();
            _appsPanel.Children.Add(new TextBlock { Text = T("storage.scanCanceled") });
        }
        catch (Exception ex)
        {
            _appsPanel.Children.Clear();
            _appsPanel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}", Foreground = Brush(Colors.IndianRed) });
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private void FilterAppsList()
    {
        if (_appsPanel == null)
        {
            return;
        }

        _appsPanel.Children.Clear();

        var filtered = SortInstalledApps(FilterInstalledApps(_installedApps)).ToList();

        _appsPanel.Children.Add(SectionTitle(F("uninstaller.appsCountFiltered", filtered.Count, _installedApps.Count)));

        if (filtered.Count == 0)
        {
            _appsPanel.Children.Add(InfoBlock(T("common.noMatches")));
            return;
        }

        foreach (var app in filtered.Take(150)) // Limit display to 150 items for UI performance
        {
            _appsPanel.Children.Add(CreateAppRow(app));
        }

        if (filtered.Count > 150)
        {
            _appsPanel.Children.Add(new TextBlock { Text = F("preview.moreTargets", filtered.Count - 150), Opacity = 0.65, Margin = new Thickness(0, 6, 0, 0) });
        }
    }

    private IEnumerable<InstalledApp> FilterInstalledApps(IEnumerable<InstalledApp> apps)
    {
        var query = _searchBox?.Text?.Trim() ?? string.Empty;
        var sourceIndex = _appsSourceFilterBox?.SelectedIndex ?? 0;
        var withLocationOnly = _appsWithLocationOnlyBox?.IsChecked == true;

        foreach (var app in apps)
        {
            if (!MatchesInstalledAppQuery(app, query))
            {
                continue;
            }

            if (sourceIndex == 1 && !app.Source.Equals("Registry", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceIndex == 2 && !app.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (withLocationOnly && string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                continue;
            }

            yield return app;
        }
    }

    private IEnumerable<InstalledApp> SortInstalledApps(IEnumerable<InstalledApp> apps)
    {
        return (_appsSortBox?.SelectedIndex ?? 0) switch
        {
            1 => apps
                .OrderBy(app => app.Publisher, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => apps
                .OrderBy(app => app.Source, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
            3 => apps
                .OrderBy(app => string.IsNullOrWhiteSpace(app.InstallLocation))
                .ThenBy(app => app.InstallLocation, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private static bool MatchesInstalledAppQuery(InstalledApp app, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.Version.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.InstallLocation.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private Border CreateAppRow(InstalledApp app)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoPanel = new StackPanel { Spacing = 4 };
        infoPanel.Children.Add(new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        detailsPanel.Children.Add(new TextBlock { Text = app.Version, FontSize = 12, Opacity = 0.62 });
        detailsPanel.Children.Add(new TextBlock { Text = app.Publisher, FontSize = 12, Opacity = 0.62 });
        detailsPanel.Children.Add(new TextBlock { Text = app.Source, FontSize = 12, Opacity = 0.62 });
        infoPanel.Children.Add(detailsPanel);

        if (!string.IsNullOrEmpty(app.InstallLocation))
        {
            infoPanel.Children.Add(new TextBlock { Text = app.InstallLocation, FontSize = 11, Opacity = 0.5, TextTrimming = TextTrimming.CharacterEllipsis });
        }

        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        var uninstallBtn = new Button { Content = T("uninstaller.uninstall"), MinWidth = 96 };
        uninstallBtn.Click += async (_, _) => await TriggerUninstallAsync(app);
        Grid.SetColumn(uninstallBtn, 1);
        grid.Children.Add(uninstallBtn);

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = grid,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private async Task TriggerUninstallAsync(InstalledApp app)
    {
        var confirmDialog = new ContentDialog
        {
            Title = T("uninstaller.confirmUninstallTitle"),
            Content = new TextBlock
            {
                Text = F("uninstaller.confirmUninstallMessage", app.Name),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = T("uninstaller.uninstall"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        MainWindow.SetStatusText(F("uninstaller.uninstalling", app.Name));
        var started = DateTimeOffset.Now;
        var uninstallCompleted = false;

        try
        {
            var success = await MainWindow.Uninstaller.UninstallAppAsync(app);
            if (success)
            {
                uninstallCompleted = true;
                await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                    "software.uninstall",
                    "Software Uninstall",
                    started,
                    DateTimeOffset.Now,
                    true,
                    0,
                    1,
                    0,
                    [$"{app.Name} ({app.Id}): uninstalled"],
                    []));
                // Uninstallation successful, now scan leftovers
                MainWindow.SetStatusText(T("common.loading"));
                var leftovers = (await MainWindow.Uninstaller.ScanLeftoversAsync(app)).ToList();

                if (leftovers.Count > 0)
                {
                    // Prompt leftovers deletion
                    var leftoversListPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
                    foreach (var path in leftovers)
                    {
                        leftoversListPanel.Children.Add(new TextBlock
                        {
                            Text = path,
                            FontSize = 12,
                            Opacity = 0.8,
                            TextWrapping = TextWrapping.Wrap
                        });
                    }

                    var scrollViewer = new ScrollViewer
                    {
                        Content = leftoversListPanel,
                        MaxHeight = 240,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                    var leftoversPanel = new StackPanel { Spacing = 8 };
                    leftoversPanel.Children.Add(new TextBlock
                    {
                        Text = F("uninstaller.leftoversMessage", app.Name),
                        TextWrapping = TextWrapping.Wrap
                    });
                    leftoversPanel.Children.Add(scrollViewer);

                    var leftoversDialog = new ContentDialog
                    {
                        Title = T("uninstaller.leftoversTitle"),
                        Content = leftoversPanel,
                        PrimaryButtonText = T("uninstaller.deleteLeftovers"),
                        CloseButtonText = T("uninstaller.keepLeftovers"),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot
                    };

                    if (await leftoversDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        MainWindow.SetStatusText(T("storage.cleaning"));
                        var cleanupStarted = DateTimeOffset.Now;
                        var cleanSuccess = await MainWindow.Uninstaller.DeleteLeftoversAsync(leftovers);
                        await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                            "software.leftovers.recycle",
                            "Move Software Leftovers to Recycle Bin",
                            cleanupStarted,
                            DateTimeOffset.Now,
                            cleanSuccess,
                            0,
                            cleanSuccess ? leftovers.Count : 0,
                            cleanSuccess ? 0 : leftovers.Count,
                            cleanSuccess ? leftovers : [],
                            cleanSuccess ? [] : ["One or more leftover paths were blocked or could not be moved."]));
                        if (cleanSuccess)
                        {
                            var completeDialog = new ContentDialog
                            {
                                Title = T("run.completed"),
                                Content = new TextBlock { Text = T("uninstaller.leftoversDeleted") },
                                CloseButtonText = T("common.close"),
                                XamlRoot = XamlRoot
                            };
                            await completeDialog.ShowAsync();
                        }
                    }
                }

                // Rescan apps
                await ScanAppsAsync();
            }
            else
            {
                await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                    "software.uninstall",
                    "Software Uninstall",
                    started,
                    DateTimeOffset.Now,
                    false,
                    0,
                    0,
                    1,
                    [],
                    [$"{app.Name} ({app.Id}): uninstall failed"]));
                var failDialog = new ContentDialog
                {
                    Title = T("uninstaller.uninstallFailed"),
                    Content = new TextBlock { Text = T("uninstaller.uninstallFailed") },
                    CloseButtonText = T("common.close"),
                    XamlRoot = XamlRoot
                };
                await failDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                uninstallCompleted ? "software.leftovers.scan" : "software.uninstall",
                uninstallCompleted ? "Software Leftover Scan" : "Software Uninstall",
                started,
                DateTimeOffset.Now,
                false,
                0,
                0,
                1,
                [],
                [$"{app.Name} ({app.Id}): {ex.Message}"]));
            await MainWindow.ShowDialogAsync_Internal(
                T(uninstallCompleted ? "uninstaller.leftoversTitle" : "uninstaller.uninstallFailed"),
                InfoBlock(ex.Message),
                T("common.close"));
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    #endregion

    private void ResetCts()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
    }
}
