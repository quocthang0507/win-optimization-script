using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _english = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _vietnamese = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService(AppLanguage? savedLanguage = null)
    {
        CurrentLanguage = savedLanguage ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Vietnamese
            : AppLanguage.English);

        LoadLanguageFile("en.json", _english);
        LoadLanguageFile("vi.json", _vietnamese);
    }

    private void LoadLanguageFile(string fileName, Dictionary<string, string> targetDict)
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "Assets", "Langs", fileName);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        targetDict[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalizationService] Failed to load {fileName}: {ex.Message}");
        }
    }

    public AppLanguage CurrentLanguage { get; set; }

    public string Get(string key)
    {
        var selected = CurrentLanguage == AppLanguage.Vietnamese ? _vietnamese : _english;
        return selected.TryGetValue(key, out var value) ? value : _english.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(GetCulture(), Get(key), args);
    }

    public string TaskLabel(string taskId, string fallback)
    {
        var key = $"task.{taskId}.label";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string TaskDescription(string taskId, string fallback)
    {
        var key = $"task.{taskId}.description";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string TaskImpact(string taskId, string fallback)
    {
        var key = $"task.{taskId}.impact";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string GroupName(string group)
    {
        var key = $"group.{group}";
        var value = Get(key);
        return value == key ? group : value;
    }

    public string RiskName(RiskLevel risk)
    {
        return Get($"risk.{risk}");
    }

    private CultureInfo GetCulture()
    {
        return CurrentLanguage == AppLanguage.Vietnamese
            ? CultureInfo.GetCultureInfo("vi-VN")
            : CultureInfo.GetCultureInfo("en-US");
    }
}
