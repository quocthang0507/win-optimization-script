using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.UI;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public abstract partial class BasePage : UserControl
{
    protected MainWindow MainWindow { get; }
    protected StackPanel MainContent { get; }

    protected BasePage(MainWindow mainWindow)
    {
        MainWindow = mainWindow;
        MainContent = new StackPanel { Spacing = 16, Padding = new Thickness(28, 22, 28, 28) };
        Content = MainContent;
    }

    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }

    protected string T(string key) => MainWindow.T_Internal(key);
    protected string F(string key, params object[] args) => MainWindow.F_Internal(key, args);

    protected void AddHeader(string title, string subtitle)
    {
        MainContent.Children.Add(new TextBlock { Text = title, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        MainContent.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap, Opacity = 0.72, Margin = new Thickness(0, -8, 0, 4) });
    }

    protected static TextBlock SectionTitle(string text)
    {
        return new TextBlock { Text = text, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
    }

    protected static TextBlock InfoBlock(string text)
    {
        return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
    }

    protected static void AddMetric(Grid grid, int row, int column, string title, string value, string detail, Color color)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(22, color.R, color.G, color.B))
        };

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = title, Opacity = 0.7 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = detail, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap });
        card.Child = stack;

        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    protected static Border Card(string title, string body, string buttonText, RoutedEventHandler action)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(18, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var button = new Button { Content = buttonText, MinWidth = 86 };
        ToolTipService.SetToolTip(button, buttonText);
        button.Click += action;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        border.Child = grid;
        return border;
    }

    protected static Border RiskBadge(RiskLevel risk, string? label = null)
    {
        var color = risk switch
        {
            RiskLevel.Safe => Colors.SeaGreen,
            RiskLevel.Medium => Colors.DarkOrange,
            RiskLevel.High => Colors.IndianRed,
            _ => Colors.Gray
        };

        return new Border
        {
            Width = 96,
            Padding = new Thickness(0, 5, 0, 5),
            CornerRadius = new CornerRadius(6),
            Background = Brush(Color.FromArgb(38, color.R, color.G, color.B)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock 
            { 
                Text = label ?? risk.ToString(), 
                Foreground = Brush(color), 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    protected static Button ActionButton(string text, Symbol symbol, RoutedEventHandler action)
    {
        var button = new Button
        {
            MinWidth = 126,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new SymbolIcon(symbol),
                    new TextBlock { Text = text }
                }
            }
        };
        ToolTipService.SetToolTip(button, text);
        button.Click += action;
        return button;
    }

    protected static Button IconButton(Symbol symbol, string label, RoutedEventHandler action)
    {
        var button = new Button
        {
            Width = 42,
            Height = 36,
            Padding = new Thickness(0),
            Content = new SymbolIcon(symbol)
        };
        ToolTipService.SetToolTip(button, label);
        button.Click += action;
        return button;
    }

    protected static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    protected static SolidColorBrush RiskBrush(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.Safe => Brush(Colors.SeaGreen),
            RiskLevel.Medium => Brush(Colors.DarkOrange),
            RiskLevel.High => Brush(Colors.IndianRed),
            _ => Brush(Colors.Gray)
        };
    }
}
