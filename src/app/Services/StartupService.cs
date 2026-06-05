using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class StartupService
{
    public Task<IReadOnlyList<StartupEntry>> ScanAsync()
    {
        return Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            var entries = new List<StartupEntry>();
            ReadRunKey(entries, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", true);
            ReadRunKey(entries, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", true);
            ReadRunKey(entries, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKCU StartupApproved", false);
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "User Startup folder");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup folder");
            return entries;
        });
    }

    private static void ReadRunKey(List<StartupEntry> entries, RegistryKey hive, string path, string source, bool enabled)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name)?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                entries.Add(new StartupEntry(name, source, value, enabled, GetRiskHint(value)));
            }
        }
        catch
        {
            // Read-only inventory should keep going when a registry branch is inaccessible.
        }
    }

    private static void ReadStartupFolder(List<StartupEntry> entries, string folder, string source)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder))
            {
                entries.Add(new StartupEntry(Path.GetFileNameWithoutExtension(file), source, file, true, GetRiskHint(file)));
            }
        }
        catch
        {
            // Ignore inaccessible startup folders in the first GUI pass.
        }
    }

    private static string GetRiskHint(string command)
    {
        return command.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)
            ? "Temp path"
            : command.Contains("AppData", StringComparison.OrdinalIgnoreCase) ? "User profile" : "Standard";
    }
}
