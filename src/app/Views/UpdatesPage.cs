using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class UpdatesPage : BasePage
{
    private StackPanel? _resultPanel;
    private TextBox? _searchBox;
    private ComboBox? _sourceFilterBox;
    private ComboBox? _sortBox;
    private Button? _scanButton;
    private Button? _upgradeAllButton;
    private Button? _cancelUpgradeButton;
    private CheckBox? _saveInstallersCheckBox;
    private List<WingetPackage> _packages = [];
    private readonly Dictionary<string, TextBlock> _packageStatusLabels = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpgrading;
    private CancellationTokenSource? _upgradeCancellation;
    private int _appliedRevision = -1;

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

        _cancelUpgradeButton = ActionButton(T("updates.cancelUpgrade"), Symbol.Stop, (_, _) =>
        {
            _upgradeCancellation?.Cancel();
        });
        _cancelUpgradeButton.IsEnabled = false;
        actions.Children.Add(_cancelUpgradeButton);

        MainContent.Children.Add(actions);
        MainContent.Children.Add(UpdateOptionsPanel());
        MainContent.Children.Add(_resultPanel);
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!MainWindow.SessionState.UpdatesLoaded)
        {
            await MainWindow.RefreshUpdatesStateAsync();
        }

        if (_appliedRevision != MainWindow.SessionState.UpdatesRevision)
        {
            ApplyCachedUpdatesState();
        }
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
        _searchBox.TextChanged += (_, _) => DebounceUiAction("updates-search", RenderPackages);
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

        _saveInstallersCheckBox = new CheckBox
        {
            Content = T("updates.saveInstallers"),
            IsChecked = false
        };
        panel.Children.Add(_saveInstallersCheckBox);
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

        CancelDebouncedUiAction("updates-search");
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
        await MainWindow.RefreshUpdatesStateAsync();
        ApplyCachedUpdatesState();
        SetUpdateControlsEnabled(true);
        MainWindow.SetStatusText(T("common.ready"));
    }

    private void ApplyCachedUpdatesState()
    {
        if (_resultPanel == null)
        {
            return;
        }

        var state = MainWindow.SessionState;
        _packages = state.UpdatePackages.ToList();
        _appliedRevision = state.UpdatesRevision;
        if (!string.IsNullOrWhiteSpace(state.UpdatesError))
        {
            _resultPanel.Children.Clear();
            _resultPanel.Children.Add(InfoBlock(F("updates.scanFailed", state.UpdatesError)));
            return;
        }

        RenderPackages();
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

        var mainLayout = new StackPanel { Spacing = 10 };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftPanel = new Grid { ColumnSpacing = 12 };
        leftPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        leftPanel.ColumnDefinitions.Add(new ColumnDefinition());

        var iconContainer = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        var fallbackIcon = new Viewbox { Width = 24, Height = 24, Child = new SymbolIcon(Symbol.Setting) { Opacity = 0.5 } };
        var actualIcon = new Image { Width = 32, Height = 32, Visibility = Visibility.Collapsed };
        iconContainer.Children.Add(fallbackIcon);
        iconContainer.Children.Add(actualIcon);
        Grid.SetColumn(iconContainer, 0);
        leftPanel.Children.Add(iconContainer);

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = package.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{package.Id}\n{package.InstalledVersion} -> {package.AvailableVersion} / {package.Source}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        Grid.SetColumn(details, 1);
        leftPanel.Children.Add(details);

        grid.Children.Add(leftPanel);

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
            catch {}
        });
        actions.Children.Add(openButton);

        var detailsContent = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed, Margin = new Thickness(44, 4, 0, 0) };
        var progressRing = new ProgressRing { IsActive = true, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 10) };
        var infoPanel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        detailsContent.Children.Add(progressRing);
        detailsContent.Children.Add(infoPanel);

        bool detailsLoaded = false;
        var infoButton = IconButton(Symbol.List, T("storage.details"), async (_, _) =>
        {
            if (detailsContent.Visibility == Visibility.Collapsed)
            {
                detailsContent.Visibility = Visibility.Visible;
                if (!detailsLoaded)
                {
                    progressRing.Visibility = Visibility.Visible;
                    infoPanel.Visibility = Visibility.Collapsed;

                    var pkgDetails = await MainWindow.Winget.ShowPackageAsync(package.Id);
                    if (pkgDetails != null)
                    {
                        infoPanel.Children.Clear();

                        if (!string.IsNullOrWhiteSpace(pkgDetails.Publisher))
                        {
                            var pubPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                            pubPanel.Children.Add(new TextBlock { Text = "Publisher:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Width = 110 });
                            pubPanel.Children.Add(string.IsNullOrWhiteSpace(pkgDetails.PublisherUrl) 
                                ? new TextBlock { Text = pkgDetails.Publisher }
                                : CreateLink(pkgDetails.Publisher, pkgDetails.PublisherUrl));
                            infoPanel.Children.Add(pubPanel);
                        }

                        if (!string.IsNullOrWhiteSpace(pkgDetails.Homepage))
                        {
                            var hpPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                            hpPanel.Children.Add(new TextBlock { Text = "Homepage:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Width = 110 });
                            hpPanel.Children.Add(CreateLink(pkgDetails.Homepage, pkgDetails.Homepage));
                            infoPanel.Children.Add(hpPanel);

                            try
                            {
                                var domain = new Uri(pkgDetails.Homepage).Host;
                                actualIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri($"https://www.google.com/s2/favicons?sz=64&domain={domain}"));
                                actualIcon.Visibility = Visibility.Visible;
                                fallbackIcon.Visibility = Visibility.Collapsed;
                            }
                            catch {}
                        }

                        if (!string.IsNullOrWhiteSpace(pkgDetails.License))
                        {
                            var licPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                            licPanel.Children.Add(new TextBlock { Text = "License:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Width = 110 });
                            licPanel.Children.Add(string.IsNullOrWhiteSpace(pkgDetails.LicenseUrl)
                                ? new TextBlock { Text = pkgDetails.License }
                                : CreateLink(pkgDetails.License, pkgDetails.LicenseUrl));
                            infoPanel.Children.Add(licPanel);
                        }

                        if (!string.IsNullOrWhiteSpace(pkgDetails.Description))
                        {
                            var descPanel = new StackPanel { Spacing = 2 };
                            descPanel.Children.Add(new TextBlock { Text = "Description:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                            descPanel.Children.Add(new TextBlock { Text = pkgDetails.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
                            infoPanel.Children.Add(descPanel);
                        }

                        if (!string.IsNullOrWhiteSpace(pkgDetails.ReleaseNotes))
                        {
                            var rnPanel = new StackPanel { Spacing = 2 };
                            rnPanel.Children.Add(new TextBlock { Text = "Release Notes:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                            rnPanel.Children.Add(new TextBlock { Text = pkgDetails.ReleaseNotes, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
                            infoPanel.Children.Add(rnPanel);
                        }

                        detailsLoaded = true;
                    }
                    else
                    {
                        infoPanel.Children.Clear();
                        infoPanel.Children.Add(new TextBlock { Text = "Failed to load package details from Winget.", Foreground = Brush(Colors.Red) });
                    }

                    progressRing.Visibility = Visibility.Collapsed;
                    infoPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                detailsContent.Visibility = Visibility.Collapsed;
            }
        });
        actions.Children.Insert(0, infoButton);

        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        mainLayout.Children.Add(grid);
        mainLayout.Children.Add(detailsContent);

        border.Child = mainLayout;
        return border;
    }

    private static UIElement CreateLink(string text, string url)
    {
        try
        {
            return new HyperlinkButton
            {
                Content = text,
                NavigateUri = new Uri(url),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        catch
        {
            return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        }
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

        string? downloadDirectory = null;
        if (selectedPackages == null && _saveInstallersCheckBox?.IsChecked == true)
        {
            downloadDirectory = await PickUpdateDownloadFolderAsync();
            if (downloadDirectory == null)
            {
                MainWindow.SetStatusText(T("common.ready"));
                return;
            }
        }

        if (!await ConfirmUpgradeAsync(packages.Count))
        {
            return;
        }

        _isUpgrading = true;
        _upgradeCancellation?.Dispose();
        _upgradeCancellation = new CancellationTokenSource();
        var cancellationToken = _upgradeCancellation.Token;
        SetUpdateControlsEnabled(false);

        var started = DateTimeOffset.Now;
        var succeeded = 0;
        var failed = 0;
        var cancelled = false;
        var reportMessages = new List<string>();
        var reportErrors = new List<string>();

        try
        {
            for (var index = 0; index < packages.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var package = packages[index];
                var progressText = F("updates.upgradeProgress", index + 1, packages.Count, package.Name);
                MainWindow.SetStatusText(progressText);
                SetPackageStatus(package, progressText, Colors.DeepSkyBlue);

                WingetPackageUpgradeResult result;
                try
                {
                    if (downloadDirectory != null)
                    {
                        var downloadProgress = F("updates.downloadProgress", index + 1, packages.Count, package.Name);
                        MainWindow.SetStatusText(downloadProgress);
                        SetPackageStatus(package, downloadProgress, Colors.DeepSkyBlue);
                        var downloadResult = await MainWindow.Winget.DownloadPackageAsync(
                            package,
                            downloadDirectory,
                            cancellationToken);
                        if (!downloadResult.Success)
                        {
                            throw new InvalidOperationException(F("updates.downloadFailed", SummarizeFailure(downloadResult)));
                        }
                    }

                    MainWindow.SetStatusText(progressText);
                    SetPackageStatus(package, progressText, Colors.DeepSkyBlue);
                    result = await MainWindow.Winget.UpgradePackageAsync(package, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    SetPackageStatus(package, T("common.cancelled"), Colors.DarkOrange);
                    break;
                }
                catch (Exception ex)
                {
                    result = new WingetPackageUpgradeResult(package, false, -1, string.Empty, ex.Message);
                }

                if (result.Success)
                {
                    succeeded++;
                    reportMessages.Add($"{package.Name} ({package.Id}): {package.InstalledVersion} -> {package.AvailableVersion}");
                    SetPackageStatus(package, T("updates.upgradeSucceeded"), Colors.MediumSeaGreen);
                }
                else
                {
                    failed++;
                    var failure = SummarizeFailure(result);
                    reportErrors.Add($"{package.Name} ({package.Id}): {failure}");
                    SetPackageStatus(package, F("updates.upgradeFailed", failure), Colors.IndianRed);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }
        finally
        {
            _isUpgrading = false;
            SetUpdateControlsEnabled(true);
            _upgradeCancellation.Dispose();
            _upgradeCancellation = null;
        }

        var notProcessed = Math.Max(0, packages.Count - succeeded - failed);
        if (cancelled)
        {
            reportMessages.Add($"Upgrade cancelled with {notProcessed:N0} package(s) not processed.");
        }

        var completionStatus = cancelled
            ? F("updates.upgradeCancelled", succeeded, failed, notProcessed)
            : F("updates.upgradeSummary", succeeded, failed);
        try
        {
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                downloadDirectory is null ? "software.update" : "software.update.download",
                downloadDirectory is null ? "Application Updates" : "Download and Update Applications",
                started,
                DateTimeOffset.Now,
                failed == 0 && !cancelled,
                0,
                succeeded,
                failed + notProcessed,
                reportMessages,
                reportErrors));
            await MainWindow.RefreshUpdatesStateAsync();
            ApplyCachedUpdatesState();
        }
        finally
        {
            MainWindow.SetStatusText(completionStatus, isBusy: false);
        }
    }

    private Task<string?> PickUpdateDownloadFolderAsync()
    {
        var path = FolderPickerHelper.PickFolder(
            MainWindow.WindowHandle,
            FolderPickerHelper.GetDownloadsFolder());
        return Task.FromResult(path);
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

        if (_saveInstallersCheckBox != null)
        {
            _saveInstallersCheckBox.IsEnabled = isEnabled;
        }

        if (_cancelUpgradeButton != null)
        {
            _cancelUpgradeButton.IsEnabled = !isEnabled && _isUpgrading;
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
