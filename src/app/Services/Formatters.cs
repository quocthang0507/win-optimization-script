using System;
using WinOptimizationApp.Models;

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

    public static string FormatDuration(TimeSpan duration, AppLanguage language)
    {
        var isVi = language == AppLanguage.Vietnamese;
        var days = (int)duration.TotalDays;
        var hours = duration.Hours;
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        if (duration.TotalDays >= 1)
        {
            if (isVi)
            {
                return $"{days} ngày {hours} giờ";
            }
            else
            {
                var dayStr = days == 1 ? "day" : "days";
                var hourStr = hours == 1 ? "hour" : "hours";
                return $"{days} {dayStr} {hours} {hourStr}";
            }
        }
        else if (duration.TotalHours >= 1)
        {
            if (isVi)
            {
                return $"{totalHours} giờ {minutes} phút";
            }
            else
            {
                var hourStr = totalHours == 1 ? "hour" : "hours";
                var minStr = minutes == 1 ? "minute" : "minutes";
                return $"{totalHours} {hourStr} {minutes} {minStr}";
            }
        }
        else
        {
            if (isVi)
            {
                return $"{minutes} phút";
            }
            else
            {
                var minStr = minutes == 1 ? "minute" : "minutes";
                return $"{minutes} {minStr}";
            }
        }
    }
}
