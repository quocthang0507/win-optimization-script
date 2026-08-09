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
using WinOptimizationApp.Services;
using System.Text.Json;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WinOptimizationApp.Views;

public sealed partial class OptimizePage : BasePage
{
    private StackPanel? _resultPanel;
    private readonly TweakService _tweakService;
    private readonly Dictionary<string, ToggleSwitch> _toggles = new();
    private readonly Dictionary<string, bool> _knownStates = new(StringComparer.OrdinalIgnoreCase);
    private TextBox? _searchBox;
    private ComboBox? _categoryBox;
    private ComboBox? _profileBox;
    private Button? _applyProfileButton;
    private CheckBox? _technicalDetailsBox;
    private bool _isLoadingState;
    private int _appliedRevision = -1;

    public OptimizePage(MainWindow mainWindow) : base(mainWindow)
    {
        _tweakService = MainWindow.Tweaks;
        RenderPage();
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!MainWindow.SessionState.TweakStatesLoaded)
        {
            await LoadTweakStatesAsync();
        }
        else if (_appliedRevision != MainWindow.SessionState.TweakStatesRevision)
        {
            ApplyCachedTweakStates();
        }
    }

    private void RenderPage()
    {
        AddHeader(T("optimize.title"), T("optimize.subtitle"));

        _resultPanel = new StackPanel { Spacing = 10 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        actions.Children.Add(ActionButton(T("optimize.exportProfile"), Symbol.Save, async (_, _) => await ExportProfileAsync()));
        actions.Children.Add(ActionButton(T("optimize.importProfile"), Symbol.OpenFile, async (_, _) => await ImportProfileAsync()));

        MainContent.Children.Add(actions);
        MainContent.Children.Add(BuildFiltersAndProfiles());
        MainContent.Children.Add(new TextBlock { Text = T("optimize.immediateNote"), Opacity = 0.6, Margin = new Thickness(0, 0, 0, 10) });
        MainContent.Children.Add(_resultPanel);

        RenderTweaks();
    }

    private UIElement BuildFiltersAndProfiles()
    {
        var panel = new StackPanel { Spacing = 10 };
        var filters = new Grid { ColumnSpacing = 10 };
        filters.ColumnDefinitions.Add(new ColumnDefinition());
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });

        _searchBox = new TextBox { PlaceholderText = T("optimize.searchPlaceholder") };
        _searchBox.TextChanged += (_, _) => DebounceUiAction("optimize-search", RenderTweaks);
        filters.Children.Add(_searchBox);

        _categoryBox = new ComboBox { Header = T("optimize.category"), MinWidth = 200 };
        _categoryBox.Items.Add(T("common.all"));
        foreach (var category in _tweakService.GetAllTweaks().Select(tweak => tweak.Category).Distinct().OrderBy(value => value))
        {
            _categoryBox.Items.Add(category);
        }
        _categoryBox.SelectedIndex = 0;
        _categoryBox.SelectionChanged += (_, _) => RenderTweaks();
        Grid.SetColumn(_categoryBox, 1);
        filters.Children.Add(_categoryBox);
        panel.Children.Add(filters);

        var profiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _profileBox = new ComboBox { Header = T("optimize.profile"), MinWidth = 240 };
        foreach (var profile in TweakProfileCatalog.All)
        {
            _profileBox.Items.Add(new ComboBoxItem { Content = T(profile.NameKey), Tag = profile });
        }
        _profileBox.SelectedIndex = 0;
        profiles.Children.Add(_profileBox);

        _applyProfileButton = ActionButton(T("optimize.applyProfile"), Symbol.Play, async (_, _) => await ApplySelectedProfileAsync());
        _applyProfileButton.VerticalAlignment = VerticalAlignment.Bottom;
        profiles.Children.Add(_applyProfileButton);
        panel.Children.Add(profiles);

        _technicalDetailsBox = new CheckBox { Content = T("optimize.showTechnicalDetails") };
        _technicalDetailsBox.Checked += (_, _) => RenderTweaks();
        _technicalDetailsBox.Unchecked += (_, _) => RenderTweaks();
        panel.Children.Add(_technicalDetailsBox);
        return panel;
    }

    private void RenderTweaks()
    {
        if (_resultPanel == null) return;
        _resultPanel.Children.Clear();
        _toggles.Clear();

        var query = _searchBox?.Text?.Trim() ?? string.Empty;
        var category = _categoryBox?.SelectedIndex > 0 ? _categoryBox.SelectedItem?.ToString() : null;
        var tweaks = _tweakService.GetAllTweaks()
            .Where(tweak => string.IsNullOrWhiteSpace(query)
                || tweak.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || tweak.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || tweak.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(tweak => category == null || tweak.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        var groups = tweaks.GroupBy(t => t.Category).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            _resultPanel.Children.Add(SectionTitle(group.Key));
            
            var groupPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var tweak in group)
            {
                groupPanel.Children.Add(TweakRow(tweak));
            }
            _resultPanel.Children.Add(groupPanel);
        }
    }

    private Border TweakRow(SystemTweak tweak)
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = tweak.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = tweak.Description,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        if (_technicalDetailsBox?.IsChecked == true)
        {
            details.Children.Add(BuildTechnicalDetails(tweak));
        }
        Grid.SetColumn(details, 0);
        grid.Children.Add(details);

        var toggle = new ToggleSwitch
        {
            VerticalAlignment = VerticalAlignment.Center,
            OffContent = T("common.off"),
            OnContent = T("common.on"),
            IsEnabled = _knownStates.ContainsKey(tweak.Id),
            IsOn = _knownStates.GetValueOrDefault(tweak.Id)
        };
        toggle.Toggled += async (s, e) => 
        {
            if (_isLoadingState) return;
            toggle.IsEnabled = false;
            try
            {
                var previousState = _knownStates.GetValueOrDefault(tweak.Id);
                await MainWindow.TweakSnapshots.SaveAsync(
                    F("optimize.snapshotBeforeChange", tweak.Title),
                    new Dictionary<string, bool> { [tweak.Id] = previousState });
                var response = await _tweakService.ApplyTweakAsync(tweak.Id, toggle.IsOn);
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    throw new InvalidOperationException(response.Error);
                }

                _knownStates[tweak.Id] = response.IsEnabled;
                MainWindow.SetCachedTweakState(response);
                _appliedRevision = MainWindow.SessionState.TweakStatesRevision;
                toggle.IsOn = response.IsEnabled;
                MainWindow.SetStatusText(F("optimize.applied", tweak.Title));
            }
            catch (Exception ex)
            {
                MainWindow.SetStatusText($"Failed to apply {tweak.Title}: {ex.Message}");
                _isLoadingState = true;
                toggle.IsOn = !toggle.IsOn; // Revert visually
                _isLoadingState = false;
            }
            finally
            {
                toggle.IsEnabled = true;
            }
        };

        _toggles[tweak.Id] = toggle;
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        border.Child = grid;
        return border;
    }

    private UIElement BuildTechnicalDetails(SystemTweak tweak)
    {
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = F(
                "optimize.technicalSummary",
                tweak.Id,
                MainWindow.Localization.RiskName(tweak.RiskLevel),
                tweak.SupportedWindows,
                tweak.RequiresAdministrator ? T("common.yes") : T("common.no"),
                tweak.RestartRequirement),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        });
        content.Children.Add(ScriptDetails(T("optimize.checkScript"), tweak.CheckScript));
        content.Children.Add(ScriptDetails(T("optimize.applyScript"), tweak.EnableScript));
        content.Children.Add(ScriptDetails(T("optimize.revertScript"), tweak.DisableScript));

        return new Expander
        {
            Header = T("optimize.technicalDetails"),
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }

    private static UIElement ScriptDetails(string title, string script)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBox
        {
            Text = script.Trim(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 150,
            FontFamily = new FontFamily("Cascadia Mono")
        });
        return panel;
    }

    private async Task LoadTweakStatesAsync()
    {
        _isLoadingState = true;
        MainWindow.SetStatusText(T("optimize.loadingStates"));
        try
        {
            await MainWindow.RefreshTweakStatesAsync();
            ApplyCachedTweakStates();
            MainWindow.SetStatusText(T("optimize.statesLoaded"));
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText(F("optimize.statesLoadFailed", ex.Message));
        }
        finally
        {
            _isLoadingState = false;
        }
    }

    private void ApplyCachedTweakStates()
    {
        _isLoadingState = true;
        _knownStates.Clear();
        foreach (var state in MainWindow.SessionState.TweakStates.Values)
        {
            if (!string.IsNullOrWhiteSpace(state.Error))
            {
                continue;
            }

            _knownStates[state.Id] = state.IsEnabled;
            if (_toggles.TryGetValue(state.Id, out var toggle))
            {
                toggle.IsOn = state.IsEnabled;
                toggle.IsEnabled = true;
            }
        }
        _appliedRevision = MainWindow.SessionState.TweakStatesRevision;
        _isLoadingState = false;
    }

    private async Task ApplySelectedProfileAsync()
    {
        if (_profileBox?.SelectedItem is not ComboBoxItem { Tag: TweakProfile profile })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = T(profile.NameKey),
            Content = new TextBlock { Text = F("optimize.confirmProfile", profile.Values.Count, T(profile.DescriptionKey)), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = T("optimize.applyProfile"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _isLoadingState = true;
        if (_applyProfileButton != null) _applyProfileButton.IsEnabled = false;
        var failed = 0;
        try
        {
            var previousValues = profile.Values.Keys
                .Where(_knownStates.ContainsKey)
                .Where(id => _knownStates[id] != profile.Values[id])
                .ToDictionary(id => id, id => _knownStates[id], StringComparer.OrdinalIgnoreCase);
            await MainWindow.TweakSnapshots.SaveAsync(T(profile.NameKey), previousValues);

            var index = 0;
            foreach (var change in profile.Values)
            {
                index++;
                MainWindow.SetStatusText(F("optimize.profileProgress", index, profile.Values.Count));
                var response = await _tweakService.ApplyTweakAsync(change.Key, change.Value);
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    failed++;
                    continue;
                }

                _knownStates[change.Key] = response.IsEnabled;
                MainWindow.SetCachedTweakState(response);
                _appliedRevision = MainWindow.SessionState.TweakStatesRevision;
            }
        }
        finally
        {
            _isLoadingState = false;
            if (_applyProfileButton != null) _applyProfileButton.IsEnabled = true;
            RenderTweaks();
        }

        MainWindow.SetStatusText(F("optimize.profileComplete", profile.Values.Count - failed, failed));
    }

    private async Task ExportProfileAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "my-tweaks-profile.json"
        };
        picker.FileTypeChoices.Add("JSON Profile", new List<string> { ".json" });
        InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        var profile = new Dictionary<string, bool>();
        foreach (var kvp in _knownStates)
        {
            profile[kvp.Key] = kvp.Value;
        }

        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(file.Path, json);
        MainWindow.SetStatusText($"Profile exported to {file.Path}");
    }

    private async Task ImportProfileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var json = await File.ReadAllTextAsync(file.Path);
            var profile = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (profile == null) return;
            var knownIds = _tweakService.GetAllTweaks().Select(tweak => tweak.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var applicable = profile.Where(entry => knownIds.Contains(entry.Key)).ToList();
            if (applicable.Count == 0)
            {
                throw new InvalidDataException(T("optimize.profileNoKnownSettings"));
            }

            var dialog = new ContentDialog
            {
                Title = "Import Profile",
                Content = F("optimize.confirmImport", applicable.Count, profile.Count - applicable.Count),
                PrimaryButtonText = "Apply",
                CloseButtonText = T("common.cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = MainWindow.Navigation_Internal.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            MainWindow.SetStatusText("Applying profile tweaks...");
            _isLoadingState = true; // Prevent individual toggles from triggering events

            var previousValues = applicable
                .Where(entry => _knownStates.ContainsKey(entry.Key) && _knownStates[entry.Key] != entry.Value)
                .ToDictionary(entry => entry.Key, entry => _knownStates[entry.Key], StringComparer.OrdinalIgnoreCase);
            await MainWindow.TweakSnapshots.SaveAsync(T("optimize.importProfile"), previousValues);

            var failed = 0;
            foreach (var kvp in applicable)
            {
                if (_knownStates.GetValueOrDefault(kvp.Key) != kvp.Value)
                {
                    var response = await _tweakService.ApplyTweakAsync(kvp.Key, kvp.Value);
                    if (!string.IsNullOrWhiteSpace(response.Error))
                    {
                        failed++;
                        continue;
                    }

                    _knownStates[kvp.Key] = response.IsEnabled;
                    MainWindow.SetCachedTweakState(response);
                    _appliedRevision = MainWindow.SessionState.TweakStatesRevision;
                }
            }

            RenderTweaks();
            MainWindow.SetStatusText(F("optimize.profileComplete", applicable.Count - failed, failed));
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText(F("optimize.importFailed", ex.Message));
        }
        finally
        {
            _isLoadingState = false;
        }
    }
}
