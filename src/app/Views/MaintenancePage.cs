using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class MaintenancePage : BasePage
{
    public MaintenancePage(
        MainWindow mainWindow,
        string title,
        string group,
        bool includePrivacy = false,
        bool includeOptimization = false) : base(mainWindow)
    {
        AddHeader(title, T("taskPage.subtitle"));

        var groups = new List<string> { group };
        if (includePrivacy)
        {
            groups.Add("Privacy");
        }

        if (includeOptimization)
        {
            groups.Add("Optimization");
        }

        if (!SystemStatusService.IsAdministrator())
        {
            var hasAdminTasks = groups.Any(g => MainWindow.Catalog.ByGroup(g).Any(t => t.RequiresAdmin));
            if (hasAdminTasks)
            {
                MainContent.Children.Add(CreateAdminWarningBanner());
            }
        }

        if (includePrivacy)
        {
            MainContent.Children.Add(PrivacyCleanerPanel());
        }

        foreach (var groupName in groups)
        {
            MainContent.Children.Add(SectionTitle(MainWindow.Localization.GroupName(groupName)));
            foreach (var task in MainWindow.Catalog.ByGroup(groupName))
            {
                AddTaskRow(task);
            }
        }
    }

    private void AddTaskRow(MaintenanceTask task)
    {
        var row = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Microsoft.UI.Colors.LightGray),
            Background = Brush(Windows.UI.Color.FromArgb(24, 128, 128, 128))
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = MainWindow.TaskLabel_Internal(task), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = MainWindow.TaskDescription_Internal(task), TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        text.Children.Add(new TextBlock { Text = MainWindow.TaskImpact_Internal(task), TextWrapping = TextWrapping.Wrap, Opacity = 0.65 });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var risk = RiskBadge(task.RiskLevel, MainWindow.Localization.RiskName(task.RiskLevel));
        Grid.SetColumn(risk, 1);
        grid.Children.Add(risk);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (task.CanPreview)
        {
            actions.Children.Add(IconButton(Symbol.Find, T("common.scan"), async (_, _) => await MainWindow.PreviewTaskAsync(task)));
        }
        actions.Children.Add(IconButton(Symbol.Play, T("common.run"), async (_, _) => await MainWindow.RunTaskAsync(task)));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        row.Child = grid;
        MainContent.Children.Add(row);
    }

    private Border PrivacyCleanerPanel()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(SectionTitle(T("privacy.cleaner")));
        stack.Children.Add(InfoBlock(T("privacy.cleanerDesc")));

        foreach (var item in PrivacyCleanerCatalog.BuildDefault())
        {
            stack.Children.Add(PrivacyCleanerRow(item));
        }

        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Microsoft.UI.Colors.LightGray),
            Background = Brush(Windows.UI.Color.FromArgb(12, 128, 128, 128)),
            Child = stack
        };
    }

    private Grid PrivacyCleanerRow(PrivacyCleanupItem item)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 44 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var risk = RiskBadge(item.RiskLevel, MainWindow.Localization.RiskName(item.RiskLevel));
        Grid.SetColumn(risk, 0);
        row.Children.Add(risk);

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = LocalizedPrivacyValue(item.Id, "label", item.Label),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{LocalizedPrivacyValue(item.Id, "source", item.Source)} / {LocalizedPrivacyValue(item.Id, "recommendation", item.Recommendation)}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        var stateText = item.IsSensitive
            ? T("privacy.sensitive")
            : item.IsSelectedByDefault ? T("privacy.defaultSelected") : T("privacy.optional");
        var state = new TextBlock
        {
            Text = stateText,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = item.IsSensitive ? 0.92 : 0.72,
            Foreground = item.IsSensitive ? Brush(Microsoft.UI.Colors.IndianRed) : null
        };
        Grid.SetColumn(state, 2);
        row.Children.Add(state);

        var action = new Button
        {
            Content = item.CanCleanAutomatically ? T("common.run") : T("common.scan"),
            MinWidth = 82,
            IsEnabled = item.CanCleanAutomatically && MainWindow.Catalog.TryGetById(item.Id, out _)
        };
        ToolTipService.SetToolTip(action, item.CanCleanAutomatically ? T("common.run") : T("privacy.previewOnly"));
        action.Click += async (_, _) =>
        {
            if (MainWindow.Catalog.TryGetById(item.Id, out var task))
            {
                await MainWindow.RunTaskAsync(task);
            }
        };
        Grid.SetColumn(action, 3);
        row.Children.Add(action);

        return row;
    }

    private string LocalizedPrivacyValue(string itemId, string field, string fallback)
    {
        var key = $"privacy.item.{itemId}.{field}";
        var value = T(key);
        return value == key ? fallback : value;
    }
}
