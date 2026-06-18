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
}
