using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Views;

public sealed partial class StartupPage : BasePage
{
    public StartupPage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderStartupPage();
    }

    private void RenderStartupPage()
    {
        AddHeader(T("startup.title"), T("startup.subtitle"));
        
        var resultPanel = new StackPanel { Spacing = 8 };
        var scanButton = ActionButton(T("startup.scan"), Symbol.Find, async (_, _) =>
        {
            MainWindow.SetStatusText(T("startup.scanning"));
            resultPanel.Children.Clear();
            var entries = await MainWindow.Startup.ScanAsync();
            resultPanel.Children.Add(SectionTitle(F("startup.entries", entries.Count)));
            foreach (var entry in entries)
            {
                resultPanel.Children.Add(StartupRow(entry));
            }
            MainWindow.SetStatusText(T("common.ready"));
        });

        MainContent.Children.Add(scanButton);
        MainContent.Children.Add(resultPanel);
    }

    private Border StartupRow(StartupEntry entry)
    {
        return Card(
            entry.Name,
            $"{entry.Source} / {(entry.Enabled ? T("startup.enabled") : T("startup.disabled"))}\n{entry.Command}\n{entry.RiskHint}",
            T("common.open"),
            (_, _) => MainWindow.OpenContainingFolder_Internal(entry.Command));
    }
}
