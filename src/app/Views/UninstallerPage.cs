using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinOptimizationApp.Models;
using WinOptimizationApp.ViewModels;

namespace WinOptimizationApp.Views;

public sealed partial class UninstallerPage : BasePage
{
    private TextBox? _searchBox;
    private ComboBox? _sourceFilterBox;
    private Button? _scanButton;
    private TextBlock? _headerText;
    private TextBlock? _emptyTextBlock;
    private ListView? _listView;
    private bool _hasScannedOnce;

    public UninstallerViewModel ViewModel { get; }

    public UninstallerPage(MainWindow mainWindow) : base(mainWindow)
    {
        ViewModel = new UninstallerViewModel(
            mainWindow.Uninstaller,
            mainWindow.Operations,
            T,
            F);

        ViewModel.PropertyChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(UpdateControlStates);
        };

        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader(T("uninstaller.title"), T("uninstaller.subtitle"));

        var filterBar = new Grid { ColumnSpacing = 10 };
        filterBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filterBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new TextBox
        {
            PlaceholderText = T("uninstaller.searchPlaceholder"),
            Height = 36
        };
        ConfigureSearch(_searchBox, "uninstaller-search", () =>
        {
            ViewModel.SearchQuery = _searchBox.Text ?? string.Empty;
        });
        Grid.SetColumn(_searchBox, 0);
        filterBar.Children.Add(_searchBox);

        _sourceFilterBox = new ComboBox { MinWidth = 150, Height = 36 };
        _sourceFilterBox.Items.Add(new ComboBoxItem { Content = T("uninstaller.filterAll"), Tag = "All" });
        _sourceFilterBox.Items.Add(new ComboBoxItem { Content = "WinGet", Tag = "Winget" });
        _sourceFilterBox.Items.Add(new ComboBoxItem { Content = "Registry (Win32)", Tag = "Registry" });
        _sourceFilterBox.SelectedIndex = 0;
        _sourceFilterBox.SelectionChanged += (_, _) =>
        {
            if (_sourceFilterBox.SelectedItem is ComboBoxItem { Tag: string tag })
            {
                ViewModel.SourceFilter = tag;
            }
        };
        Grid.SetColumn(_sourceFilterBox, 1);
        filterBar.Children.Add(_sourceFilterBox);

        _scanButton = ActionButton(T("common.scan"), Symbol.Refresh, async (_, _) =>
        {
            await ScanAppsAsync();
        });
        Grid.SetColumn(_scanButton, 2);
        filterBar.Children.Add(_scanButton);

        MainContent.Children.Add(filterBar);

        _headerText = SectionTitle(T("uninstaller.detected"));
        _headerText.Visibility = Visibility.Collapsed;
        MainContent.Children.Add(_headerText);

        _emptyTextBlock = InfoBlock(T("common.noMatches"));
        _emptyTextBlock.Visibility = Visibility.Collapsed;
        MainContent.Children.Add(_emptyTextBlock);

        _listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            ItemsSource = ViewModel.FilteredItems,
            MaxHeight = 680
        };

        _listView.ContainerContentChanging += (_, args) =>
        {
            if (args.Item is InstalledApp app)
            {
                args.ItemContainer.Content = CreateAppRow(app);
            }
        };

        MainContent.Children.Add(_listView);
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!_hasScannedOnce && !ViewModel.IsWorking)
        {
            _hasScannedOnce = true;
            await ScanAppsAsync();
        }
    }

    private async Task ScanAppsAsync()
    {
        if (ViewModel.IsWorking)
        {
            return;
        }

        MainWindow.SetStatusText(T("uninstaller.scanning"));
        try
        {
            await ViewModel.ScanAppsAsync();
            UpdateControlStates();
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"), isBusy: false);
        }
    }

    private Border CreateAppRow(InstalledApp app)
    {
        var border = new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoStack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleRow.Children.Add(Badge(app.Source, app.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Colors.DodgerBlue)
            : new SolidColorBrush(Colors.SlateGray)));
        infoStack.Children.Add(titleRow);

        var details = string.IsNullOrWhiteSpace(app.Publisher)
            ? app.Version
            : $"{app.Publisher} • {app.Version}";
        infoStack.Children.Add(new TextBlock
        {
            Text = details,
            FontSize = 12,
            Opacity = 0.72,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            var openBtn = ActionButton(T("common.openFolder"), Symbol.Folder, (_, _) =>
            {
                MainWindow.OpenFolder_Internal(app.InstallLocation);
            });
            actions.Children.Add(openBtn);
        }

        var uninstallBtn = ActionButton(T("uninstaller.uninstallButton"), Symbol.Delete, async (_, _) =>
        {
            await ConfirmAndUninstallAsync(app);
        });
        actions.Children.Add(uninstallBtn);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        border.Child = grid;
        return border;
    }

    private async Task ConfirmAndUninstallAsync(InstalledApp app)
    {
        var confirmDialog = new ContentDialog
        {
            Title = T("uninstaller.confirmTitle"),
            Content = F("uninstaller.confirmMessage", app.Name, app.Publisher),
            PrimaryButtonText = T("uninstaller.uninstallButton"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close
        };

        var result = await MainWindow.ShowThemedDialogAsync(confirmDialog);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var succeeded = await ViewModel.UninstallAppAsync(app);
        if (!succeeded)
        {
            MainWindow.ShowNotification(
                T("uninstaller.title"),
                F("uninstaller.failedToast", app.Name),
                InfoBarSeverity.Warning);
            return;
        }

        MainWindow.ShowNotification(
            T("uninstaller.title"),
            F("uninstaller.successToast", app.Name),
            InfoBarSeverity.Success);

        var leftovers = await ViewModel.ScanLeftoversAsync(app);
        if (leftovers.Count > 0)
        {
            await PromptLeftoverCleanupAsync(app, leftovers);
        }
    }

    private async Task PromptLeftoverCleanupAsync(InstalledApp app, IReadOnlyList<LeftoverCleanupCandidate> leftovers)
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 640 };
        panel.Children.Add(new TextBlock
        {
            Text = F("uninstaller.leftoversFound", app.Name, leftovers.Count),
            TextWrapping = TextWrapping.Wrap
        });

        var checkBoxes = new List<CheckBox>();
        foreach (var item in leftovers)
        {
            var cb = new CheckBox
            {
                Content = $"{item.Path} ({item.Confidence})",
                Tag = item.Path,
                IsChecked = item.Confidence == LeftoverCleanupConfidence.High
            };
            checkBoxes.Add(cb);
            panel.Children.Add(cb);
        }

        var dialog = new ContentDialog
        {
            Title = T("uninstaller.leftoversTitle"),
            Content = new ScrollViewer { MaxHeight = 320, Content = panel },
            PrimaryButtonText = T("uninstaller.cleanLeftovers"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var choice = await MainWindow.ShowThemedDialogAsync(dialog);
        if (choice == ContentDialogResult.Primary)
        {
            var selectedPaths = checkBoxes
                .Where(c => c.IsChecked == true && c.Tag is string)
                .Select(c => (string)c.Tag!)
                .ToList();

            if (selectedPaths.Count > 0)
            {
                var deleted = await ViewModel.DeleteLeftoversAsync(selectedPaths);
                MainWindow.ShowNotification(
                    T("uninstaller.leftoversTitle"),
                    deleted ? F("uninstaller.leftoversCleaned", selectedPaths.Count) : T("uninstaller.leftoversPartial"),
                    deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
        }
    }

    private void UpdateControlStates()
    {
        if (_scanButton is not null)
        {
            _scanButton.IsEnabled = !ViewModel.IsWorking;
        }

        if (_headerText is not null)
        {
            _headerText.Text = $"{T("uninstaller.detected")} ({ViewModel.FilteredCount}/{ViewModel.TotalCount})";
            _headerText.Visibility = ViewModel.TotalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_emptyTextBlock is not null)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                _emptyTextBlock.Text = ViewModel.ErrorMessage;
                _emptyTextBlock.Visibility = Visibility.Visible;
            }
            else if (ViewModel.FilteredCount == 0 && !ViewModel.IsWorking)
            {
                _emptyTextBlock.Text = T("common.noMatches");
                _emptyTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyTextBlock.Visibility = Visibility.Collapsed;
            }
        }
    }
}
