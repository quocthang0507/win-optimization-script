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
    private bool _isApplyingChanges;
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

        _searchBox = new TextBox { PlaceholderText = T("optimize.searchPlaceholder"), VerticalAlignment = VerticalAlignment.Bottom };
        _searchBox.TextChanged += (_, _) => DebounceUiAction("optimize-search", RenderTweaks);
        filters.Children.Add(_searchBox);

        _categoryBox = new ComboBox { Header = T("optimize.category"), MinWidth = 200 };
        _categoryBox.Items.Add(T("common.all"));
        foreach (var category in _tweakService.GetAllTweaks().Select(tweak => tweak.Category).Distinct().OrderBy(value => value))
        {
            _categoryBox.Items.Add(new ComboBoxItem { Content = MainWindow.Localization.TweakCategory(category), Tag = category });
        }
        _categoryBox.SelectedIndex = 0;
        _categoryBox.SelectionChanged += (_, _) => RenderTweaks();
        Grid.SetColumn(_categoryBox, 1);
        filters.Children.Add(_categoryBox);
        panel.Children.Add(filters);

        var profiles = new AdaptiveWrapPanel();
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
        var category = _categoryBox?.SelectedIndex > 0 ? (_categoryBox.SelectedItem as ComboBoxItem)?.Tag as string : null;
        var tweaks = _tweakService.GetAllTweaks()
            .Where(tweak => MainWindow.Localization.MatchesTweak(tweak, query))
            .Where(tweak => category == null || tweak.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        var groups = tweaks.GroupBy(t => t.Category).OrderBy(g => g.Key);

        if (!tweaks.Any()) _resultPanel.Children.Add(InfoBlock(T("common.noMatches")));
        foreach (var group in groups)
        {
            _resultPanel.Children.Add(SectionTitle(MainWindow.Localization.TweakCategory(group.Key)));
            
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
        details.Children.Add(new TextBlock { Text = MainWindow.Localization.TweakTitle(tweak.Id, tweak.Title), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = MainWindow.Localization.TweakDescription(tweak),
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
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, MainWindow.Localization.TweakTitle(tweak.Id, tweak.Title));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(toggle, MainWindow.Localization.TweakDescription(tweak));
        toggle.Toggled += async (s, e) => 
        {
            if (_isLoadingState || _isApplyingChanges) return;
            _isApplyingChanges = true;
            IsEnabled = false;
            toggle.IsEnabled = false;
            try
            {
                var previousState = _knownStates.GetValueOrDefault(tweak.Id);
                await MainWindow.TweakSnapshots.SaveAsync(
                    F("optimize.snapshotBeforeChange", MainWindow.Localization.TweakTitle(tweak.Id, tweak.Title)),
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
                MainWindow.SetStatusText(F("optimize.applied", MainWindow.Localization.TweakTitle(tweak.Id, tweak.Title)));
            }
            catch (Exception ex)
            {
                MainWindow.SetStatusText(F("optimize.applyFailed", MainWindow.Localization.TweakTitle(tweak.Id, tweak.Title), ex.Message));
                _isLoadingState = true;
                toggle.IsOn = !toggle.IsOn; // Revert visually
                _isLoadingState = false;
            }
            finally
            {
                toggle.IsEnabled = true;
                IsEnabled = true;
                _isApplyingChanges = false;
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

        await ReviewAndApplyProfileAsync(T(profile.NameKey), profile.Values);
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
        MainWindow.SetStatusText(F("optimize.exported", file.Path));
    }

    private async Task ImportProfileAsync()
    {
        if (_isApplyingChanges) return;
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
            if (new FileInfo(file.Path).Length > 1024 * 1024)
                throw new InvalidDataException(T("optimize.profileTooLarge"));
            var profile = TweakChangePlanner.ParseProfile(await File.ReadAllTextAsync(file.Path));
            await ReviewAndApplyProfileAsync(T("optimize.importProfile"), profile);
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText(F("optimize.importFailed", ex.Message));
        }
    }

    private async Task ReviewAndApplyProfileAsync(string label, IReadOnlyDictionary<string, bool> values)
    {
        if (_isApplyingChanges) return;
        _isApplyingChanges = true;
        IsEnabled = false;
        try
        {
            // Refresh first, so the undo snapshot never guesses a missing state from the UI cache.
            var states = await _tweakService.CheckAllTweakStatesAsync();
            var current = states.Where(state => string.IsNullOrWhiteSpace(state.Error))
                .ToDictionary(state => state.Id, state => state.IsEnabled, StringComparer.OrdinalIgnoreCase);
            var plan = TweakChangePlanner.Create(values, _tweakService.GetAllTweaks(), current);
            if (plan.Changes.Count + plan.UnchangedCount == 0)
                throw new InvalidDataException(T("optimize.profileNoKnownSettings"));

            var preview = new StackPanel { Spacing = 10 };
            preview.Children.Add(InfoBlock(F("optimize.planSummary", plan.Changes.Count, plan.UnchangedCount, plan.UnknownCount)));
            foreach (var change in plan.Changes)
                preview.Children.Add(InfoBlock($"{MainWindow.Localization.TweakTitle(change.Id, change.Title)}\n{T(change.Before ? "common.on" : "common.off")} → {T(change.After ? "common.on" : "common.off")}"));
            var dialog = new ContentDialog
            {
                Title = label,
                Content = new ScrollViewer { MaxHeight = 420, Content = preview },
                PrimaryButtonText = plan.Changes.Count > 0 ? T("optimize.applyProfile") : string.Empty,
                CloseButtonText = T("common.cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = MainWindow.Navigation_Internal.XamlRoot
            };
            if (await MainWindow.ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;

            var backup = await MainWindow.TweakSnapshots.SaveAsync(label,
                plan.Changes.ToDictionary(change => change.Id, change => change.Before, StringComparer.OrdinalIgnoreCase));
            if (backup is null) throw new IOException(T("optimize.backupRequired"));

            var started = DateTimeOffset.Now;
            var messages = new List<string> { $"Undo snapshot: {backup}", $"Unchanged: {plan.UnchangedCount}; unknown IDs ignored: {plan.UnknownCount}." };
            var errors = new List<string>();
            _isLoadingState = true;
            var index = 0;
            foreach (var change in plan.Changes)
            {
                MainWindow.SetStatusText(F("optimize.profileProgress", ++index, plan.Changes.Count));
                try
                {
                    var response = await _tweakService.ApplyTweakAsync(change.Id, change.After);
                    if (!string.IsNullOrWhiteSpace(response.Error)) throw new InvalidOperationException(response.Error);
                    _knownStates[change.Id] = response.IsEnabled;
                    MainWindow.SetCachedTweakState(response);
                    messages.Add($"{change.Id}: {change.Before} → {response.IsEnabled} (verified)");
                }
                catch (Exception ex)
                {
                    errors.Add($"{change.Id}: {change.Before} → {change.After}: {ex.Message}");
                }
            }
            _appliedRevision = MainWindow.SessionState.TweakStatesRevision;
            await MainWindow.SaveOperationReportAsync(new TaskRunResult("tweaks.profile", label, started,
                DateTimeOffset.Now, errors.Count == 0, 0, 0, 0, messages, errors));
            MainWindow.SetStatusText(F("optimize.profileComplete", plan.Changes.Count - errors.Count, errors.Count));
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText(F("optimize.importFailed", ex.Message));
        }
        finally
        {
            _isLoadingState = false;
            _isApplyingChanges = false;
            IsEnabled = true;
            RenderTweaks();
            _applyProfileButton?.Focus(FocusState.Programmatic);
        }
    }
}
