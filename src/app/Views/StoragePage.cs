using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinRT.Interop;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace WinOptimizationApp.Views;

public sealed partial class StoragePage : BasePage
{
    private CancellationTokenSource? _diskScanCts;
    private DiskScanResult? _lastDiskScan;

    public StoragePage(MainWindow mainWindow) : base(mainWindow)
    {
        RenderStorageAnalyzerPage();
    }

    private void RenderStorageAnalyzerPage()
    {
        AddHeader(T("storage.title"), T("storage.subtitle"));

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var rootBox = new TextBox
        {
            Header = T("storage.driveOrFolder"),
            Text = _lastDiskScan?.Root.FullPath ?? systemRoot,
            MinWidth = 360,
            PlaceholderText = T("storage.placeholder")
        };

        var driveBox = new ComboBox { Header = T("storage.drive"), MinWidth = 180 };
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
        {
            driveBox.Items.Add($"{drive.Name}  {Formatters.FormatBytes(drive.AvailableFreeSpace)} {T("storage.free")}");
        }

        driveBox.SelectionChanged += (_, _) =>
        {
            if (driveBox.SelectedItem is string selected && selected.Length >= 3)
            {
                rootBox.Text = selected[..3];
            }
        };

        var includeHidden = new CheckBox { Content = T("common.hidden"), VerticalAlignment = VerticalAlignment.Bottom };
        var includeSystem = new CheckBox { Content = T("common.system"), VerticalAlignment = VerticalAlignment.Bottom };
        var followLinks = new CheckBox { Content = T("common.followLinks"), VerticalAlignment = VerticalAlignment.Bottom };
        ToolTipService.SetToolTip(followLinks, T("storage.followTooltip"));

        var resultPanel = new StackPanel { Spacing = 14 };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 5, Visibility = Visibility.Collapsed };
        var progressText = new TextBlock { Opacity = 0.72, TextWrapping = TextWrapping.Wrap };

        Button? scanButton = null;
        Button? stopButton = null;

        scanButton = ActionButton(T("common.scan"), Symbol.Find, async (_, _) =>
        {
            await StartDiskScanAsync(
                rootBox.Text,
                includeHidden.IsChecked == true,
                includeSystem.IsChecked == true,
                followLinks.IsChecked == true,
                progress,
                progressText,
                resultPanel);
        });

        stopButton = ActionButton(T("common.stop"), Symbol.Stop, (_, _) =>
        {
            _diskScanCts?.Cancel();
            MainWindow.SetStatusText(T("storage.stopping"));
        });
        stopButton.IsEnabled = false;

        var browseButton = ActionButton(T("common.browse"), Symbol.OpenFile, async (_, _) =>
        {
            var folder = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                rootBox.Text = folder;
            }
        });

        var commandGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition());
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(rootBox, 0);
        commandGrid.Children.Add(rootBox);
        Grid.SetColumn(driveBox, 1);
        commandGrid.Children.Add(driveBox);
        Grid.SetColumn(includeHidden, 2);
        commandGrid.Children.Add(includeHidden);
        Grid.SetColumn(includeSystem, 3);
        commandGrid.Children.Add(includeSystem);
        Grid.SetColumn(followLinks, 4);
        commandGrid.Children.Add(followLinks);
        Grid.SetColumn(browseButton, 5);
        commandGrid.Children.Add(browseButton);
        Grid.SetColumn(scanButton, 6);
        commandGrid.Children.Add(scanButton);

        var stopRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        stopRow.Children.Add(stopButton);
        stopRow.Children.Add(progressText);

        MainContent.Children.Add(commandGrid);
        MainContent.Children.Add(progress);
        MainContent.Children.Add(stopRow);
        MainContent.Children.Add(resultPanel);

        if (_lastDiskScan is not null)
        {
            RenderStorageResults(resultPanel, _lastDiskScan);
        }

        async Task StartDiskScanAsync(
            string rootPath,
            bool withHidden,
            bool withSystem,
            bool withLinks,
            ProgressBar progressBar,
            TextBlock statusBlock,
            StackPanel output)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                await MainWindow.ShowDialogAsync_Internal(T("storage.cleanupReview"), InfoBlock(T("storage.selectAtLeastOne")), T("common.close"));
                return;
            }

            if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
            {
                await MainWindow.ShowDialogAsync_Internal(T("storage.pathNotFound"), InfoBlock(rootPath), T("common.close"));
                return;
            }

            _diskScanCts?.Cancel();
            _diskScanCts = new CancellationTokenSource();
            scanButton!.IsEnabled = false;
            stopButton!.IsEnabled = true;
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;
            output.Children.Clear();
            MainWindow.SetStatusText(T("storage.scanning"));

            var options = new DiskScanOptions(rootPath, withHidden, withSystem, withLinks);
            var scanProgress = new Progress<DiskScanProgress>(value =>
            {
                statusBlock.Text = F("storage.progress", Formatters.FormatBytes(value.TotalBytes), value.FileCount, value.FolderCount, value.SkippedCount, value.CurrentPath);
            });

            try
            {
                var result = await MainWindow.DiskAnalysis.ScanAsync(options, scanProgress, _diskScanCts.Token);
                _lastDiskScan = result;
                RenderStorageResults(output, result);
                statusBlock.Text = F("storage.completedIn", (result.FinishedAt - result.StartedAt).TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                statusBlock.Text = T("storage.scanCanceled");
                output.Children.Add(InfoBlock(T("storage.scanCanceledDetail")));
            }
            catch (Exception ex)
            {
                statusBlock.Text = ex.Message;
                output.Children.Add(InfoBlock(F("storage.scanFailed", ex.Message)));
            }
            finally
            {
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                scanButton!.IsEnabled = true;
                stopButton!.IsEnabled = false;
                MainWindow.SetStatusText(T("common.ready"));
            }
        }
    }

    private void RenderStorageResults(StackPanel resultPanel, DiskScanResult result)
    {
        resultPanel.Children.Clear();

        var summary = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.RowDefinitions.Add(new RowDefinition());
        var largestFolders = MainWindow.DiskAnalysis.GetLargestDirectories(result, 1);
        var largestFolder = largestFolders.Count > 0 ? largestFolders[0] : null;
        AddMetric(summary, 0, 0, T("storage.scanned"), Formatters.FormatBytes(result.TotalBytes), F("storage.filesFolders", result.FileCount, result.FolderCount), Colors.SteelBlue);
        AddMetric(summary, 0, 1, T("storage.largestFolder"), largestFolder is null ? T("common.none") : Formatters.FormatBytes(largestFolder.Size), largestFolder?.FullPath ?? result.Root.FullPath, Colors.DarkCyan);
        AddMetric(summary, 0, 2, T("storage.skipped"), $"{result.SkippedCount:N0}", F("storage.errors", result.Errors.Count), result.Errors.Count > 0 ? Colors.DarkOrange : Colors.SeaGreen);
        resultPanel.Children.Add(summary);

        var candidatePanel = new StackPanel { Spacing = 8 };
        var candidates = MainWindow.StorageCleanup.CreateCandidates(result);
        if (candidates.Count > 0)
        {
            var selected = new List<(CheckBox Box, StorageCleanupCandidate Candidate)>();
            var candidateHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            candidateHeader.Children.Add(SectionTitle(T("storage.cleanupReview")));
            candidateHeader.Children.Add(ActionButton(T("storage.reviewSelected"), Symbol.Delete, async (_, _) =>
            {
                var picked = selected.Where(item => item.Box.IsChecked == true).Select(item => item.Candidate).ToList();
                await ReviewStorageCandidatesAsync(picked);
            }));
            candidatePanel.Children.Add(candidateHeader);

            foreach (var candidate in candidates.Take(12))
            {
                var checkBox = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                selected.Add((checkBox, candidate));
                candidatePanel.Children.Add(StorageCandidateRow(candidate, checkBox));
            }

            resultPanel.Children.Add(candidatePanel);
        }

        resultPanel.Children.Add(SectionTitle(T("storage.spaceMap")));
        foreach (var directory in MainWindow.DiskAnalysis.GetLargestDirectories(result, 12))
        {
            resultPanel.Children.Add(StorageBarRow(directory, result.TotalBytes));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.folderTree")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.name"), T("storage.size"), T("storage.files"), T("storage.modified"), T("storage.action")));
        foreach (var item in MainWindow.DiskAnalysis.FlattenVisibleTree(result.Root, 120))
        {
            resultPanel.Children.Add(StorageDiskItemRow(item, result.Root));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.largestFiles")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.name"), T("storage.size"), T("storage.type"), T("storage.modified"), T("storage.action")));
        foreach (var file in result.LargestFiles.Take(80))
        {
            resultPanel.Children.Add(StorageFileRow(file));
        }

        resultPanel.Children.Add(SectionTitle(T("storage.fileTypes")));
        resultPanel.Children.Add(StorageHeaderRow(T("storage.extension"), T("storage.size"), T("storage.count"), T("storage.lastModified"), T("storage.largest")));
        foreach (var type in result.FileTypes.Take(40))
        {
            resultPanel.Children.Add(StorageFileTypeRow(type));
        }

        if (result.Errors.Count > 0)
        {
            resultPanel.Children.Add(SectionTitle(T("storage.skippedErrors")));
            foreach (var error in result.Errors.Take(20))
            {
                resultPanel.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            }
        }
    }

    private static Grid StorageHeaderRow(string first, string second, string third, string fourth, string fifth)
    {
        var row = StorageGrid();
        row.Children.Add(HeaderText(first, 0));
        row.Children.Add(HeaderText(second, 1));
        row.Children.Add(HeaderText(third, 2));
        row.Children.Add(HeaderText(fourth, 3));
        row.Children.Add(HeaderText(fifth, 4));
        return row;

        static TextBlock HeaderText(string text, int column)
        {
            var block = new TextBlock { Text = text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = 0.75 };
            Grid.SetColumn(block, column);
            return block;
        }
    }

    private Grid StorageDiskItemRow(DiskItem item, DiskItem root)
    {
        var row = StorageGrid();
        row.Children.Add(CellText($"{(item.IsDirectory ? "[D]" : "[F]")} {item.Name}", 0, item.FullPath));
        row.Children.Add(CellText(Formatters.FormatBytes(item.Size), 1));
        row.Children.Add(CellText(item.FileCount.ToString("N0"), 2));
        row.Children.Add(CellText(FormatStorageDate(item.LastModified), 3));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("common.openLocation"), (_, _) => MainWindow.OpenContainingFolder_Internal(item.FullPath)));
        if (item.FullPath != root.FullPath)
        {
            actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
            {
                await ReviewStorageCandidatesAsync([CreateManualCandidate(item)]);
            }));
        }
        Grid.SetColumn(actions, 4);
        row.Children.Add(actions);
        return row;
    }

    private Grid StorageFileRow(DiskItem file)
    {
        var row = StorageGrid();
        row.Children.Add(CellText(file.Name, 0, file.FullPath));
        row.Children.Add(CellText(Formatters.FormatBytes(file.Size), 1));
        row.Children.Add(CellText(file.Extension, 2));
        row.Children.Add(CellText(FormatStorageDate(file.LastModified), 3));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconButton(Symbol.OpenFile, T("common.openLocation"), (_, _) => MainWindow.OpenContainingFolder_Internal(file.FullPath)));
        actions.Children.Add(IconButton(Symbol.Add, T("common.addCleanupReview"), async (_, _) =>
        {
            await ReviewStorageCandidatesAsync([CreateManualCandidate(file)]);
        }));
        Grid.SetColumn(actions, 4);
        row.Children.Add(actions);
        return row;
    }

    private static Grid StorageFileTypeRow(FileTypeSummary summary)
    {
        var row = StorageGrid();
        row.Children.Add(CellText(summary.Extension, 0));
        row.Children.Add(CellText(Formatters.FormatBytes(summary.TotalBytes), 1));
        row.Children.Add(CellText(summary.Count.ToString("N0"), 2));
        row.Children.Add(CellText(FormatStorageDate(summary.LastModified), 3));
        row.Children.Add(CellText(summary.LargestItemPath, 4));
        return row;
    }

    private static StackPanel StorageBarRow(DiskItem item, long totalBytes)
    {
        var percent = totalBytes > 0 ? Math.Max(2, Math.Min(100, item.Size * 100d / totalBytes)) : 0;
        var wrapper = new StackPanel { Spacing = 4 };
        wrapper.Children.Add(new TextBlock
        {
            Text = $"{item.Name}  {Formatters.FormatBytes(item.Size)}  ({percent:N1}%)",
            TextWrapping = TextWrapping.Wrap
        });

        var bar = new Grid { Height = 18 };
        bar.Children.Add(new Rectangle
        {
            Fill = Brush(Color.FromArgb(28, 128, 128, 128)),
            RadiusX = 3,
            RadiusY = 3
        });
        bar.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = percent,
            Height = 18,
            IsHitTestVisible = false
        });
        wrapper.Children.Add(bar);
        return wrapper;
    }

    private Grid StorageCandidateRow(StorageCleanupCandidate candidate, CheckBox checkBox)
    {
        var row = StorageGrid();
        Grid.SetColumn(checkBox, 0);
        row.Children.Add(checkBox);
        row.Children.Add(CellText(candidate.Label, 0, candidate.SourcePath, new Thickness(34, 0, 0, 0)));
        row.Children.Add(CellText(Formatters.FormatBytes(candidate.EstimatedBytes), 1));
        var risk = RiskBadge(candidate.RiskLevel, MainWindow.Localization.RiskName(candidate.RiskLevel));
        Grid.SetColumn(risk, 2);
        row.Children.Add(risk);
        row.Children.Add(CellText(candidate.Reason, 3));
        row.Children.Add(CellText(candidate.CleanupMode == StorageCleanupMode.MoveToRecycleBin ? T("common.recycleBin") : T("common.delete"), 4));
        return row;
    }

    private async Task ReviewStorageCandidatesAsync(List<StorageCleanupCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            await MainWindow.ShowDialogAsync_Internal(T("storage.cleanupReview"), InfoBlock(T("storage.selectAtLeastOne")), T("common.close"));
            return;
        }

        var panel = new StackPanel { Spacing = 10, MaxWidth = 760 };
        panel.Children.Add(new TextBlock
        {
            Text = F("storage.itemSummary", candidates.Count, Formatters.FormatBytes(candidates.Sum(candidate => candidate.EstimatedBytes))),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var listPanel = new StackPanel { Spacing = 6 };
        foreach (var candidate in candidates.Take(100))
        {
            listPanel.Children.Add(new TextBlock
            {
                Text = $"{candidate.Label} / {Formatters.FormatBytes(candidate.EstimatedBytes)} / {candidate.RiskLevel}\n{candidate.SourcePath}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.82
            });
        }

        if (candidates.Count > 100)
        {
            listPanel.Children.Add(new TextBlock { Text = F("storage.moreItems", candidates.Count - 100), Opacity = 0.65 });
        }

        var scrollViewer = new ScrollViewer
        {
            Content = listPanel,
            MaxHeight = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 8, 0, 0)
        };
        panel.Children.Add(scrollViewer);

        var dialog = new ContentDialog
        {
            Title = T("storage.moveQuestion"),
            Content = panel,
            PrimaryButtonText = T("common.move"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        MainWindow.SetStatusText(T("storage.cleaning"));
        var result = await MainWindow.StorageCleanup.CleanupAsync(candidates);
        await MainWindow.ShowRunResultAsync_Internal(result);
        MainWindow.SetStatusText(T("common.ready"));
    }

    private StorageCleanupCandidate CreateManualCandidate(DiskItem item)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var isUserFile = item.FullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
        var risk = item.IsDirectory || !isUserFile ? RiskLevel.High : RiskLevel.Medium;

        return new StorageCleanupCandidate(
            $"manual:{item.FullPath}",
            item.Name,
            item.FullPath,
            item.Size,
            risk,
            StorageCleanupMode.MoveToRecycleBin,
            T("storage.manualReason"),
            item.IsDirectory);
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static Grid StorageGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 7, 8, 7),
            Background = Brush(Color.FromArgb(10, 128, 128, 128))
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        return grid;
    }

    private static TextBlock CellText(string text, int column, string? tooltip = null, Thickness? margin = null)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.86,
            Margin = margin ?? new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            ToolTipService.SetToolTip(block, tooltip);
        }

        Grid.SetColumn(block, column);
        return block;
    }

    private static string FormatStorageDate(DateTimeOffset value)
    {
        return value <= DateTimeOffset.MinValue.AddDays(1) ? "-" : value.LocalDateTime.ToString("yyyy-MM-dd");
    }
}
