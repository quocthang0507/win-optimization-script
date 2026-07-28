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
    private int _appliedRevision = -1;

    public BloatwarePage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader(T("bloatware.title"), T("bloatware.subtitle"));

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _scanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await ScanAppsAsync();
        });
        actions.Children.Add(_scanButton);

        _removeButton = ActionButton(T("bloatware.removeSelected"), Symbol.Delete, async (_, _) =>
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
        if (!MainWindow.SessionState.AppxLoaded)
        {
            await MainWindow.RefreshAppxStateAsync();
        }

        if (_appliedRevision != MainWindow.SessionState.AppxRevision)
        {
            ApplyCachedAppxState();
        }
    }

    private async Task ScanAppsAsync()
    {
        if (_resultPanel == null || _isWorking) return;

        MainWindow.SetStatusText(T("bloatware.scanning"));
        SetControlsEnabled(false);
        _resultPanel.Children.Clear();
        _selectedIds.Clear();
        
        await MainWindow.RefreshAppxStateAsync();
        ApplyCachedAppxState();
        SetControlsEnabled(true);
        MainWindow.SetStatusText(T("common.ready"));
    }

    private void ApplyCachedAppxState()
    {
        if (_resultPanel == null)
        {
            return;
        }

        var state = MainWindow.SessionState;
        _apps = state.AppxPackages.ToList();
        _appliedRevision = state.AppxRevision;
        if (!string.IsNullOrWhiteSpace(state.AppxError))
        {
            _resultPanel.Children.Clear();
            _resultPanel.Children.Add(InfoBlock(F("bloatware.scanFailed", state.AppxError)));
            return;
        }

        RenderApps();
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

        _resultPanel.Children.Add(SectionTitle(F("bloatware.detected", _apps.Count)));

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
            _removeButton.Content = F("bloatware.removeSelectedCount", _selectedIds.Count);
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
            Title = T("bloatware.confirmTitle"),
            Content = F("bloatware.confirmBody", selectedApps.Count),
            PrimaryButtonText = T("bloatware.remove"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _isWorking = true;
        SetControlsEnabled(false);
        var started = DateTimeOffset.Now;
        var succeeded = 0;
        var failed = 0;
        var reportMessages = new List<string>();
        var reportErrors = new List<string>();

        try
        {
            for (var index = 0; index < selectedApps.Count; index++)
            {
                var app = selectedApps[index];
                MainWindow.SetStatusText(F("bloatware.removing", index + 1, selectedApps.Count, app.Name));
                try
                {
                    var ok = await MainWindow.Uninstaller.RemoveAppxPackageAsync(app);
                    if (ok)
                    {
                        succeeded++;
                        reportMessages.Add($"{app.Name} ({app.Id}): removed");
                    }
                    else
                    {
                        failed++;
                        reportErrors.Add($"{app.Name} ({app.Id}): Windows rejected removal");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    reportErrors.Add($"{app.Name} ({app.Id}): {ex.Message}");
                }
            }
        }
        finally
        {
            _isWorking = false;
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                "software.appx.remove",
                "Windows App Removal",
                started,
                DateTimeOffset.Now,
                failed == 0,
                0,
                succeeded,
                failed,
                reportMessages,
                reportErrors));
            await ScanAppsAsync(); // Rescan
            MainWindow.SetStatusText(F("bloatware.summary", succeeded, failed));
        }
    }
}
