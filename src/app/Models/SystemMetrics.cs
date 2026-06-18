namespace WinOptimizationApp.Models;

public sealed record SystemMetrics(
    double CpuUsagePercent,
    double RamUsagePercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long DiskFreeBytes,
    long DiskTotalBytes,
    double DiskUsagePercent,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond
);
