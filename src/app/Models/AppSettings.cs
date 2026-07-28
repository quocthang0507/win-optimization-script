namespace WinOptimizationApp.Models;

public sealed class AppSettings
{
    public AppLanguage? Language { get; set; }

    public AppTheme? Theme { get; set; }

    public AppWinUiStyle? WinUiStyle { get; set; }

    public bool WidgetEnabled { get; set; }

    public bool WidgetShowCpu { get; set; } = true;

    public bool WidgetShowRam { get; set; } = true;

    public bool WidgetShowDisk { get; set; } = true;

    public bool WidgetShowNetwork { get; set; } = true;

    public int WindowWidth { get; set; } = 1180;

    public int WindowHeight { get; set; } = 760;

    public bool IsNavigationPaneOpen { get; set; } = true;

    public List<string> ProtectedPaths { get; set; } = [];

    public string? CustomWinapp2DatabasePath { get; set; }
}
