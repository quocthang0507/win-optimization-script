using WinOptimizationApp.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinOptimizationApp.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettingsService(string? customPath = null)
    {
        SettingsPath = customPath ?? Path.Combine(AppRuntimePaths.OriginalBaseDirectory, "settings.json");
    }

    public string SettingsPath { get; }

    public string? RecoveredCorruptSettingsPath { get; private set; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            using var stream = File.OpenRead(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
            return Normalize(settings);
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            RecoveredCorruptSettingsPath = PreserveCorruptSettings();
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            Normalize(settings);
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{SettingsPath}.tmp";
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
            }

            File.Move(tempPath, SettingsPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static IReadOnlyList<int> SupportedRetentionDays { get; } = [0, 30, 90, 180, 365];

    public static bool Export(AppSettings settings, string destinationPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(destinationPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a previously exported settings file. Window geometry is machine-specific and is kept from
    /// <paramref name="current"/> so an import from a different display layout cannot open off-screen.
    /// </summary>
    public static bool TryImport(string sourcePath, AppSettings current, out AppSettings? imported)
    {
        imported = null;
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists || info.Length > 1024 * 1024)
            {
                return false;
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(sourcePath), JsonOptions);
            if (settings is null)
            {
                return false;
            }

            settings.WindowWidth = current.WindowWidth;
            settings.WindowHeight = current.WindowHeight;
            imported = Normalize(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    internal static AppSettings Normalize(AppSettings settings)
    {
        settings.ProtectedPaths = ProtectedPathService.NormalizePaths(settings.ProtectedPaths).ToList();
        settings.ScheduledMaintenanceTaskIds = (settings.ScheduledMaintenanceTaskIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!Enum.IsDefined(settings.ScheduleFrequency))
        {
            settings.ScheduleFrequency = MaintenanceScheduleFrequency.Off;
        }

        if (!Enum.IsDefined(settings.ScheduleDayOfWeek))
        {
            settings.ScheduleDayOfWeek = DayOfWeek.Sunday;
        }

        if (settings.ScheduleTime < TimeSpan.Zero || settings.ScheduleTime >= TimeSpan.FromDays(1))
        {
            settings.ScheduleTime = new TimeSpan(12, 0, 0);
        }

        settings.ScheduleTime = new TimeSpan(settings.ScheduleTime.Hours, settings.ScheduleTime.Minutes, 0);
        if (!SupportedRetentionDays.Contains(settings.ReportRetentionDays))
        {
            settings.ReportRetentionDays = 0;
        }

        return settings;
    }

    private string? PreserveCorruptSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var directory = Path.GetDirectoryName(SettingsPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(SettingsPath);
            var extension = Path.GetExtension(SettingsPath);
            var recoveryPath = Path.Combine(
                directory,
                $"{fileName}.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}{extension}");
            File.Move(SettingsPath, recoveryPath);
            return recoveryPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
