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
            return JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
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
