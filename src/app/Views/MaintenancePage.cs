using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
}
