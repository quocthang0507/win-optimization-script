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

    public MaintenanceScheduleFrequency ScheduleFrequency { get; set; } = MaintenanceScheduleFrequency.Off;

    public TimeSpan ScheduleTime { get; set; } = new(12, 0, 0);

    public DayOfWeek ScheduleDayOfWeek { get; set; } = DayOfWeek.Sunday;

    public List<string> ScheduledMaintenanceTaskIds { get; set; } = ["cleanup.temp"];

    /// <summary>Maintenance reports older than this many days are removed; 0 keeps reports forever.</summary>
    public int ReportRetentionDays { get; set; }
}
