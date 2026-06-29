using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class TweaksPage : BasePage
{
    private readonly TweakService _tweakService;
    private StackPanel? _contentPanel;
    private ProgressBar? _progressBar;

    public TweaksPage(MainWindow mainWindow) : base(mainWindow)
    {
        _tweakService = new TweakService(mainWindow.Commands);
        RenderPage();
    }

    private void RenderPage()
    {
        AddHeader(T("tweaks.title"), T("tweaks.subtitle"));

        _progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 10)
        };
        MainContent.Children.Add(_progressBar);

        _contentPanel = new StackPanel { Spacing = 20 };
        MainContent.Children.Add(_contentPanel);

        _ = LoadTweaksAsync();
    }

    private async Task LoadTweaksAsync()
    {
        if (_progressBar is not null) _progressBar.Visibility = Visibility.Visible;
        if (_contentPanel is not null) _contentPanel.Children.Clear();

        var tweaks = _tweakService.GetAllTweaks();
        var categories = tweaks.Select(t => t.Category).Distinct().ToList();

        foreach (var category in categories)
        {
            var categoryTweaks = tweaks.Where(t => t.Category == category).ToList();
            if (categoryTweaks.Count == 0) continue;

            var categoryGroup = new Expander
            {
                Header = CreateCategoryHeader(category),
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var tweakList = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };

            foreach (var tweak in categoryTweaks)
            {
                var card = await CreateTweakCardAsync(tweak);
                tweakList.Children.Add(card);
            }

            categoryGroup.Content = tweakList;
            _contentPanel?.Children.Add(categoryGroup);
        }

        if (_progressBar is not null) _progressBar.Visibility = Visibility.Collapsed;
    }

    private UIElement CreateCategoryHeader(string category)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        
        Symbol icon = category switch
        {
            "Privacy" => Symbol.Important,
            "System" => Symbol.Setting,
            "Gaming" => Symbol.Target,
            _ => Symbol.Setting
        };

        panel.Children.Add(new SymbolIcon { Symbol = icon, VerticalAlignment = VerticalAlignment.Center });
        
        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock
        {
            Text = category,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(textPanel);
        return panel;
    }

    private async Task<UIElement> CreateTweakCardAsync(SystemTweak tweak)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16)
        };

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoPanel.Children.Add(new TextBlock
        {
            Text = tweak.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = tweak.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        });
        
        var statusLabel = new TextBlock
        {
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed
        };
        infoPanel.Children.Add(statusLabel);
        
        grid.Children.Add(infoPanel);
        Grid.SetColumn(infoPanel, 0);

        var statusIcon = new SymbolIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        grid.Children.Add(statusIcon);
        Grid.SetColumn(statusIcon, 1);

        var actionButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 100
        };
        grid.Children.Add(actionButton);
        Grid.SetColumn(actionButton, 2);

        card.Child = grid;

        // Load status
        var status = await _tweakService.CheckTweakStateAsync(tweak.Id);
        UpdateTweakUI(status, statusLabel, statusIcon, actionButton);

        actionButton.Click += async (_, _) =>
        {
            actionButton.IsEnabled = false;
            if (_progressBar is not null) _progressBar.Visibility = Visibility.Visible;

            var applyEnable = status == null || !status.IsEnabled;
            status = await _tweakService.ApplyTweakAsync(tweak.Id, applyEnable);

            UpdateTweakUI(status, statusLabel, statusIcon, actionButton);
            if (_progressBar is not null) _progressBar.Visibility = Visibility.Collapsed;
            actionButton.IsEnabled = true;
        };

        return card;
    }

    private void UpdateTweakUI(TweakStateResponse status, TextBlock statusLabel, SymbolIcon statusIcon, Button actionButton)
    {
        if (status != null && !string.IsNullOrEmpty(status.Error))
        {
            statusLabel.Text = status.Error;
            statusLabel.Foreground = new SolidColorBrush(Colors.Red);
            statusLabel.Visibility = Visibility.Visible;
            statusIcon.Symbol = Symbol.Cancel;
            statusIcon.Foreground = new SolidColorBrush(Colors.Red);
            actionButton.IsEnabled = false;
            return;
        }

        statusLabel.Visibility = Visibility.Collapsed;

        if (status != null && status.IsEnabled)
        {
            statusIcon.Symbol = Symbol.Accept;
            statusIcon.Foreground = new SolidColorBrush(Colors.Green);
            actionButton.Content = T("tweaks.btn.revert");
        }
        else
        {
            statusIcon.Symbol = Symbol.Help;
            statusIcon.Foreground = new SolidColorBrush(Colors.Gray);
            actionButton.Content = T("tweaks.btn.apply");
        }
    }
}
