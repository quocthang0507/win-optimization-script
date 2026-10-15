using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public static class CommandPaletteDialog
{
    public static async Task ShowAsync(MainWindow mainWindow)
    {
        var commands = BuildCommands(mainWindow);
        var filteredCommands = new List<CommandItem>(commands);

        var dialog = new ContentDialog
        {
            Title = mainWindow.Translate("commandPalette.title"),
            CloseButtonText = mainWindow.Translate("common.close"),
            DefaultButton = ContentDialogButton.None,
            XamlRoot = mainWindow.Navigation_Internal.XamlRoot
        };

        var rootPanel = new Grid { RowSpacing = 10, MinWidth = 520, MaxWidth = 620 };
        rootPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootPanel.RowDefinitions.Add(new RowDefinition());
        rootPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var searchBox = new TextBox
        {
            PlaceholderText = mainWindow.Translate("commandPalette.placeholder"),
            Height = 40,
            FontSize = 14
        };
        Grid.SetRow(searchBox, 0);
        rootPanel.Children.Add(searchBox);

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = filteredCommands,
            MaxHeight = 360,
            MinHeight = 120
        };

        listView.ContainerContentChanging += (s, args) =>
        {
            if (args.Item is CommandItem item)
            {
                if (args.ItemContainer.Content is not Border border)
                {
                    args.ItemContainer.Content = CreateCommandRow(item, mainWindow);
                }
                else
                {
                    UpdateCommandRow(border, item, mainWindow);
                }
            }
        };

        Grid.SetRow(listView, 1);
        rootPanel.Children.Add(listView);

        var hint = new TextBlock
        {
            Text = "↑↓ để duyệt, Enter để chạy, Esc để đóng / Use arrows to navigate, Enter to run, Esc to close",
            FontSize = 11,
            Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(hint, 2);
        rootPanel.Children.Add(hint);

        dialog.Content = rootPanel;

        async Task ExecuteSelectedAsync()
        {
            var selected = listView.SelectedItem as CommandItem ?? filteredCommands.FirstOrDefault();
            if (selected != null)
            {
                dialog.Hide();
                await selected.ExecuteAsync();
            }
        }

        searchBox.TextChanged += (_, _) =>
        {
            filteredCommands = CommandPaletteService.Filter(commands, searchBox.Text).ToList();
            listView.ItemsSource = filteredCommands;
            if (filteredCommands.Count > 0)
            {
                listView.SelectedIndex = 0;
            }
        };

        searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await ExecuteSelectedAsync();
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                if (listView.SelectedIndex < filteredCommands.Count - 1)
                {
                    listView.SelectedIndex++;
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                if (listView.SelectedIndex > 0)
                {
                    listView.SelectedIndex--;
                }
                e.Handled = true;
            }
        };

        listView.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is CommandItem item)
            {
                dialog.Hide();
                await item.ExecuteAsync();
            }
        };
        listView.IsItemClickEnabled = true;

        if (filteredCommands.Count > 0)
        {
            listView.SelectedIndex = 0;
        }

        searchBox.Loaded += (_, _) => searchBox.Focus(FocusState.Programmatic);

        await mainWindow.ShowThemedDialogAsync(dialog);
    }

    public static IReadOnlyList<CommandItem> BuildCommands(MainWindow mainWindow)
    {
        var list = new List<CommandItem>();

        // 1. Navigation items
        void AddNav(string id, string titleKey, string descKey, string tag, params string[] extraKeywords)
        {
            list.Add(new CommandItem
            {
                Id = id,
                Title = mainWindow.Translate(titleKey),
                Description = mainWindow.Translate(descKey),
                Category = CommandCategory.Navigation,
                Keywords = extraKeywords,
                Glyph = "\uE80F",
                ExecuteAsync = () => mainWindow.NavigateToTagAsync(tag)
            });
        }

        AddNav("nav.dashboard", "nav.dashboard", "dashboard.subtitle", "dashboard", "overview", "tổng quan", "health");
        AddNav("nav.cleanup", "nav.cleanup", "oneClick.subtitle", "cleanup", "dọn dẹp", "clean", "rác", "temp", "cache");
        AddNav("nav.optimize", "nav.optimize", "optimize.subtitle", "optimize", "tối ưu", "tweaks", "tinh chỉnh", "windows");
        AddNav("nav.storage", "nav.storage", "storage.subtitle", "storage", "dung lượng", "ổ đĩa", "disk", "space", "drives");
        AddNav("nav.winapp2", "nav.winapp2", "winapp2.subtitle", "winapp2", "ứng dụng", "dọn dẹp app", "app cleaning");
        AddNav("nav.startup", "nav.startup", "startup.subtitle", "startup", "khởi động", "boot", "tự chạy", "programs");
        AddNav("nav.updates", "nav.updates", "updates.subtitle", "updates", "cập nhật", "winget", "upgrade", "software");
        AddNav("nav.software", "nav.software", "software.subtitle", "software", "cài đặt", "install", "packages");
        AddNav("nav.bloatware", "nav.bloatware", "bloatware.subtitle", "bloatware", "gỡ cài đặt", "appx", "windows apps");
        AddNav("nav.toolbox", "nav.toolbox", "toolbox.title", "toolbox", "tiện ích", "công cụ", "network", "registry", "tools");
        AddNav("nav.history", "nav.history", "history.subtitle", "history", "lịch sử", "nhật ký", "reports", "báo cáo");
        AddNav("nav.settings", "nav.settings", "settings.subtitle", "settings", "cài đặt", "giao diện", "ngôn ngữ", "options");

        // 2. System Maintenance & Network Tools
        list.Add(new CommandItem
        {
            Id = "tool.rebuild_icon_cache",
            Title = mainWindow.Translate("systemTools.rebuildIconCache"),
            Description = mainWindow.Translate("systemTools.rebuildIconCacheDesc"),
            Category = CommandCategory.SystemTool,
            Keywords = ["icon", "biểu tượng", "cache", "explorer", "desktop"],
            Glyph = "\uE7B5",
            ExecuteAsync = async () =>
            {
                mainWindow.SetStatusText(mainWindow.Translate("common.loading"), isBusy: true);
                var result = await mainWindow.SystemMaintenance.RebuildIconCacheAsync();
                mainWindow.SetStatusText(result.IsSuccess ? mainWindow.Translate("systemTools.actionSuccess") : result.Detail, isBusy: false);
            }
        });

        list.Add(new CommandItem
        {
            Id = "tool.reset_tcp_ip",
            Title = mainWindow.Translate("systemTools.resetTcpIp"),
            Description = mainWindow.Translate("systemTools.resetTcpIpDesc"),
            Category = CommandCategory.SystemTool,
            Keywords = ["tcp", "ip", "netsh", "reset", "network", "mạng"],
            Glyph = "\uE839",
            ExecuteAsync = async () =>
            {
                mainWindow.SetStatusText(mainWindow.Translate("common.loading"), isBusy: true);
                var result = await mainWindow.SystemMaintenance.ResetTcpIpStackAsync();
                mainWindow.SetStatusText(result.IsSuccess ? mainWindow.Translate("systemTools.actionSuccess") : result.Detail, isBusy: false);
            }
        });

        list.Add(new CommandItem
        {
            Id = "tool.reset_store",
            Title = mainWindow.Translate("systemTools.resetStoreCache"),
            Description = mainWindow.Translate("systemTools.resetStoreCacheDesc"),
            Category = CommandCategory.SystemTool,
            Keywords = ["wsreset", "store", "microsoft store", "app store"],
            Glyph = "\uE719",
            ExecuteAsync = async () =>
            {
                await mainWindow.SystemMaintenance.ResetStoreCacheAsync();
                mainWindow.SetStatusText(mainWindow.Translate("systemTools.actionStarted"), isBusy: false);
            }
        });

        list.Add(new CommandItem
        {
            Id = "tool.clear_arp",
            Title = mainWindow.Translate("systemTools.clearArp"),
            Description = mainWindow.Translate("systemTools.clearArpDesc"),
            Category = CommandCategory.SystemTool,
            Keywords = ["arp", "cache", "routing", "mạng nội bộ"],
            Glyph = "\uE774",
            ExecuteAsync = async () =>
            {
                mainWindow.SetStatusText(mainWindow.Translate("common.loading"), isBusy: true);
                var result = await mainWindow.SystemMaintenance.ClearArpCacheAsync();
                mainWindow.SetStatusText(result.IsSuccess ? mainWindow.Translate("systemTools.actionSuccess") : result.Detail, isBusy: false);
            }
        });

        list.Add(new CommandItem
        {
            Id = "tool.flush_dns",
            Title = mainWindow.Translate("network.flushDns"),
            Description = mainWindow.Translate("network.flushDnsDesc"),
            Category = CommandCategory.SystemTool,
            Keywords = ["dns", "flush", "mạng", "domain"],
            Glyph = "\uE774",
            ExecuteAsync = async () =>
            {
                mainWindow.SetStatusText(mainWindow.Translate("network.runningAction"), isBusy: true);
                var success = await mainWindow.NetworkOptimizer.FlushDnsAsync();
                mainWindow.SetStatusText(success ? mainWindow.Translate("run.completed") : mainWindow.Translate("common.error"), isBusy: false);
            }
        });

        list.Add(new CommandItem
        {
            Id = "tool.create_restore_point",
            Title = mainWindow.Translate("systemTools.createRestorePoint"),
            Description = mainWindow.Translate("systemTools.restorePointDesc"),
            Category = CommandCategory.Maintenance,
            Keywords = ["restore point", "điểm khôi phục", "backup", "system restore"],
            Glyph = "\uE777",
            ExecuteAsync = async () =>
            {
                mainWindow.SetStatusText(mainWindow.Translate("systemTools.creatingRestorePoint"), isBusy: true);
                var (success, msg) = await mainWindow.RestorePoint.CreateManualRestorePointAsync("Manual Point via Command Palette");
                mainWindow.SetStatusText(success ? mainWindow.FormatTranslation("systemTools.restorePointSuccess", msg) : msg, isBusy: false);
            }
        });

        // 3. Built-in Windows Administrative Tools
        void AddAdminTool(string toolKey, string titleKey, string descKey, params string[] extraKeywords)
        {
            list.Add(new CommandItem
            {
                Id = $"admintool.{toolKey}",
                Title = mainWindow.Translate(titleKey),
                Description = mainWindow.Translate(descKey),
                Category = CommandCategory.SystemTool,
                Keywords = extraKeywords,
                Glyph = "\uE7EE",
                ExecuteAsync = () =>
                {
                    SystemMaintenanceService.LaunchAdminTool(toolKey);
                    return Task.CompletedTask;
                }
            });
        }

        AddAdminTool("taskmgr", "systemTools.toolTaskMgr", "systemTools.adminToolsDesc", "task manager", "processes", "tiến trình", "cpu");
        AddAdminTool("devmgmt", "systemTools.toolDevMgmt", "systemTools.adminToolsDesc", "device manager", "drivers", "thiết bị", "phần cứng");
        AddAdminTool("diskmgmt", "systemTools.toolDiskMgmt", "systemTools.adminToolsDesc", "disk management", "partition", "ổ đĩa", "phân vùng");
        AddAdminTool("services", "systemTools.toolServices", "systemTools.adminToolsDesc", "services", "dịch vụ hệ thống", "windows services");
        AddAdminTool("resmon", "systemTools.toolResMon", "systemTools.adminToolsDesc", "resource monitor", "tài nguyên", "bộ nhớ", "ram");
        AddAdminTool("eventvwr", "systemTools.toolEventVwr", "systemTools.adminToolsDesc", "event viewer", "nhật ký sự kiện", "logs");
        AddAdminTool("dxdiag", "systemTools.toolDxDiag", "systemTools.adminToolsDesc", "directx", "diagnostics", "card màn hình", "gpu");
        AddAdminTool("sysdm", "systemTools.toolSysProps", "systemTools.adminToolsDesc", "system properties", "thông tin máy", "cấu hình");

        return list;
    }

    private static Border CreateCommandRow(CommandItem item, MainWindow mainWindow)
    {
        var border = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Background = new SolidColorBrush(Colors.Transparent),
            Margin = new Thickness(0, 2, 0, 2),
            Tag = item
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBlock = new FontIcon
        {
            Glyph = item.Glyph ?? "\uE80F",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = CategoryBrush(item.Category)
        };
        Grid.SetColumn(iconBlock, 0);
        grid.Children.Add(iconBlock);

        var stack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = item.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var desc = new TextBlock
        {
            Text = item.Description,
            FontSize = 12,
            Opacity = 0.65,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(title);
        stack.Children.Add(desc);
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        var badge = CategoryBadge(item.Category, mainWindow);
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        border.Child = grid;
        return border;
    }

    private static void UpdateCommandRow(Border border, CommandItem item, MainWindow mainWindow)
    {
        border.Tag = item;
        if (border.Child is Grid grid && grid.Children.Count >= 3)
        {
            if (grid.Children[0] is FontIcon icon)
            {
                icon.Glyph = item.Glyph ?? "\uE80F";
                icon.Foreground = CategoryBrush(item.Category);
            }

            if (grid.Children[1] is StackPanel stack && stack.Children.Count >= 2)
            {
                if (stack.Children[0] is TextBlock title) title.Text = item.Title;
                if (stack.Children[1] is TextBlock desc) desc.Text = item.Description;
            }

            grid.Children[2] = CategoryBadge(item.Category, mainWindow);
            Grid.SetColumn((FrameworkElement)grid.Children[2], 2);
        }
    }

    private static Border CategoryBadge(CommandCategory category, MainWindow mainWindow)
    {
        var color = category switch
        {
            CommandCategory.Navigation => Colors.DodgerBlue,
            CommandCategory.SystemTool => Colors.DarkOrange,
            CommandCategory.Maintenance => Colors.SeaGreen,
            CommandCategory.Settings => Colors.MediumPurple,
            _ => Colors.Gray
        };

        var labelKey = category switch
        {
            CommandCategory.Navigation => "commandPalette.category.navigation",
            CommandCategory.SystemTool => "commandPalette.category.systemTool",
            CommandCategory.Maintenance => "commandPalette.category.maintenance",
            CommandCategory.Settings => "commandPalette.category.settings",
            _ => "common.open"
        };

        return new Border
        {
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(32, color.R, color.G, color.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = mainWindow.Translate(labelKey),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            }
        };
    }

    private static SolidColorBrush CategoryBrush(CommandCategory category)
    {
        return category switch
        {
            CommandCategory.Navigation => new SolidColorBrush(Colors.DodgerBlue),
            CommandCategory.SystemTool => new SolidColorBrush(Colors.DarkOrange),
            CommandCategory.Maintenance => new SolidColorBrush(Colors.SeaGreen),
            CommandCategory.Settings => new SolidColorBrush(Colors.MediumPurple),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }
}
