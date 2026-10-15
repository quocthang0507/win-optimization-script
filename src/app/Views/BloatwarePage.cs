using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinOptimizationApp.Models;
using WinOptimizationApp.ViewModels;

namespace WinOptimizationApp.Views;

public sealed partial class BloatwarePage : BasePage
{
    private TextBox? _searchBox;
    private Button? _scanButton;
    private Button? _selectAllButton;
    private Button? _clearSelectionButton;
    private Button? _removeButton;
    private TextBlock? _headerText;
    private TextBlock? _emptyTextBlock;
    private ListView? _listView;
    private int _appliedRevision = -1;

    public BloatwareViewModel ViewModel { get; }

    public BloatwarePage(MainWindow mainWindow) : base(mainWindow)
    {
        ViewModel = new BloatwareViewModel(
            mainWindow.Uninstaller,
            mainWindow.Operations,
            mainWindow.SaveOperationReportAsync,
            T,
            F);

        ViewModel.PropertyChanged += (_, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateControlStates();
            });
        };

        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader(T("bloatware.title"), T("bloatware.subtitle"));

        _searchBox = new TextBox
        {
            PlaceholderText = T("bloatware.searchPlaceholder"),
            Height = 36
        };
        ConfigureSearch(_searchBox, "bloatware-search", () =>
        {
            ViewModel.SearchQuery = _searchBox.Text ?? string.Empty;
        });

        var actions = new AdaptiveWrapPanel { Spacing = 10 };

        _scanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await ScanAppsAsync();
        });
        actions.Children.Add(_scanButton);

        _selectAllButton = ActionButton(T("bloatware.selectAll"), Symbol.SelectAll, (_, _) =>
        {
            ViewModel.SelectAllFiltered();
        });
        actions.Children.Add(_selectAllButton);

        _clearSelectionButton = ActionButton(T("bloatware.clearSelection"), Symbol.Clear, (_, _) =>
        {
            ViewModel.ClearSelection();
        });
        _clearSelectionButton.IsEnabled = false;
        actions.Children.Add(_clearSelectionButton);

        _removeButton = ActionButton(T("bloatware.removeSelected"), Symbol.Delete, async (_, _) =>
        {
            await RemoveSelectedAppsAsync();
        });
        _removeButton.IsEnabled = false;
        actions.Children.Add(_removeButton);

        MainContent.Children.Add(_searchBox);
        MainContent.Children.Add(actions);

        _headerText = SectionTitle(T("bloatware.detected"));
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

        _listView.ContainerContentChanging += (sender, args) =>
        {
            if (args.Item is BloatwareItemViewModel item)
            {
                if (args.ItemContainer.Content is not Border border)
                {
                    args.ItemContainer.Content = CreateAppRow(item);
                }
                else
                {
                    UpdateAppRow(border, item);
                }
            }
        };

        MainContent.Children.Add(_listView);
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!MainWindow.SessionState.AppxLoaded)
        {
            await MainWindow.RefreshAppxStateAsync();
        }

        if (_appliedRevision != MainWindow.SessionState.AppxRevision)
        {
            ApplyCachedAppxState();
        }
    }

    private void ApplyCachedAppxState()
    {
        var state = MainWindow.SessionState;
        _appliedRevision = state.AppxRevision;
        ViewModel.LoadApps(state.AppxPackages, state.AppxError);
        UpdateControlStates();
    }

    private async Task ScanAppsAsync()
    {
        if (ViewModel.IsWorking)
        {
            return;
        }

        MainWindow.SetStatusText(T("bloatware.scanning"));
        try
        {
            await MainWindow.RefreshAppxStateAsync();
            ApplyCachedAppxState();
        }
        finally
        {
            MainWindow.SetStatusText(T("common.ready"), isBusy: false);
        }
    }

    private async Task RemoveSelectedAppsAsync()
    {
        if (ViewModel.IsWorking || ViewModel.SelectedCount == 0)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = T("bloatware.confirmTitle"),
            Content = F("bloatware.confirmBody", ViewModel.SelectedCount),
            PrimaryButtonText = T("bloatware.remove"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await MainWindow.ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var (succeeded, failed) = await ViewModel.RemoveSelectedAppsAsync();
        await MainWindow.RefreshAppxStateAsync();
        ApplyCachedAppxState();
        MainWindow.SetStatusText(F("bloatware.summary", succeeded, failed), isBusy: false);
    }

    private void UpdateControlStates()
    {
        if (_scanButton != null) _scanButton.IsEnabled = !ViewModel.IsWorking;
        if (_searchBox != null) _searchBox.IsEnabled = !ViewModel.IsWorking;
        if (_selectAllButton != null) _selectAllButton.IsEnabled = ViewModel.CanSelectAll;
        if (_clearSelectionButton != null) _clearSelectionButton.IsEnabled = ViewModel.CanClearSelection;
        if (_removeButton != null) _removeButton.IsEnabled = ViewModel.CanRemove;

        if (_emptyTextBlock != null)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                _emptyTextBlock.Text = F("bloatware.scanFailed", ViewModel.ErrorMessage);
                _emptyTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyTextBlock.Text = T("common.noMatches");
                _emptyTextBlock.Visibility = ViewModel.FilteredCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        if (_headerText != null)
        {
            if (ViewModel.FilteredCount > 0)
            {
                _headerText.Text = ViewModel.FilteredCount == ViewModel.TotalCount
                    ? F("bloatware.detected", ViewModel.TotalCount)
                    : F("bloatware.detectedFiltered", ViewModel.FilteredCount, ViewModel.TotalCount);
                _headerText.Visibility = Visibility.Visible;
            }
            else
            {
                _headerText.Visibility = Visibility.Collapsed;
            }
        }
    }

    private Border CreateAppRow(BloatwareItemViewModel item)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Margin = new Thickness(0, 4, 0, 4),
            Tag = item
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var checkbox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = item.IsSelected
        };
        checkbox.Checked += (_, _) => item.IsSelected = true;
        checkbox.Unchecked += (_, _) => item.IsSelected = false;
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var nameBlock = new TextBlock
        {
            Text = item.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        var descBlock = new TextBlock
        {
            Text = item.Details,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        };
        details.Children.Add(nameBlock);
        details.Children.Add(descBlock);
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        border.Child = grid;

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BloatwareItemViewModel.IsSelected))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (checkbox.IsChecked != item.IsSelected)
                    {
                        checkbox.IsChecked = item.IsSelected;
                    }
                });
            }
        };

        return border;
    }

    private void UpdateAppRow(Border border, BloatwareItemViewModel item)
    {
        border.Tag = item;
        if (border.Child is Grid grid)
        {
            if (grid.Children[0] is CheckBox cb)
            {
                cb.IsChecked = item.IsSelected;
            }

            if (grid.Children[1] is StackPanel sp && sp.Children.Count >= 2)
            {
                if (sp.Children[0] is TextBlock nameBlock) nameBlock.Text = item.Name;
                if (sp.Children[1] is TextBlock descBlock) descBlock.Text = item.Details;
            }
        }
    }
}
