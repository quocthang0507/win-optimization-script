namespace WinOptimizationApp.Views;

public sealed partial class HistoryPage : BasePage
{
    public HistoryPage(MainWindow mainWindow) : base(mainWindow)
    {
    }

    public override Task OnNavigatedToAsync()
    {
        MainContent.Children.Clear();
        AddHeader(T("history.title"), T("history.subtitle"));

        var logsDir = MainWindow.Paths.LogsDirectory;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(ActionButton(T("history.openLogs"), Symbol.OpenFile, (_, _) => MainWindow.OpenFolder_Internal(logsDir)));
        MainContent.Children.Add(actions);

        var snapshots = MainWindow.TweakSnapshots.GetSnapshots();
        if (snapshots.Count > 0)
        {
            MainContent.Children.Add(SectionTitle(F("history.undoSnapshots", snapshots.Count)));
            foreach (var item in snapshots.Take(20))
            {
                MainContent.Children.Add(TweakSnapshotCard(item.Path, item.Snapshot));
            }
        }

        if (!Directory.Exists(logsDir))
        {
            MainContent.Children.Add(InfoBlock(T("history.empty")));
            return Task.CompletedTask;
        }

        var reports = Directory.GetFiles(logsDir, "maintenance-*.json")
            .OrderByDescending(File.GetLastWriteTime)
            .Take(30)
            .ToList();

        if (reports.Count > 0)
        {
            actions.Children.Add(ActionButton(T("history.clearAll"), Symbol.Delete, async (_, _) => await ClearAllHistoryAsync(logsDir)));
        }

        bool hasReports = false;
        foreach (var report in reports)
        {
            hasReports = true;
            MainContent.Children.Add(Card(
                Path.GetFileName(report),
                report,
                T("common.open"),
                (_, _) => MainWindow.OpenFile_Internal(report)));
        }

        if (!hasReports)
        {
            MainContent.Children.Add(InfoBlock(T("history.empty")));
        }

        return Task.CompletedTask;
    }

    private Border TweakSnapshotCard(string path, WinOptimizationApp.Models.TweakSnapshot snapshot)
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
        text.Children.Add(new TextBlock { Text = snapshot.Label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock
        {
            Text = F("history.snapshotDetail", snapshot.Values.Count, snapshot.CreatedAt.LocalDateTime),
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        grid.Children.Add(text);

        var undo = new Button { Content = T("history.undo"), MinWidth = 96 };
        undo.Click += async (_, _) => await UndoSnapshotAsync(path, snapshot);
        Grid.SetColumn(undo, 1);
        grid.Children.Add(undo);
        border.Child = grid;
        return border;
    }

    private async Task UndoSnapshotAsync(string path, WinOptimizationApp.Models.TweakSnapshot snapshot)
    {
        var dialog = new ContentDialog
        {
            Title = T("history.undoConfirmTitle"),
            Content = new TextBlock { Text = F("history.undoConfirmBody", snapshot.Values.Count), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = T("history.undo"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var tweaks = new WinOptimizationApp.Services.TweakService(MainWindow.Commands) { Client = MainWindow.IpcClient };
        var currentValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in snapshot.Values.Keys)
        {
            var state = await tweaks.CheckTweakStateAsync(id);
            if (string.IsNullOrWhiteSpace(state.Error)) currentValues[id] = state.IsEnabled;
        }
        await MainWindow.TweakSnapshots.SaveAsync(F("history.beforeUndo", snapshot.Label), currentValues);

        var failures = 0;
        foreach (var value in snapshot.Values)
        {
            var result = await tweaks.ApplyTweakAsync(value.Key, value.Value);
            if (!string.IsNullOrWhiteSpace(result.Error)) failures++;
        }

        if (failures == 0)
        {
            MainWindow.TweakSnapshots.Delete(path);
            MainWindow.SetStatusText(T("history.undoComplete"));
            await OnNavigatedToAsync();
        }
        else
        {
            MainWindow.SetStatusText(F("history.undoFailed", failures));
        }
    }

    private async Task ClearAllHistoryAsync(string logsDir)
    {
        var targets = Directory.GetFiles(logsDir, "maintenance-*.json")
            .Concat(Directory.GetFiles(logsDir, "maintenance-*.log"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targets.Count == 0)
        {
            MainContent.Children.Add(InfoBlock(T("history.empty")));
            return;
        }

        var dialog = new ContentDialog
        {
            Title = T("history.clearAllConfirmTitle"),
            Content = new TextBlock { Text = F("history.clearAllConfirmBody", targets.Count), TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
            PrimaryButtonText = T("common.delete"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            foreach (var target in targets)
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }

            MainWindow.SetStatusText(T("history.cleared"));
            await OnNavigatedToAsync();
        }
        catch (Exception ex)
        {
            await MainWindow.ShowDialogAsync_Internal(T("common.delete"), InfoBlock(ex.Message), T("common.close"));
        }
    }
}
