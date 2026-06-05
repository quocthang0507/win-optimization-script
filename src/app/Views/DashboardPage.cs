using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class DashboardPage : BasePage
{
    private readonly StackPanel _wingetResultPanel;

    public DashboardPage(MainWindow mainWindow) : base(mainWindow)
    {
        _wingetResultPanel = new StackPanel { Spacing = 8 };
    }

    public override async Task OnNavigatedToAsync()
    {
        MainContent.Children.Clear();
        _wingetResultPanel.Children.Clear();

        AddHeader(T("dashboard.title"), T("dashboard.subtitle"));
        
        var status = await MainWindow.Status.GetAsync();

        // System Health Section
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var card1 = CreateMetricCardWithIcon(T("dashboard.windows"), status.WindowsVersion, status.PendingReboot ? T("dashboard.pendingReboot") : T("dashboard.noRebootPending"), status.PendingReboot ? Colors.OrangeRed : Colors.SeaGreen, "\uE7F4");
        Grid.SetRow(card1, 0); Grid.SetColumn(card1, 0);
        grid.Children.Add(card1);

        var card2 = CreateMetricCardWithIcon(T("dashboard.administrator"), status.IsAdministrator ? T("dashboard.elevated") : T("dashboard.standardUser"), status.IsAdministrator ? T("dashboard.highRiskEnabled") : T("dashboard.highRiskNeedAdmin"), status.IsAdministrator ? Colors.SeaGreen : Colors.DarkOrange, "\uEA18");
        Grid.SetRow(card2, 0); Grid.SetColumn(card2, 1);
        grid.Children.Add(card2);

        var card3 = CreateMetricCardWithIcon(T("dashboard.systemDrive"), $"{Formatters.FormatBytes(status.SystemDriveFreeBytes)} {T("dashboard.free")}", $"{status.SystemDrive} of {Formatters.FormatBytes(status.SystemDriveTotalBytes)}", Colors.SteelBlue, "\uE7F1");
        Grid.SetRow(card3, 1); Grid.SetColumn(card3, 0);
        grid.Children.Add(card3);

        var card4 = CreateMetricCardWithIcon(T("dashboard.uptime"), Formatters.FormatDuration(status.Uptime, MainWindow.Localization.CurrentLanguage), status.WingetAvailable ? T("dashboard.wingetAvailable") : T("dashboard.wingetNotFound"), status.WingetAvailable ? Colors.SeaGreen : Colors.Gray, "\uE916");
        Grid.SetRow(card4, 1); Grid.SetColumn(card4, 1);
        grid.Children.Add(card4);

        MainContent.Children.Add(grid);

        // Hardware & Resources Section
        MainContent.Children.Add(SectionTitle(T("dashboard.hardware")));

        var hardwareCard = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(10, 128, 128, 128))
        };

        var hardwareStack = new StackPanel { Spacing = 20 };
        
        // 1. CPU Processor Name
        var cpuGrid = new Grid { ColumnSpacing = 16 };
        cpuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cpuGrid.ColumnDefinitions.Add(new ColumnDefinition());
        
        var cpuIcon = new FontIcon
        {
            Glyph = "\uE950", // CPU
            FontSize = 26,
            Foreground = Brush(Colors.MediumPurple),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(cpuIcon, 0);
        cpuGrid.Children.Add(cpuIcon);
        
        var cpuInfo = new StackPanel { Spacing = 2 };
        cpuInfo.Children.Add(new TextBlock { Text = T("dashboard.cpu"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        cpuInfo.Children.Add(new TextBlock { Text = status.CpuName, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(cpuInfo, 1);
        cpuGrid.Children.Add(cpuInfo);
        hardwareStack.Children.Add(cpuGrid);
        
        // 2. RAM Memory capacity progress bar
        var ramUsed = status.TotalRamBytes - status.AvailableRamBytes;
        double ramPercent = status.TotalRamBytes > 0 ? (ramUsed * 100.0 / status.TotalRamBytes) : 0;
        
        var ramGrid = new Grid { ColumnSpacing = 16 };
        ramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ramGrid.ColumnDefinitions.Add(new ColumnDefinition());
        
        var ramIcon = new FontIcon
        {
            Glyph = "\uE964", // RAM
            FontSize = 26,
            Foreground = Brush(Colors.DarkCyan),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(ramIcon, 0);
        ramGrid.Children.Add(ramIcon);
        
        var ramInfo = new StackPanel { Spacing = 6 };
        var ramHeader = new Grid();
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition());
        ramHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var ramTitle = new TextBlock { Text = T("dashboard.ram"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var ramText = new TextBlock 
        { 
            Text = $"{Formatters.FormatBytes((long)ramUsed)} / {Formatters.FormatBytes((long)status.TotalRamBytes)} ({ramPercent:F1}%)", 
            Opacity = 0.8 
        };
        Grid.SetColumn(ramTitle, 0);
        Grid.SetColumn(ramText, 1);
        ramHeader.Children.Add(ramTitle);
        ramHeader.Children.Add(ramText);
        
        var ramProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = ramPercent,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = Brush(ramPercent > 85 ? Colors.OrangeRed : ramPercent > 70 ? Colors.Goldenrod : Colors.DarkCyan)
        };
        
        ramInfo.Children.Add(ramHeader);
        ramInfo.Children.Add(ramProgress);
        Grid.SetColumn(ramInfo, 1);
        ramGrid.Children.Add(ramInfo);
        hardwareStack.Children.Add(ramGrid);
        
        // 3. Disk Space capacity progress bar
        var diskTotal = status.SystemDriveTotalBytes;
        var diskFree = status.SystemDriveFreeBytes;
        var diskUsed = diskTotal - diskFree;
        double diskPercent = diskTotal > 0 ? (diskUsed * 100.0 / diskTotal) : 0;
        
        var diskGrid = new Grid { ColumnSpacing = 16 };
        diskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        diskGrid.ColumnDefinitions.Add(new ColumnDefinition());
        
        var diskIcon = new FontIcon
        {
            Glyph = "\uE7F1", // Disk Storage
            FontSize = 26,
            Foreground = Brush(Colors.SteelBlue),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(diskIcon, 0);
        diskGrid.Children.Add(diskIcon);
        
        var diskInfo = new StackPanel { Spacing = 6 };
        var diskHeader = new Grid();
        diskHeader.ColumnDefinitions.Add(new ColumnDefinition());
        diskHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var diskTitle = new TextBlock { Text = $"{T("dashboard.storage")} ({status.SystemDrive})", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var diskText = new TextBlock 
        { 
            Text = $"{Formatters.FormatBytes(diskUsed)} / {Formatters.FormatBytes(diskTotal)} ({diskPercent:F1}%)", 
            Opacity = 0.8 
        };
        Grid.SetColumn(diskTitle, 0);
        Grid.SetColumn(diskText, 1);
        diskHeader.Children.Add(diskTitle);
        diskHeader.Children.Add(diskText);
        
        var diskProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = diskPercent,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = Brush(diskPercent > 85 ? Colors.OrangeRed : diskPercent > 70 ? Colors.Goldenrod : Colors.SteelBlue)
        };
        
        diskInfo.Children.Add(diskHeader);
        diskInfo.Children.Add(diskProgress);
        Grid.SetColumn(diskInfo, 1);
        diskGrid.Children.Add(diskInfo);
        hardwareStack.Children.Add(diskGrid);
        
        hardwareCard.Child = hardwareStack;
        MainContent.Children.Add(hardwareCard);

        // Quick Actions
        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        quick.Children.Add(ActionButton(T("dashboard.scanCleanup"), Symbol.Find, async (_, _) => await MainWindow.NavigateToTagAsync("cleanup")));
        quick.Children.Add(ActionButton(T("dashboard.analyzeStorage"), Symbol.View, async (_, _) => await MainWindow.NavigateToTagAsync("storage")));
        quick.Children.Add(ActionButton(T("dashboard.scanUpdates"), Symbol.Download, async (_, _) => await MainWindow.ScanWingetAsync(_wingetResultPanel)));
        quick.Children.Add(ActionButton(T("dashboard.openLogs"), Symbol.OpenFile, (_, _) => MainWindow.OpenFolder_Internal(MainWindow.Paths.LogsDirectory)));
        MainContent.Children.Add(quick);

        MainContent.Children.Add(_wingetResultPanel);

        if (!string.IsNullOrWhiteSpace(status.LastReportPath))
        {
            var border = new Border
            {
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = Brush(Colors.LightGray),
                Background = Brush(Color.FromArgb(18, 128, 128, 128))
            };

            var cardGrid = new Grid { ColumnSpacing = 12 };
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition());
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { Spacing = 4 };
            textStack.Children.Add(new TextBlock { Text = T("dashboard.lastReport"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            textStack.Children.Add(new TextBlock { Text = status.LastReportPath, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
            cardGrid.Children.Add(textStack);

            var openBtn = new Button { Content = T("common.open"), MinWidth = 86 };
            ToolTipService.SetToolTip(openBtn, T("common.open"));
            openBtn.Click += (_, _) => MainWindow.OpenFile_Internal(status.LastReportPath);
            Grid.SetColumn(openBtn, 1);
            cardGrid.Children.Add(openBtn);

            var deleteBtn = new Button { Content = T("common.delete"), MinWidth = 86 };
            ToolTipService.SetToolTip(deleteBtn, T("common.delete"));
            deleteBtn.Click += async (_, _) =>
            {
                var isVi = MainWindow.Localization.CurrentLanguage == AppLanguage.Vietnamese;
                var confirmTitle = isVi ? "Xóa báo cáo?" : "Delete report?";
                var confirmBody = isVi 
                    ? "Bạn có chắc chắn muốn xóa file báo cáo gần nhất và file log của nó không?" 
                    : "Are you sure you want to delete the last report file and its logs?";

                var confirmDialog = new ContentDialog
                {
                    Title = confirmTitle,
                    Content = new TextBlock { Text = confirmBody, TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = T("common.delete"),
                    CloseButtonText = T("common.cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = MainWindow.Navigation_Internal.XamlRoot
                };

                if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        var logFile = Path.ChangeExtension(status.LastReportPath, ".log");
                        if (File.Exists(status.LastReportPath))
                        {
                            File.Delete(status.LastReportPath);
                        }
                        if (File.Exists(logFile))
                        {
                            File.Delete(logFile);
                        }
                        MainWindow.SetStatusText(T("settings.saved"));
                        await MainWindow.NavigateAsync("dashboard");
                    }
                    catch (Exception ex)
                    {
                        await MainWindow.ShowDialogAsync_Internal(T("common.delete"), InfoBlock(ex.Message), T("common.close"));
                    }
                }
            };
            Grid.SetColumn(deleteBtn, 2);
            cardGrid.Children.Add(deleteBtn);

            border.Child = cardGrid;
            MainContent.Children.Add(border);
        }
    }

    private static Border CreateMetricCardWithIcon(
        string title, 
        string value, 
        string detail, 
        Color color, 
        string glyph)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(Colors.LightGray),
            Background = Brush(Color.FromArgb(16, color.R, color.G, color.B))
        };

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 28,
            Foreground = Brush(color),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var stack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock { Text = title, Opacity = 0.7, FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = detail, Foreground = Brush(color), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        card.Child = grid;
        return card;
    }
}
