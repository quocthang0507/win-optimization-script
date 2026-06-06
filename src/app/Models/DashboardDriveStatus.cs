namespace WinOptimizationApp.Models;

public sealed record DashboardDriveStatus(
    string Name,
    string DriveType,
    string Format,
    string Label,
    long TotalBytes,
    long FreeBytes);
