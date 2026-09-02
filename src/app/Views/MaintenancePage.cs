using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Views;

public sealed partial class MaintenancePage : BasePage
{
    private readonly bool _includeWinapp2;
    private readonly bool _includeOneClick;
    private readonly Dictionary<string, CheckBox> _oneClickSelections = new(StringComparer.OrdinalIgnoreCase);
    private Button? _oneClickRunButton;
    private Button? _oneClickStopButton;
    private ProgressBar? _oneClickProgress;
    private TextBlock? _oneClickStatus;
    private CancellationTokenSource? _oneClickCancellation;
    private bool _oneClickRunning;
    private UIElement? _winapp2Panel;
    private int _appliedWinapp2Revision = -1;

    public MaintenancePage(
        MainWindow mainWindow,
        string title,
        string group,
        bool includePrivacy = false,
        bool includeOptimization = false) : base(mainWindow)
    {
        _includeWinapp2 = includePrivacy;
        _includeOneClick = group.Equals("Cleanup", StringComparison.OrdinalIgnoreCase);
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

        if (_includeOneClick)
        {
            MainContent.Children.Add(OneClickPanel());
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

    public override async Task OnNavigatedToAsync()
    {
        if (!_includeWinapp2)
        {
            return;
        }

        if (!MainWindow.SessionState.Winapp2Loaded)
        {
            await MainWindow.RefreshWinapp2StateAsync();
        }

        if (_appliedWinapp2Revision == MainWindow.SessionState.Winapp2Revision)
        {
            return;
        }

        if (_winapp2Panel != null)
        {
            MainContent.Children.Remove(_winapp2Panel);
        }

        _winapp2Panel = MainWindow.SessionState.Winapp2Entries.Count > 0
            ? Winapp2CleanerPanel(MainWindow.SessionState.Winapp2Entries)
            : !string.IsNullOrWhiteSpace(MainWindow.SessionState.Winapp2Error)
                ? InfoBlock(MainWindow.SessionState.Winapp2Error!)
                : null;
        if (_winapp2Panel != null)
        {
            MainContent.Children.Add(_winapp2Panel);
        }

        _appliedWinapp2Revision = MainWindow.SessionState.Winapp2Revision;
    }

    private void AddTaskRow(MaintenanceTask task)
    {
        var row = new Border
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
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
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

    private Border Winapp2CleanerPanel(IReadOnlyList<CleanerEntry> entries)
    {
        var customDatabase = IsCustomWinapp2DatabaseActive();
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(SectionTitle(T("winapp2.title")));
        stack.Children.Add(InfoBlock(F("winapp2.detected", entries.Count)));
        stack.Children.Add(InfoBlock(T("winapp2.selectionSafety")));

        var row = new Grid { ColumnSpacing = 12, MinHeight = 44 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Dropdown with Checkboxes (ListView inside Flyout)
        var dropDownBtn = new DropDownButton
        {
            Content = F("winapp2.selectApps", entries.Count(e => e.CanSelectByDefault(customDatabase))),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 400,
            Width = 350
        };

        foreach (var entry in entries)
        {
            var details = new StackPanel { Spacing = 4 };
            details.Children.Add(new TextBlock { Text = entry.Name, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(entry.Warning))
                details.Children.Add(new TextBlock
                {
                    Text = F("winapp2.entryWarning", entry.Warning),
                    TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8
                });
            var item = new ListViewItem
            {
                Content = details,
                Tag = entry,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsSelected = entry.CanSelectByDefault(customDatabase)
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, entry.Name);
            listView.Items.Add(item);
        }

        var runBtn = new Button { Content = T("common.scan"), MinWidth = 82, IsEnabled = listView.SelectedItems.Count > 0 };

        listView.SelectionChanged += (s, e) =>
        {
            dropDownBtn.Content = F("winapp2.selectApps", listView.SelectedItems.Count);
            runBtn.IsEnabled = listView.SelectedItems.Count > 0;
        };

        var search = new TextBox { PlaceholderText = T("winapp2.search"), Width = 350 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, T("winapp2.search"));
        search.TextChanged += (_, _) => DebounceUiAction("winapp2-search", () =>
        {
            var query = search.Text.Trim();
            foreach (ListViewItem item in listView.Items)
            {
                var entry = (CleanerEntry)item.Tag;
                item.Visibility = entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                  entry.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        });
        var pickerContent = new StackPanel { Spacing = 10 };
        pickerContent.Children.Add(search);
        pickerContent.Children.Add(listView);
        var flyout = new Flyout
        {
            Content = pickerContent,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        };
        dropDownBtn.Flyout = flyout;
        Grid.SetColumn(dropDownBtn, 0);
        row.Children.Add(dropDownBtn);

        runBtn.Click += async (_, _) =>
        {
            runBtn.IsEnabled = false;
            dropDownBtn.IsEnabled = false;
            try
            {
                var selected = listView.SelectedItems.Select(i => (CleanerEntry)((ListViewItem)i).Tag).ToList();
                await PreviewAndRunWinapp2CleanupAsync(selected);
            }
            catch (Exception ex)
            {
                MainWindow.SetStatusText(F("winapp2.operationFailed", ex.Message));
            }
            finally
            {
                dropDownBtn.IsEnabled = true;
                runBtn.IsEnabled = listView.SelectedItems.Count > 0;
            }
        };
        Grid.SetColumn(runBtn, 1);
        row.Children.Add(runBtn);

        stack.Children.Add(row);

        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = stack,
            Margin = new Thickness(0, 16, 0, 0)
        };
    }

    private async Task PreviewAndRunWinapp2CleanupAsync(List<CleanerEntry> selectedEntries)
    {
        var customDatabase = IsCustomWinapp2DatabaseActive();
        MainWindow.SetStatusText(T("winapp2.scanning"));
        var preview = await MainWindow.Winapp2Cleanup.PreviewAsync(
            selectedEntries,
            MainWindow.Settings.ProtectedPaths,
            restrictCustomDatabase: customDatabase);
        if (preview.Candidates.Count == 0)
        {
            MainWindow.SetStatusText(T("common.ready"));
            await MainWindow.ShowDialogAsync_Internal(T("winapp2.title"), InfoBlock(T("winapp2.nothingFound")), T("common.close"));
            return;
        }

        var confirmation = new StackPanel { Spacing = 10 };
        confirmation.Children.Add(new TextBlock
        {
            Text = F("winapp2.confirmBody", preview.Candidates.Count, Formatters.FormatBytes(preview.TotalBytes), selectedEntries.Count),
            TextWrapping = TextWrapping.Wrap
        });
        if (customDatabase)
        {
            confirmation.Children.Add(InfoBlock(T("winapp2.customDatabaseWarning")));
        }

        confirmation.Children.Add(new TextBlock
        {
            Text = T("winapp2.targetPaths"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var targetText = string.Join(Environment.NewLine, preview.Candidates.Take(25).Select(candidate => candidate.Path));
        if (preview.Candidates.Count > 25)
        {
            targetText += Environment.NewLine + F("winapp2.moreTargets", preview.Candidates.Count - 25);
        }
        confirmation.Children.Add(new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = targetText,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            }
        });
        if (preview.Warnings.Count > 0)
        {
            confirmation.Children.Add(InfoBlock(F("winapp2.reviewWarnings", preview.Warnings.Count)));
            confirmation.Children.Add(new ScrollViewer
            {
                MaxHeight = 160,
                Content = new TextBlock { Text = string.Join(Environment.NewLine, preview.Warnings), TextWrapping = TextWrapping.Wrap }
            });
        }

        var dialog = new ContentDialog
        {
            Title = T("winapp2.confirmTitle"),
            Content = confirmation,
            PrimaryButtonText = T("common.delete"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            MainWindow.SetStatusText(T("common.ready"));
            return;
        }

        MainWindow.SetStatusText(T("winapp2.cleaning"));
        var result = await MainWindow.Winapp2Cleanup.RunAsync(
            preview,
            selectedEntries.Count,
            MainWindow.Settings.ProtectedPaths,
            restrictCustomDatabase: customDatabase);
        MainWindow.SetStatusText(T("common.ready"));
        await MainWindow.ShowRunResultAsync_Internal(result);
    }

    private Border OneClickPanel()
    {
        var stack = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        stack.Children.Add(new TextBlock
        {
            Text = T("oneClick.title"),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(InfoBlock(T("oneClick.description")));

        var cleanupItems = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var systemItems = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var performanceItems = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var systemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cleanup.shaders", "cleanup.errorreports", "cleanup.prefetch", "cleanup.defenderlogs",
            "cleanup.systemdumps", "cleanup.windowsupdate"
        };

        foreach (var (definition, task) in MainWindow.OneClickMaintenance.GetItems())
        {
            var checkBox = OneClickOption(task, definition.DefaultSelected);
            _oneClickSelections[task.Id] = checkBox;
            if (definition.IsPerformanceAction)
            {
                performanceItems.Children.Add(checkBox);
            }
            else if (systemIds.Contains(task.Id))
            {
                systemItems.Children.Add(checkBox);
            }
            else
            {
                cleanupItems.Children.Add(checkBox);
            }
        }

        var groups = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        groups.Children.Add(OneClickGroup(T("oneClick.cleanupGroup"), Symbol.Delete, cleanupItems, expanded: true));
        groups.Children.Add(OneClickGroup(T("oneClick.systemGroup"), Symbol.Setting, systemItems, expanded: false));
        groups.Children.Add(OneClickGroup(T("oneClick.performanceGroup"), Symbol.Repair, performanceItems, expanded: true));
        stack.Children.Add(groups);

        var footer = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        _oneClickProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 4,
            Visibility = Visibility.Collapsed
        };
        _oneClickStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        footer.Children.Add(_oneClickProgress);
        footer.Children.Add(_oneClickStatus);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _oneClickRunButton = ActionButton(T("oneClick.analyze"), Symbol.Play, async (_, _) => await RunOneClickAsync());
        _oneClickRunButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        _oneClickStopButton = ActionButton(T("common.stop"), Symbol.Stop, (_, _) => _oneClickCancellation?.Cancel());
        _oneClickStopButton.IsEnabled = false;
        actions.Children.Add(_oneClickRunButton);
        actions.Children.Add(_oneClickStopButton);
        footer.Children.Add(actions);
        stack.Children.Add(new Border
        {
            Padding = new Thickness(0, 16, 0, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = ThemeBorderBrush(),
            Child = footer
        });

        return new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBorderBrush(),
            Background = ThemeCardBackground(),
            Child = stack
        };
    }

    private CheckBox OneClickOption(MaintenanceTask task, bool selectedByDefault)
    {
        var content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(new TextBlock
        {
            Text = MainWindow.TaskLabel_Internal(task),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = MainWindow.TaskImpact_Internal(task),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        });
        var checkBox = new CheckBox
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 10, 12, 10),
            MinHeight = 64,
            IsChecked = selectedByDefault,
            IsEnabled = !task.RequiresAdmin || SystemStatusService.IsAdministrator()
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(checkBox, MainWindow.TaskLabel_Internal(task));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(checkBox, MainWindow.TaskImpact_Internal(task));
        if (!checkBox.IsEnabled)
        {
            ToolTipService.SetToolTip(checkBox, T("oneClick.adminRequired"));
        }
        else if (task.RiskLevel == RiskLevel.High)
        {
            ToolTipService.SetToolTip(checkBox, T("oneClick.highRiskWarning"));
        }
        return checkBox;
    }

    private static Expander OneClickGroup(string title, Symbol symbol, StackPanel content, bool expanded)
    {
        var header = new Grid { ColumnSpacing = 12, MinHeight = 28 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new SymbolIcon(symbol) { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center });
        var label = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        header.Children.Add(label);
        var count = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(10),
            Background = ThemeBorderBrush(),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = content.Children.Count.ToString(), FontSize = 12 }
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(count, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        Grid.SetColumn(count, 2);
        header.Children.Add(count);

        var expander = new Expander
        {
            Header = header,
            Content = new Border { Padding = new Thickness(8, 4, 8, 4), Child = content },
            IsExpanded = expanded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(expander, title);
        return expander;
    }

    private async Task RunOneClickAsync()
    {
        if (_oneClickRunning || _oneClickRunButton == null || _oneClickStopButton == null ||
            _oneClickProgress == null || _oneClickStatus == null)
        {
            return;
        }

        var selectedIds = _oneClickSelections
            .Where(pair => pair.Value.IsEnabled && pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .ToList();
        if (selectedIds.Count == 0)
        {
            await MainWindow.ShowDialogAsync_Internal(
                T("oneClick.title"),
                InfoBlock(T("oneClick.selectAtLeastOne")),
                T("common.close"));
            return;
        }

        _oneClickRunning = true;
        _oneClickCancellation?.Dispose();
        _oneClickCancellation = new CancellationTokenSource();
        _oneClickRunButton.IsEnabled = false;
        _oneClickStopButton.IsEnabled = true;
        _oneClickProgress.Visibility = Visibility.Visible;
        SetOneClickSelectionsEnabled(false);

        var progress = new Progress<OneClickProgress>(value =>
        {
            _oneClickProgress.Maximum = Math.Max(1, value.Total);
            _oneClickProgress.Value = value.Current;
            var task = MainWindow.Catalog.GetById(value.TaskId);
            _oneClickStatus.Text = F(
                value.IsRunning ? "oneClick.runningProgress" : "oneClick.scanningProgress",
                value.Current,
                value.Total,
                MainWindow.TaskLabel_Internal(task));
        });

        try
        {
            var preview = await MainWindow.OneClickMaintenance.PreviewAsync(
                selectedIds,
                MainWindow.Settings.ProtectedPaths,
                progress,
                _oneClickCancellation.Token);
            if (!await ConfirmOneClickAsync(preview))
            {
                return;
            }

            var summary = await MainWindow.OneClickMaintenance.RunAsync(
                preview,
                MainWindow.Settings.ProtectedPaths,
                progress,
                _oneClickCancellation.Token);
            var aggregate = BuildOneClickReport(summary);
            await MainWindow.SaveOperationReportAsync(aggregate);
            if (!summary.Cancelled)
            {
                await MainWindow.RefreshHealthMetricsStateAsync(_oneClickCancellation.Token);
            }
            await MainWindow.ShowRunResultAsync_Internal(aggregate);
        }
        catch (OperationCanceledException)
        {
            _oneClickStatus.Text = T("common.cancelled");
        }
        finally
        {
            _oneClickRunning = false;
            _oneClickRunButton.IsEnabled = true;
            _oneClickStopButton.IsEnabled = false;
            SetOneClickSelectionsEnabled(true);
            _oneClickCancellation?.Dispose();
            _oneClickCancellation = null;
            MainWindow.SetStatusText(T("common.ready"));
        }
    }

    private async Task<bool> ConfirmOneClickAsync(OneClickPreview preview)
    {
        var panel = new StackPanel { Spacing = 10, MaxWidth = 720 };
        panel.Children.Add(new TextBlock
        {
            Text = F("oneClick.previewSummary", preview.EstimatedFileCount, Formatters.FormatBytes(preview.EstimatedBytes), preview.Tasks.Count),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var details = new StackPanel { Spacing = 8 };
        foreach (var item in preview.Tasks)
        {
            details.Children.Add(new TextBlock
            {
                Text = item.IsPerformanceAction
                    ? F("oneClick.performanceLine", MainWindow.TaskLabel_Internal(item.Task))
                    : F("oneClick.cleanupLine", MainWindow.TaskLabel_Internal(item.Task), Formatters.FormatBytes(item.Preview.EstimatedBytes), item.Preview.EstimatedFileCount),
                Foreground = item.Task.RiskLevel == RiskLevel.High ? Brush(Colors.IndianRed) : null,
                TextWrapping = TextWrapping.Wrap
            });
            foreach (var warning in item.Preview.Warnings.Take(3))
            {
                details.Children.Add(new TextBlock
                {
                    Text = $"  • {warning}",
                    Foreground = Brush(Colors.DarkOrange),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = details,
            MaxHeight = 340,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        var dialog = new ContentDialog
        {
            Title = T("oneClick.confirmTitle"),
            Content = panel,
            PrimaryButtonText = T("oneClick.cleanAndBoost"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Navigation_Internal.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private TaskRunResult BuildOneClickReport(OneClickRunSummary summary)
    {
        var now = DateTimeOffset.Now;
        var messages = summary.Results
            .Select(result => $"{MainWindow.TaskLabel_Internal(MainWindow.Catalog.GetById(result.TaskId))}: {(result.Success ? "OK" : "Failed")}")
            .ToList();
        if (summary.Cancelled)
        {
            messages.Add("One-click maintenance was cancelled before all actions completed.");
        }
        var errors = summary.Results
            .SelectMany(result => result.Errors.Select(error => $"{result.TaskId}: {error}"))
            .ToList();
        return new TaskRunResult(
            "maintenance.oneclick",
            "1-click cleanup and boost",
            summary.Results.Count > 0 ? summary.Results.Min(result => result.StartedAt) : now,
            now,
            !summary.Cancelled && summary.Results.All(result => result.Success),
            summary.FreedBytes,
            summary.FilesRemoved,
            summary.FilesSkipped,
            messages,
            errors);
    }

    private void SetOneClickSelectionsEnabled(bool enabled)
    {
        foreach (var (taskId, checkBox) in _oneClickSelections)
        {
            var task = MainWindow.Catalog.GetById(taskId);
            checkBox.IsEnabled = enabled && (!task.RequiresAdmin || SystemStatusService.IsAdministrator());
        }
    }

    private bool IsCustomWinapp2DatabaseActive()
    {
        var path = MainWindow.Settings.CustomWinapp2DatabasePath;
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
