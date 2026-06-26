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

    protected string T(string key)
    {
        return MainWindow.Translate(key);
    }

    protected string F(string key, params object[] args)
    {
        return MainWindow.FormatTranslation(key, args);
    }

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
            BorderBrush = ThemeBorderBrush(),
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
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
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

    /// <summary>
    /// Returns a border brush that adapts to the current theme.
    /// Light theme: semi-transparent dark border; Dark theme: semi-transparent light border.
    /// </summary>
    protected static SolidColorBrush ThemeBorderBrush()
    {
        var theme = ResolveEffectiveTheme();
        return theme == ElementTheme.Light
            ? Brush(Color.FromArgb(255, 214, 222, 232))
            : Brush(Color.FromArgb(255, 58, 66, 79));
    }

    /// <summary>
    /// Returns a stroke brush suitable for chart separators that adapts to the current theme.
    /// </summary>
    protected static SolidColorBrush ThemeChartStrokeBrush()
    {
        var theme = ResolveEffectiveTheme();
        return theme == ElementTheme.Light
            ? Brush(Color.FromArgb(255, 226, 232, 240))
            : Brush(Color.FromArgb(255, 64, 72, 86));
    }

    /// <summary>
    /// Returns a card background brush that adapts to the current theme.
    /// Light theme: opaque white surface; Dark theme: subtle white overlay.
    /// </summary>
    protected static SolidColorBrush ThemeCardBackground()
    {
        var theme = ResolveEffectiveTheme();
        return theme == ElementTheme.Light
            ? Brush(Color.FromArgb(255, 252, 253, 255))
            : Brush(Color.FromArgb(255, 31, 36, 44));
    }

    private static ElementTheme ResolveEffectiveTheme()
    {
        var theme = MainWindow.CurrentElementTheme;
        if (theme == ElementTheme.Default)
        {
            theme = Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }
        return theme;
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

    protected Border CreateAdminWarningBanner(string? title = null, string? description = null)
    {
        var adminWarningBanner = new Border
        {
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.DarkOrange),
            Background = Brush(Color.FromArgb(18, 255, 140, 0))
        };

        var bannerGrid = new Grid { ColumnSpacing = 16 };
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bannerText = new StackPanel { Spacing = 4 };
        bannerText.Children.Add(new TextBlock
        {
            Text = title ?? T("admin.title"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(Colors.DarkOrange)
        });
        bannerText.Children.Add(new TextBlock
        {
            Text = description ?? T("admin.bannerDesc"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });
        Grid.SetColumn(bannerText, 0);
        bannerGrid.Children.Add(bannerText);

        var elevateBtn = ActionButton(
            T("admin.elevateButton"),
            Symbol.Admin,
            (_, _) => MainWindow.ElevateApplication());
        Grid.SetColumn(elevateBtn, 1);
        bannerGrid.Children.Add(elevateBtn);

        adminWarningBanner.Child = bannerGrid;
        return adminWarningBanner;
    }
}
