using WinOptimizationApp.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using System;
using System.Diagnostics;

namespace WinOptimizationApp.Views;

public sealed partial class SoftwareInstallerPage : BasePage
{
    private StackPanel? _resultPanel;
    private TextBox? _searchBox;
    private ComboBox? _groupFilterBox;
    private Button? _installSelectedButton;
    private Button? _selectVisibleButton;
    private Button? _clearSelectionButton;
    private TextBlock? _operationStatus;
    private readonly HashSet<string> _selectedIds = new();
    private bool _isInstalling;

    private readonly List<(string Name, string Id, string Description, string Group)> _curatedApps = [
        ("Google Chrome", "Google.Chrome", "Web Browser", "Browsers"),
        ("Mozilla Firefox", "Mozilla.Firefox", "Web Browser", "Browsers"),
        ("Brave", "Brave.Brave", "Web Browser", "Browsers"),
        ("7-Zip", "7zip.7zip", "Archive Tool", "Utilities"),
        ("WinRAR", "RARLab.WinRAR", "Archive Tool", "Utilities"),
        ("VLC Media Player", "VideoLAN.VLC", "Media Player", "Media"),
        ("Spotify", "Spotify.Spotify", "Music Streaming", "Media"),
        ("Visual Studio Code", "Microsoft.VisualStudioCode", "Code Editor", "Development"),
        ("Git", "Git.Git", "Version Control", "Development"),
        ("Notepad++", "Notepad++.Notepad++", "Text Editor", "Utilities"),
        ("Discord", "Discord.Discord", "Communication", "Social"),
        ("Zoom", "Zoom.Zoom", "Video Conferencing", "Social"),
        ("OBS Studio", "OBSProject.OBSStudio", "Screen Recording", "Media"),
        ("PowerToys", "Microsoft.PowerToys", "System Utilities", "Utilities"),
        ("Everything", "voidtools.Everything", "Fast Search", "Utilities")
    ];

    public SoftwareInstallerPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader(T("software.title"), T("software.subtitle"));

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new AdaptiveWrapPanel();

        _installSelectedButton = ActionButton(T("software.installSelected"), Symbol.Download, async (_, _) =>
        {
            await InstallSelectedAppsAsync();
        });
        _installSelectedButton.IsEnabled = false;
        actions.Children.Add(_installSelectedButton);

        _selectVisibleButton = ActionButton(T("software.selectVisible"), Symbol.Accept, (_, _) => SelectVisibleApps());
        actions.Children.Add(_selectVisibleButton);

        _clearSelectionButton = ActionButton(T("software.clearSelection"), Symbol.Clear, (_, _) => ClearSelection());
        _clearSelectionButton.IsEnabled = false;
        actions.Children.Add(_clearSelectionButton);

        MainContent.Children.Add(actions);
        _operationStatus = InfoBlock(string.Empty);
        _operationStatus.Visibility = Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(_operationStatus, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        MainContent.Children.Add(_operationStatus);
        MainContent.Children.Add(FilterPanel());
        MainContent.Children.Add(_resultPanel);

        RenderApps();
    }

    private StackPanel FilterPanel()
    {
        var panel = new StackPanel { Spacing = 10 };

        var searchRow = new Grid { ColumnSpacing = 10 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition());

        _searchBox = new TextBox
        {
            PlaceholderText = T("software.searchPlaceholder"),
            Height = 36
        };
        ConfigureSearch(_searchBox, "software-search", RenderApps);
        searchRow.Children.Add(_searchBox);
        panel.Children.Add(searchRow);

        var filterRow = new AdaptiveWrapPanel { Spacing = 10 };

        _groupFilterBox = new ComboBox
        {
            Header = T("software.category"),
            MinWidth = 160
        };
        _groupFilterBox.Items.Add(T("common.all"));
        foreach (var g in _curatedApps.Select(a => a.Group).Distinct().OrderBy(g => g))
        {
            _groupFilterBox.Items.Add(new ComboBoxItem { Content = T($"software.group.{g}"), Tag = g });
        }
        _groupFilterBox.SelectedIndex = 0;
        _groupFilterBox.SelectionChanged += (_, _) => RenderApps();
        filterRow.Children.Add(_groupFilterBox);

        panel.Children.Add(filterRow);
        return panel;
    }

    private void RenderApps()
    {
        if (_resultPanel == null) return;
        _resultPanel.Children.Clear();

        var query = _searchBox?.Text?.Trim() ?? string.Empty;
        var filtered = GetFilteredApps(query);
        UpdateInstallButton();

        _resultPanel.Children.Add(SectionTitle(F("software.results", filtered.Count, _curatedApps.Count)));

        if (filtered.Count == 0)
        {
            _resultPanel.Children.Add(InfoBlock(T("common.noMatches")));
            return;
        }

        string currentGroup = "";
        foreach (var app in filtered)
        {
            if (app.Group != currentGroup)
            {
                currentGroup = app.Group;
                _resultPanel.Children.Add(SectionTitle(T($"software.group.{currentGroup}")));
            }
            _resultPanel.Children.Add(AppRow(app));
        }
    }

    private Border AppRow((string Name, string Id, string Description, string Group) app)
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkbox = new CheckBox
        {
            MinWidth = 0,
            Padding = new Thickness(0),
            IsEnabled = !_isInstalling,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = _selectedIds.Contains(app.Id)
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(checkbox, F("software.selectApp", app.Name));
        checkbox.Checked += (_, _) => { _selectedIds.Add(app.Id); UpdateInstallButton(); };
        checkbox.Unchecked += (_, _) => { _selectedIds.Remove(app.Id); UpdateInstallButton(); };
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{T($"software.description.{app.Id}")} • {app.Id}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var openButton = IconButton(Symbol.OpenFile, F("common.actionFor", T("common.open"), app.Name), (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://winget.run/pkg/{Uri.EscapeDataString(app.Id)}",
                    UseShellExecute = true
                });
            }
            catch { }
        });
        Grid.SetColumn(openButton, 2);
        grid.Children.Add(openButton);

        border.Child = grid;
        return border;
    }

    private void UpdateInstallButton()
    {
        if (_installSelectedButton != null)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_installSelectedButton, F("software.installSelectedCount", _selectedIds.Count));
            _installSelectedButton.IsEnabled = _selectedIds.Count > 0 && !_isInstalling;
            _installSelectedButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new SymbolIcon(Symbol.Download),
                    new TextBlock { Text = F("software.installSelectedCount", _selectedIds.Count) }
                }
            };
        }

        if (_clearSelectionButton != null) _clearSelectionButton.IsEnabled = _selectedIds.Count > 0 && !_isInstalling;
        if (_selectVisibleButton != null) _selectVisibleButton.IsEnabled = !_isInstalling && GetFilteredApps().Any();
    }

    private List<(string Name, string Id, string Description, string Group)> GetFilteredApps(string? query = null)
    {
        query ??= _searchBox?.Text?.Trim() ?? string.Empty;
        var selectedGroup = (_groupFilterBox?.SelectedItem as ComboBoxItem)?.Tag as string;
        return _curatedApps
            .Where(app => string.IsNullOrEmpty(query)
                || app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || T($"software.description.{app.Id}").Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || T($"software.group.{app.Group}").Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Where(app => selectedGroup == null || app.Group == selectedGroup)
            .OrderBy(app => app.Group)
            .ThenBy(app => app.Name)
            .ToList();
    }

    private void SelectVisibleApps()
    {
        if (_isInstalling) return;
        foreach (var app in GetFilteredApps()) _selectedIds.Add(app.Id);
        RenderApps();
    }

    private void ClearSelection()
    {
        if (_isInstalling) return;
        _selectedIds.Clear();
        RenderApps();
    }

    private void SetControlsEnabled(bool isEnabled)
    {
        if (_installSelectedButton != null) _installSelectedButton.IsEnabled = isEnabled && _selectedIds.Count > 0;
        if (_searchBox != null) _searchBox.IsEnabled = isEnabled;
        if (_groupFilterBox != null) _groupFilterBox.IsEnabled = isEnabled;
        if (_selectVisibleButton != null) _selectVisibleButton.IsEnabled = isEnabled && GetFilteredApps().Any();
        if (_clearSelectionButton != null) _clearSelectionButton.IsEnabled = isEnabled && _selectedIds.Count > 0;
    }

    private async Task InstallSelectedAppsAsync()
    {
        if (_resultPanel == null || _isInstalling || _selectedIds.Count == 0) return;

        var selectedApps = _curatedApps.Where(a => _selectedIds.Contains(a.Id)).ToList();
        var confirmation = new StackPanel { Spacing = 10 };
        confirmation.Children.Add(InfoBlock(F("software.confirmBody", selectedApps.Count)));
        foreach (var app in selectedApps)
            confirmation.Children.Add(new TextBlock { Text = $"{app.Name} ({app.Id})", TextWrapping = TextWrapping.Wrap });
        var dialog = new ContentDialog
        {
            Title = T("software.confirmTitle"),
            Content = new ScrollViewer { Content = confirmation, MaxHeight = 360 },
            PrimaryButtonText = T("software.install"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await MainWindow.ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;

        _isInstalling = true;
        CancelDebouncedUiAction("software-search");
        RenderApps();
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
                SetOperationStatus(F("software.installProgress", index + 1, selectedApps.Count, app.Name));
                WingetPackageUpgradeResult result;
                try
                {
                    result = await MainWindow.Winget.InstallPackageAsync(app.Id);
                }
                catch (Exception ex)
                {
                    result = new WingetPackageUpgradeResult(
                        new WingetPackage(app.Name, app.Id, string.Empty, string.Empty, "winget"),
                        false,
                        -1,
                        string.Empty,
                        ex.Message);
                }
                if (result.Success)
                {
                    succeeded++;
                    _selectedIds.Remove(app.Id);
                    reportMessages.Add($"{app.Name} ({app.Id}): installed");
                }
                else
                {
                    failed++;
                    reportErrors.Add($"{app.Name} ({app.Id}): {result.StandardError}");
                }
            }
        }
        finally
        {
            _isInstalling = false;
            RenderApps();
            SetControlsEnabled(true);
            SetOperationStatus(F("software.installSummary", succeeded, failed));
            await MainWindow.SaveOperationReportAsync(new TaskRunResult(
                "software.install",
                "Software Installation",
                started,
                DateTimeOffset.Now,
                failed == 0,
                0,
                succeeded,
                failed,
                reportMessages,
                reportErrors));
        }
    }

    private void SetOperationStatus(string text)
    {
        if (_operationStatus != null)
        {
            _operationStatus.Text = text;
            _operationStatus.Visibility = Visibility.Visible;
        }
        MainWindow.SetStatusText(text);
    }
}
