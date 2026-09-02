using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class SettingsPage : BasePage
{
    public SettingsPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderSettingsPage();
    }

    private void RenderSettingsPage()
    {
        AddHeader(T("settings.title"), T("settings.subtitle"));

        if (!string.IsNullOrWhiteSpace(MainWindow.SettingsService.RecoveredCorruptSettingsPath))
        {
            MainContent.Children.Add(InfoBlock(F(
                "settings.corruptRecovered",
                MainWindow.SettingsService.RecoveredCorruptSettingsPath)));
        }

        MainContent.Children.Add(LanguageCard());
        MainContent.Children.Add(ThemeCard());
        MainContent.Children.Add(WinUiStyleCard());
        MainContent.Children.Add(WidgetCard());
        MainContent.Children.Add(ProtectedPathsCard());
        MainContent.Children.Add(Winapp2DatabaseCard());

        MainContent.Children.Add(Card(
            T("settings.cliScript"),
            MainWindow.Paths.CliScriptPath,
            T("common.launch"),
            async (_, _) => await MainWindow.RunTaskAsync(MainWindow.Catalog.GetById("cli.launch"))));

        MainContent.Children.Add(Card(
            T("settings.storageSense"),
            T("settings.storageSenseDescription"),
            T("common.open"),
            async (_, _) => await MainWindow.RunTaskAsync(MainWindow.Catalog.GetById("settings.storage"))));

        MainContent.Children.Add(Card(
            T("settings.logs"),
            MainWindow.Paths.LogsDirectory,
            T("common.open"),
            (_, _) => MainWindow.OpenFolder_Internal(MainWindow.Paths.LogsDirectory)));

        MainContent.Children.Add(Card(
            T("settings.repository"),
            MainWindow.Paths.RepositoryRoot,
            T("common.open"),
            (_, _) => MainWindow.OpenFolder_Internal(MainWindow.Paths.RepositoryRoot)));

        MainContent.Children.Add(AcknowledgmentsCard());
    }

    private Border LanguageCard()
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
        text.Children.Add(new TextBlock { Text = T("settings.language"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.languageDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var combo = new ComboBox { MinWidth = 170 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, T("settings.language"));
        combo.Items.Add(new ComboBoxItem { Content = "English", Tag = AppLanguage.English });
        combo.Items.Add(new ComboBoxItem { Content = "Tiếng Việt", Tag = AppLanguage.Vietnamese });
        combo.SelectedIndex = MainWindow.Localization.CurrentLanguage == AppLanguage.Vietnamese ? 1 : 0;
        combo.SelectionChanged += async (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppLanguage language && language != MainWindow.Localization.CurrentLanguage)
            {
                await MainWindow.ChangeLanguageAsync(language);
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private Border ThemeCard()
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
        text.Children.Add(new TextBlock { Text = T("settings.theme"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.themeDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var currentTheme = MainWindow.Settings.Theme ?? AppTheme.System;
        var combo = new ComboBox { MinWidth = 170 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, T("settings.theme"));
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeSystem"), Tag = AppTheme.System });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeLight"), Tag = AppTheme.Light });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.themeDark"), Tag = AppTheme.Dark });
        combo.SelectedIndex = currentTheme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };
        combo.SelectionChanged += async (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppTheme theme && theme != (MainWindow.Settings.Theme ?? AppTheme.System))
            {
                MainWindow.Settings.Theme = theme;
                var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
                await MainWindow.ApplyThemeAsync_Internal(theme);
                MainWindow.SetStatusText(saved ? T("settings.saved") : MainWindow.FormatTranslation("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private Border WinUiStyleCard()
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
        text.Children.Add(new TextBlock { Text = T("settings.winUiStyle"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.winUiStyleDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var currentStyle = MainWindow.Settings.WinUiStyle ?? AppWinUiStyle.Default;
        var combo = new ComboBox { MinWidth = 170 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, T("settings.winUiStyle"));
        combo.Items.Add(new ComboBoxItem { Content = T("settings.winUiStyleDefault"), Tag = AppWinUiStyle.Default });
        combo.Items.Add(new ComboBoxItem { Content = "Mica", Tag = AppWinUiStyle.Mica });
        combo.Items.Add(new ComboBoxItem { Content = "Acrylic", Tag = AppWinUiStyle.Acrylic });
        combo.Items.Add(new ComboBoxItem { Content = T("settings.winUiStyleSolid"), Tag = AppWinUiStyle.Solid });
        combo.SelectedIndex = currentStyle switch
        {
            AppWinUiStyle.Mica => 1,
            AppWinUiStyle.Acrylic => 2,
            AppWinUiStyle.Solid => 3,
            _ => 0
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is AppWinUiStyle style && style != (MainWindow.Settings.WinUiStyle ?? AppWinUiStyle.Default))
            {
                MainWindow.Settings.WinUiStyle = style;
                MainWindow.ApplyWinUiStyle_Internal(style);
                var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
                MainWindow.SetStatusText(saved ? T("settings.saved") : MainWindow.FormatTranslation("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
            }
        };

        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        border.Child = grid;
        return border;
    }

    private Border WidgetCard()
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
        text.Children.Add(new TextBlock { Text = T("settings.widget"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = T("settings.widgetDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        grid.Children.Add(text);

        var toggle = new ToggleSwitch
        {
            IsOn = MainWindow.Settings.WidgetEnabled,
            OnContent = T("common.yes"),
            OffContent = T("common.no")
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, T("settings.widget"));
        toggle.Toggled += (s, e) =>
        {
            MainWindow.Settings.WidgetEnabled = toggle.IsOn;
            var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
            MainWindow.SetStatusText(saved ? T("settings.saved") : MainWindow.FormatTranslation("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
            MainWindow.ToggleWidget(toggle.IsOn);
        };

        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        border.Child = grid;
        return border;
    }

    private Border ProtectedPathsCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
        };

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = T("settings.protectedPaths"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = T("settings.protectedPathsDescription"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });

        var pathList = new StackPanel { Spacing = 6 };
        void RenderPaths()
        {
            pathList.Children.Clear();
            if (MainWindow.Settings.ProtectedPaths.Count == 0)
            {
                pathList.Children.Add(new TextBlock { Text = T("settings.noProtectedPaths"), Opacity = 0.65 });
                return;
            }

            foreach (var path in MainWindow.Settings.ProtectedPaths.ToList())
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = path,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ToolTipService.SetToolTip(label, path);
                row.Children.Add(label);

                var remove = IconButton(Symbol.Delete, T("settings.removeProtectedPath"), (_, _) =>
                {
                    MainWindow.Settings.ProtectedPaths.RemoveAll(item => item.Equals(path, StringComparison.OrdinalIgnoreCase));
                    var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
                    MainWindow.SetStatusText(saved ? T("settings.saved") : F("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
                    RenderPaths();
                });
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                pathList.Children.Add(row);
            }
        }

        stack.Children.Add(pathList);
        stack.Children.Add(ActionButton(T("settings.addProtectedPath"), Symbol.Add, (_, _) =>
        {
            var path = FolderPickerHelper.PickFolder(MainWindow.WindowHandle);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            MainWindow.Settings.ProtectedPaths = ProtectedPathService.NormalizePaths(
                MainWindow.Settings.ProtectedPaths.Append(path)).ToList();
            var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
            MainWindow.SetStatusText(saved ? T("settings.saved") : F("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
            RenderPaths();
        }));

        RenderPaths();
        border.Child = stack;
        return border;
    }

    private Border Winapp2DatabaseCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground()
        };
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = T("settings.winapp2Database"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = T("settings.winapp2DatabaseDescription"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        var source = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(MainWindow.Settings.CustomWinapp2DatabasePath)
                ? T("settings.winapp2Bundled")
                : MainWindow.Settings.CustomWinapp2DatabasePath,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Opacity = 0.78
        };
        ToolTipService.SetToolTip(source, source.Text);
        stack.Children.Add(source);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(ActionButton(T("settings.selectWinapp2Database"), Symbol.OpenFile, async (_, _) =>
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".ini");
            InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            MainWindow.Settings.CustomWinapp2DatabasePath = file.Path;
            var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
            source.Text = file.Path;
            ToolTipService.SetToolTip(source, file.Path);
            MainWindow.SessionState.Winapp2Loaded = false;
            await MainWindow.RefreshWinapp2StateAsync();
            MainWindow.SetStatusText(!string.IsNullOrWhiteSpace(MainWindow.SessionState.Winapp2Error)
                ? MainWindow.SessionState.Winapp2Error
                : saved ? T("settings.saved") : F("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
        }));
        actions.Children.Add(ActionButton(T("settings.useBundledWinapp2"), Symbol.Refresh, async (_, _) =>
        {
            MainWindow.Settings.CustomWinapp2DatabasePath = null;
            var saved = MainWindow.SettingsService.Save(MainWindow.Settings);
            source.Text = T("settings.winapp2Bundled");
            ToolTipService.SetToolTip(source, source.Text);
            MainWindow.SessionState.Winapp2Loaded = false;
            await MainWindow.RefreshWinapp2StateAsync();
            MainWindow.SetStatusText(saved ? T("settings.saved") : F("settings.saveFailed", MainWindow.SettingsService.SettingsPath));
        }));
        stack.Children.Add(actions);
        border.Child = stack;
        return border;
    }

    private Border AcknowledgmentsCard()
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Margin = new Thickness(0, 16, 0, 0)
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = T("settings.about"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 18 });
        stack.Children.Add(new TextBlock { Text = T("settings.aboutDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
        stack.Children.Add(new TextBlock { Text = F("settings.version", version), Opacity = 0.68 });
        stack.Children.Add(new TextBlock
        {
            Text = T("settings.acknowledgments"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16,
            Margin = new Thickness(0, 8, 0, 0)
        });
        stack.Children.Add(new TextBlock { Text = T("settings.acknowledgmentsDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(8, 4, 0, 0) };
        
        var fluentCleaner = new HyperlinkButton { Content = "FluentCleaner (builtbybel)", NavigateUri = new Uri("https://github.com/builtbybel/FluentCleaner") };
        var fluentDesc = new TextBlock { Text = T("settings.fluentCleanerCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(fluentCleaner);
        list.Children.Add(fluentDesc);

        var winhance = new HyperlinkButton { Content = "Winhance (memstechtips)", NavigateUri = new Uri("https://github.com/memstechtips/Winhance"), Margin = new Thickness(0, 8, 0, 0) };
        var winhanceDesc = new TextBlock { Text = T("settings.winhanceCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(winhance);
        list.Children.Add(winhanceDesc);

        var win11Debloat = new HyperlinkButton { Content = "Win11Debloat (Raphire)", NavigateUri = new Uri("https://github.com/Raphire/Win11Debloat"), Margin = new Thickness(0, 8, 0, 0) };
        var win11DebloatDesc = new TextBlock { Text = T("settings.win11DebloatCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(win11Debloat);
        list.Children.Add(win11DebloatDesc);

        var optimizer = new HyperlinkButton { Content = "Optimizer (hellzerg)", NavigateUri = new Uri("https://github.com/hellzerg/optimizer"), Margin = new Thickness(0, 8, 0, 0) };
        var optimizerDesc = new TextBlock { Text = T("settings.optimizerCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(optimizer);
        list.Children.Add(optimizerDesc);

        var qDirStat = new HyperlinkButton { Content = "QDirStat (shundhammer)", NavigateUri = new Uri("https://github.com/shundhammer/qdirstat"), Margin = new Thickness(0, 8, 0, 0) };
        var qDirStatDesc = new TextBlock { Text = T("settings.qDirStatCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(qDirStat);
        list.Children.Add(qDirStatDesc);

        var winMole = new HyperlinkButton { Content = "WinMole (bhadraagada)", NavigateUri = new Uri("https://github.com/bhadraagada/winmole"), Margin = new Thickness(0, 8, 0, 0) };
        var winMoleDesc = new TextBlock { Text = T("settings.winMoleCredit"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        list.Children.Add(winMole);
        list.Children.Add(winMoleDesc);

        stack.Children.Add(list);
        border.Child = stack;

        return border;
    }
}
