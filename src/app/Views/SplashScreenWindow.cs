using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed class SplashScreenWindow : Window
{
    private const int SplashWidth = 620;
    private const int SplashHeight = 380;

    private readonly TextBlock _statusText;
    private readonly TaskCompletionSource<bool> _loadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IntPtr _windowHandle;

    public SplashScreenWindow(ElementTheme theme)
    {
        _windowHandle = WindowNative.GetWindowHandle(this);
        Title = "Windows System Maintenance";
        SystemBackdrop = null;
        ExtendsContentIntoTitleBar = true; // Fixes black borders around the window
        var (layout, statusText) = CreateLayout(theme);
        _statusText = statusText;
        Content = layout;
        ConfigureWindow();
        Activated += (_, _) => DispatcherQueue.TryEnqueue(ConfigureWindow);

        if (layout.IsLoaded)
        {
            _loadedTcs.TrySetResult(true);
        }
        else
        {
            layout.Loaded += (_, _) => _loadedTcs.TrySetResult(true);
        }
    }

    public void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    public async Task WaitUntilReadyAsync()
    {
        await Task.WhenAny(_loadedTcs.Task, Task.Delay(1200));
        await Task.Delay(350);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private static (Grid Layout, TextBlock StatusText) CreateLayout(ElementTheme theme)
    {
        var isDark = theme == ElementTheme.Dark;
        var background = isDark
            ? Color.FromArgb(255, 20, 24, 32)
            : Color.FromArgb(255, 244, 247, 251);
        var surfaceColor = isDark
            ? Color.FromArgb(255, 31, 36, 46)
            : Color.FromArgb(255, 255, 255, 255);
        var border = isDark
            ? Color.FromArgb(255, 62, 70, 84)
            : Color.FromArgb(255, 214, 222, 232);
        var primaryText = isDark ? Colors.White : Color.FromArgb(255, 25, 32, 44);
        var secondaryText = isDark
            ? Color.FromArgb(215, 255, 255, 255)
            : Color.FromArgb(220, 58, 68, 84);
        var mutedText = isDark
            ? Color.FromArgb(180, 255, 255, 255)
            : Color.FromArgb(190, 82, 94, 112);
        var progressTrack = isDark
            ? Color.FromArgb(45, 255, 255, 255)
            : Color.FromArgb(255, 223, 229, 238);

        var root = new Grid
        {
            RequestedTheme = theme,
            Background = new SolidColorBrush(background)
        };

        var surface = new Border
        {
            Margin = new Thickness(20),
            Padding = new Thickness(32),
            CornerRadius = new CornerRadius(24),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(border),
            Background = new SolidColorBrush(surfaceColor)
        };

        var content = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var hero = new Grid
        {
            ColumnSpacing = 24,
            Margin = new Thickness(0, 0, 0, 26)
        };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        hero.ColumnDefinitions.Add(new ColumnDefinition());

        hero.Children.Add(CreateLogoTile(isDark));

        var titleStack = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(new TextBlock
        {
            Text = "Maintenance",
            FontSize = 32,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(primaryText),
            TextWrapping = TextWrapping.Wrap
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Windows optimization tool",
            FontSize = 13,
            Foreground = new SolidColorBrush(mutedText),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(titleStack, 1);
        hero.Children.Add(titleStack);
        Grid.SetRow(hero, 0);
        content.Children.Add(hero);

        var statusText = new TextBlock
        {
            Text = "Starting...",
            FontSize = 13,
            Foreground = new SolidColorBrush(mutedText),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 2, 14)
        };
        Grid.SetRow(statusText, 1);
        content.Children.Add(statusText);

        var progress = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 5,
            CornerRadius = new CornerRadius(3),
            Foreground = new SolidColorBrush(Colors.DeepSkyBlue),
            Background = new SolidColorBrush(progressTrack)
        };
        Grid.SetRow(progress, 2);
        content.Children.Add(progress);

        surface.Child = content;
        root.Children.Add(surface);
        return (root, statusText);
    }

    private static FrameworkElement CreateLogoTile(bool isDark)
    {
        var tile = new Border
        {
            Width = 86,
            Height = 86,
            CornerRadius = new CornerRadius(22),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Colors.DeepSkyBlue, Offset = 0 },
                    new GradientStop { Color = Colors.MediumPurple, Offset = 1 }
                }
            },
            Child = CreateLogoContent(isDark)
        };

        return tile;
    }

    private static UIElement CreateLogoContent(bool isDark)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
        if (File.Exists(iconPath))
        {
            return new Image
            {
                Width = 52,
                Height = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath))
            };
        }

        return new FontIcon
        {
            Glyph = "\uE90F",
            FontSize = 42,
            Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromArgb(255, 18, 24, 34)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void ConfigureWindow()
    {
        try
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow is null)
            {
                return;
            }

            var dpi = GetDpiForWindow(_windowHandle);
            var scaleFactor = dpi / 96.0;
            var physicalWidth = (int)(SplashWidth * scaleFactor);
            var physicalHeight = (int)(SplashHeight * scaleFactor);

            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                var workArea = displayArea.WorkArea;
                appWindow.Move(new Windows.Graphics.PointInt32(
                    workArea.X + ((workArea.Width - physicalWidth) / 2),
                    workArea.Y + ((workArea.Height - physicalHeight) / 2)));
            }

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Splash screen sizing is best-effort.
        }
    }
}
