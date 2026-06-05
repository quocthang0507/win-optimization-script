namespace WinOptimizationApp.Services;

public static class Formatters
{
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:N0} {units[unit]}" : $"{value:N1} {units[unit]}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}d {duration.Hours}h"
            : duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{duration.Minutes}m";
    }
}
