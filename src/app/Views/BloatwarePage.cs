using WinOptimizationApp.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using System;

namespace WinOptimizationApp.Views;

public sealed partial class BloatwarePage : BasePage
{
    private StackPanel? _resultPanel;
    private Button? _scanButton;
    private Button? _removeButton;
    private List<InstalledApp> _apps = [];
    private bool _isWorking;
    private readonly HashSet<string> _selectedIds = new();

    public BloatwarePage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader("Windows App Remover", "Uninstall pre-installed Windows UWP apps (Bloatware) safely.");

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _scanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await ScanAppsAsync();
        });
        actions.Children.Add(_scanButton);

        _removeButton = ActionButton("Remove Selected", Symbol.Delete, async (_, _) =>
        {
            await RemoveSelectedAppsAsync();
        });
        _removeButton.IsEnabled = false;
        actions.Children.Add(_removeButton);

        MainContent.Children.Add(actions);
        MainContent.Children.Add(_resultPanel);
    }

    public override async Task OnNavigatedToAsync()
    {
        if (_apps.Count == 0)
        {
            await ScanAppsAsync();
        }
    }

    private async Task ScanAppsAsync()
    {
        if (_resultPanel == null || _isWorking) return;

        MainWindow.SetStatusText("Scanning Windows Apps...");
        SetControlsEnabled(false);
        _resultPanel.Children.Clear();
        _selectedIds.Clear();
        
        try
        {
            _apps = (await MainWindow.Uninstaller.ScanAppxPackagesAsync()).ToList();
            RenderApps();
        }
        finally
        {
            SetControlsEnabled(true);
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private void RenderApps()
    {
        if (_resultPanel == null) return;
        _resultPanel.Children.Clear();

        if (_apps.Count == 0)
        {
            _resultPanel.Children.Add(InfoBlock(T("common.noMatches")));
            return;
        }

        _resultPanel.Children.Add(SectionTitle($"Detected {_apps.Count} removable Windows Apps"));

        foreach (var app in _apps)
        {
            _resultPanel.Children.Add(AppRow(app));
        }
    }

    private Border AppRow(InstalledApp app)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var checkbox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = false
        };
        checkbox.Checked += (_, _) => { _selectedIds.Add(app.Id); UpdateRemoveButton(); };
        checkbox.Unchecked += (_, _) => { _selectedIds.Remove(app.Id); UpdateRemoveButton(); };
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{app.Publisher} / {app.Version}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        border.Child = grid;
        return border;
    }

    private void UpdateRemoveButton()
    {
        if (_removeButton != null)
        {
            _removeButton.IsEnabled = _selectedIds.Count > 0 && !_isWorking;
            _removeButton.Content = $"Remove Selected ({_selectedIds.Count})";
        }
    }

    private void SetControlsEnabled(bool isEnabled)
    {
        if (_scanButton != null) _scanButton.IsEnabled = isEnabled;
        if (_removeButton != null) _removeButton.IsEnabled = isEnabled && _selectedIds.Count > 0;
    }

    private async Task RemoveSelectedAppsAsync()
    {
        if (_resultPanel == null || _isWorking || _selectedIds.Count == 0) return;

        var selectedApps = _apps.Where(a => _selectedIds.Contains(a.Id)).ToList();
        var dialog = new ContentDialog
        {
            Title = $"Confirm Removal",
            Content = $"Are you sure you want to completely uninstall {selectedApps.Count} Windows apps?",
            PrimaryButtonText = "Remove",
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _isWorking = true;
        SetControlsEnabled(false);
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < selectedApps.Count; index++)
            {
                var app = selectedApps[index];
                MainWindow.SetStatusText($"Removing {index + 1}/{selectedApps.Count}: {app.Name}");
                var ok = await MainWindow.Uninstaller.RemoveAppxPackageAsync(app);
                if (ok) succeeded++; else failed++;
            }
        }
        finally
        {
            _isWorking = false;
            await ScanAppsAsync(); // Rescan
            MainWindow.SetStatusText($"Removed: {succeeded}, Failed: {failed}");
        }
    }
}
