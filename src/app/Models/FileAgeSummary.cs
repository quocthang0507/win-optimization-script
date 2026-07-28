namespace WinOptimizationApp.Models;

public enum FileAgeRange
{
    Last7Days,
    Last30Days,
    LastYear,
    Older,
    Unknown
}

public sealed record FileAgeSummary(
    FileAgeRange Range,
    long TotalBytes,
    int Count);
