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
        AddHeader("Software Installer", "Install essential Windows applications quickly using WinGet.");

        _resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _installSelectedButton = ActionButton("Install Selected", Symbol.Download, async (_, _) =>
        {
            await InstallSelectedAppsAsync();
        });
        _installSelectedButton.IsEnabled = false;
        actions.Children.Add(_installSelectedButton);

        MainContent.Children.Add(actions);
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
            PlaceholderText = "Search by name or ID...",
            Height = 36
        };
        _searchBox.TextChanged += (_, _) => RenderApps();
        searchRow.Children.Add(_searchBox);
        panel.Children.Add(searchRow);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _groupFilterBox = new ComboBox
        {
            Header = "Category",
            MinWidth = 160
        };
        _groupFilterBox.Items.Add("All");
        foreach (var g in _curatedApps.Select(a => a.Group).Distinct().OrderBy(g => g))
        {
            _groupFilterBox.Items.Add(g);
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
        var selectedGroup = _groupFilterBox?.SelectedItem?.ToString() ?? "All";

        var filtered = _curatedApps.Where(a => 
            (string.IsNullOrEmpty(query) || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || a.Id.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (selectedGroup == "All" || a.Group == selectedGroup)
        ).OrderBy(a => a.Group).ThenBy(a => a.Name).ToList();

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
                _resultPanel.Children.Add(SectionTitle(currentGroup));
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
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = _selectedIds.Contains(app.Id)
        };
        checkbox.Checked += (_, _) => { _selectedIds.Add(app.Id); UpdateInstallButton(); };
        checkbox.Unchecked += (_, _) => { _selectedIds.Remove(app.Id); UpdateInstallButton(); };
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{app.Description} • {app.Id}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var openButton = IconButton(Symbol.OpenFile, T("common.open"), (_, _) =>
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
            _installSelectedButton.IsEnabled = _selectedIds.Count > 0 && !_isInstalling;
            _installSelectedButton.Content = $"Install Selected ({_selectedIds.Count})";
        }
    }

    private void SetControlsEnabled(bool isEnabled)
    {
        if (_installSelectedButton != null) _installSelectedButton.IsEnabled = isEnabled && _selectedIds.Count > 0;
        if (_searchBox != null) _searchBox.IsEnabled = isEnabled;
        if (_groupFilterBox != null) _groupFilterBox.IsEnabled = isEnabled;
    }

    private async Task InstallSelectedAppsAsync()
    {
        if (_resultPanel == null || _isInstalling || _selectedIds.Count == 0) return;

        var selectedApps = _curatedApps.Where(a => _selectedIds.Contains(a.Id)).ToList();
        var dialog = new ContentDialog
        {
            Title = $"Confirm Installation",
            Content = $"Are you sure you want to install {selectedApps.Count} applications via WinGet? This process will happen silently in the background.",
            PrimaryButtonText = "Install",
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _isInstalling = true;
        SetControlsEnabled(false);
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < selectedApps.Count; index++)
            {
                var app = selectedApps[index];
                MainWindow.SetStatusText($"Installing {index + 1}/{selectedApps.Count}: {app.Name}");
                var result = await MainWindow.Winget.InstallPackageAsync(app.Id);
                if (result.Success) succeeded++; else failed++;
            }
        }
        finally
        {
            _isInstalling = false;
            _selectedIds.Clear();
            RenderApps();
            UpdateInstallButton();
            SetControlsEnabled(true);
            MainWindow.SetStatusText($"Installation finished. Succeeded: {succeeded}, Failed: {failed}");
        }
    }
}
