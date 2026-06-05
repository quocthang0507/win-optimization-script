using Microsoft.UI.Xaml.Controls;

namespace WinOptimizationApp.Views;

public sealed partial class UpdatesPage : BasePage
{
    public UpdatesPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderUpdatesPage();
    }

    private void RenderUpdatesPage()
    {
        AddHeader(T("updates.title"), T("updates.subtitle"));

        var resultPanel = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        actions.Children.Add(ActionButton(T("updates.scanWinget"), Symbol.Find, async (_, _) =>
        {
            await MainWindow.ScanWingetAsync(resultPanel);
        }));

        actions.Children.Add(ActionButton(T("updates.upgradeAll"), Symbol.Download, async (_, _) =>
        {
            var task = MainWindow.Catalog.GetById("software.winget");
            await MainWindow.RunTaskAsync(task);
        }));

        MainContent.Children.Add(actions);
        MainContent.Children.Add(resultPanel);
    }
}
