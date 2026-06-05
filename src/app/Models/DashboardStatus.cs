namespace WinOptimizationApp.Models;

public sealed record DashboardStatus(
    string WindowsVersion,
    bool IsAdministrator,
    TimeSpan Uptime,
    string SystemDrive,
    long SystemDriveFreeBytes,
    long SystemDriveTotalBytes,
    bool PendingReboot,
    bool WingetAvailable,
    string? LastReportPath);
