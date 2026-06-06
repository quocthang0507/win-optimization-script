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
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            actions.Children.Add(ActionButton(T("history.clearAll"), Symbol.Delete, async (_, _) => await ClearAllHistoryAsync(logsDir)));
            MainContent.Children.Add(actions);
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
