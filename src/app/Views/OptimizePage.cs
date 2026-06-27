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
    private bool _isLoadingState;

    public OptimizePage(MainWindow mainWindow) : base(mainWindow)
    {
        _tweakService = new TweakService(new CommandRunner());
        _tweakService.Client = MainWindow.IpcClient;
        RenderPage();
        _ = LoadTweakStatesAsync();
    }

    private void RenderPage()
    {
        AddHeader("System Tweaks", "Optimize Windows by toggling telemetry, gaming features, and UI settings.");

        _resultPanel = new StackPanel { Spacing = 10 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        actions.Children.Add(ActionButton("Export Profile", Symbol.Save, async (_, _) => await ExportProfileAsync()));
        actions.Children.Add(ActionButton("Import Profile", Symbol.OpenFile, async (_, _) => await ImportProfileAsync()));

        MainContent.Children.Add(actions);
        MainContent.Children.Add(new TextBlock { Text = "Note: Toggle changes are applied immediately.", Opacity = 0.6, Margin = new Thickness(0, 0, 0, 10) });
        MainContent.Children.Add(_resultPanel);

        RenderTweaks();
    }

    private void RenderTweaks()
    {
        if (_resultPanel == null) return;
        _resultPanel.Children.Clear();
        _toggles.Clear();

        var tweaks = _tweakService.GetAllTweaks();
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
        Grid.SetColumn(details, 0);
        grid.Children.Add(details);

        var toggle = new ToggleSwitch
        {
            VerticalAlignment = VerticalAlignment.Center,
            OffContent = "Off",
            OnContent = "On",
            IsEnabled = false // Disabled until state loads
        };
        toggle.Toggled += async (s, e) => 
        {
            if (_isLoadingState) return;
            toggle.IsEnabled = false;
            try
            {
                await _tweakService.ApplyTweakAsync(tweak.Id, toggle.IsOn);
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

    private async Task LoadTweakStatesAsync()
    {
        _isLoadingState = true;
        MainWindow.SetStatusText("Loading tweak states...");
        try
        {
            var tweaks = _tweakService.GetAllTweaks();
            foreach (var tweak in tweaks)
            {
                var state = await _tweakService.CheckTweakStateAsync(tweak.Id);
                if (_toggles.TryGetValue(tweak.Id, out var toggle))
                {
                    toggle.IsOn = state.IsEnabled;
                    toggle.IsEnabled = true;
                }
            }
            MainWindow.SetStatusText("Tweak states loaded.");
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText($"Failed to load tweak states: {ex.Message}");
        }
        finally
        {
            _isLoadingState = false;
        }
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
        foreach (var kvp in _toggles)
        {
            profile[kvp.Key] = kvp.Value.IsOn;
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

            var dialog = new ContentDialog
            {
                Title = "Import Profile",
                Content = $"Are you sure you want to apply {profile.Count} tweaks from this profile?",
                PrimaryButtonText = "Apply",
                CloseButtonText = T("common.cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = MainWindow.Navigation_Internal.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            MainWindow.SetStatusText("Applying profile tweaks...");
            _isLoadingState = true; // Prevent individual toggles from triggering events

            foreach (var kvp in profile)
            {
                if (_toggles.TryGetValue(kvp.Key, out var toggle))
                {
                    if (toggle.IsOn != kvp.Value)
                    {
                        await _tweakService.ApplyTweakAsync(kvp.Key, kvp.Value);
                        toggle.IsOn = kvp.Value;
                    }
                }
            }
            
            MainWindow.SetStatusText("Profile imported successfully.");
        }
        catch (Exception ex)
        {
            MainWindow.SetStatusText($"Failed to import profile: {ex.Message}");
        }
        finally
        {
            _isLoadingState = false;
        }
    }
}
