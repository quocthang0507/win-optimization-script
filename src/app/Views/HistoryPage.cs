using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            .Take(30);

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
}
