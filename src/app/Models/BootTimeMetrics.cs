namespace WinOptimizationApp.Models;

public enum BootSpeedRating
{
    Unknown,
    Fast,
    Normal,
    Slow
}

public sealed record BootEventRecord(
    DateTimeOffset Timestamp,
    double TotalBootSeconds,
    double MainPathSeconds,
    double PostBootSeconds);

public sealed record BootTimeSummary(
    bool IsAvailable,
    BootEventRecord? LastBoot,
    double AverageBootSeconds,
    BootSpeedRating Rating,
    IReadOnlyList<BootEventRecord> RecentBoots);
