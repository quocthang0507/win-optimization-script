using WinOptimizationApp.Models;

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
        stack.Children.Add(new TextBlock { Text = T("settings.acknowledgments"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 18 });
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

        stack.Children.Add(list);
        border.Child = stack;

        return border;
    }
}
