using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class RegistryCleanerService
{
    public IpcClient? Client { get; set; }

    public async Task<IReadOnlyList<RegistryIssue>> ScanAsync()
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("ScanRegistry");
            return System.Text.Json.JsonSerializer.Deserialize<List<RegistryIssue>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<RegistryIssue>>(() =>
        {
            var issues = new List<RegistryIssue>();
            ScanSharedDlls(issues);
            ScanAppPaths(issues);
            ScanFileExtensions(issues);
            return issues;
        });
    }

    public async Task<bool> CleanAsync(List<RegistryIssue> issues)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(issues);
            var response = await Client.SendRequestAsync("CleanRegistry", payload);
            return response == "Success";
        }

        return await Task.Run(() => CleanInternal(issues));
    }

    private static void ScanSharedDlls(List<RegistryIssue> issues)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs");
            if (key == null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                try
                {
                    var expanded = Environment.ExpandEnvironmentVariables(name);
                    if (!File.Exists(expanded))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Id = $"SharedDll:{name}",
                            Category = "Shared DLLs",
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",
                            ValueName = name,
                            ValueData = key.GetValue(name)?.ToString() ?? string.Empty,
                            Description = $"Shared DLL file path does not exist: {name}"
                        });
                    }
                }
                catch
                {
                    // Ignore parsing errors for single values
                }
            }
        }
        catch
        {
            // Ignore key-level access errors
        }
    }

    private static void ScanAppPaths(List<RegistryIssue> issues)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
            if (key == null)
            {
                return;
            }

            foreach (var subkeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subkey = key.OpenSubKey(subkeyName);
                    if (subkey == null)
                    {
                        continue;
                    }

                    var defaultVal = subkey.GetValue("")?.ToString();
                    if (string.IsNullOrWhiteSpace(defaultVal))
                    {
                        continue;
                    }

                    var expanded = Environment.ExpandEnvironmentVariables(defaultVal).Trim('"');
                    if (!File.Exists(expanded))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Id = $"AppPath:{subkeyName}",
                            Category = "Application Paths",
                            KeyPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{subkeyName}",
                            ValueName = "",
                            ValueData = defaultVal,
                            Description = $"Executable path for {subkeyName} does not exist: {defaultVal}"
                        });
                    }
                }
                catch
                {
                    // Ignore individual subkey failures
                }
            }
        }
        catch
        {
            // Ignore key-level access errors
        }
    }

    private static void ScanFileExtensions(List<RegistryIssue> issues)
    {
        ScanFileExtensionsForHive(issues, Registry.CurrentUser, @"Software\Classes", "HKEY_CURRENT_USER");
        ScanFileExtensionsForHive(issues, Registry.LocalMachine, @"Software\Classes", "HKEY_LOCAL_MACHINE");
    }

    private static void ScanFileExtensionsForHive(List<RegistryIssue> issues, RegistryKey hive, string path, string hiveName)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key == null)
            {
                return;
            }

            foreach (var subkeyName in key.GetSubKeyNames())
            {
                if (!subkeyName.StartsWith("."))
                {
                    continue;
                }

                try
                {
                    using var subkey = key.OpenSubKey(subkeyName);
                    if (subkey == null)
                    {
                        continue;
                    }

                    var progId = subkey.GetValue("")?.ToString();
                    if (string.IsNullOrWhiteSpace(progId) || progId.Length > 255)
                    {
                        continue;
                    }

                    bool progIdExists = false;
                    using (var cuProgId = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}"))
                    {
                        if (cuProgId != null)
                        {
                            progIdExists = true;
                        }
                    }

                    if (!progIdExists)
                    {
                        using var lmProgId = Registry.LocalMachine.OpenSubKey($@"Software\Classes\{progId}");
                        if (lmProgId != null)
                        {
                            progIdExists = true;
                        }
                    }

                    if (!progIdExists)
                    {
                        issues.Add(new RegistryIssue
                        {
                            Id = $"FileExt:{hiveName}:{subkeyName}",
                            Category = "File Extensions",
                            KeyPath = $@"{hiveName}\{path}\{subkeyName}",
                            ValueName = "",
                            ValueData = progId,
                            Description = $"Extension {subkeyName} references missing ProgID: {progId}"
                        });
                    }
                }
                catch
                {
                    // Ignore individual subkey failures
                }
            }
        }
        catch
        {
            // Ignore key-level access errors
        }
    }

    private bool CleanInternal(List<RegistryIssue> issues)
    {
        var pathService = new PathService();
        var logsDir = pathService.LogsDirectory;
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }

        bool overallSuccess = true;

        foreach (var issue in issues)
        {
            if (!issue.IsSelected)
            {
                continue;
            }

            try
            {
                CreateRegBackup(issue, logsDir);

                var parts = issue.KeyPath.Split('\\');
                if (parts.Length < 2)
                {
                    continue;
                }

                var hiveStr = parts[0];
                var subkeyPath = string.Join('\\', parts.Skip(1));

                var hive = hiveStr switch
                {
                    "HKEY_CURRENT_USER" => Registry.CurrentUser,
                    "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                    _ => null
                };

                if (hive == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(issue.ValueName))
                {
                    if (issue.Category is "Application Paths" or "File Extensions")
                    {
                        var lastBackslash = subkeyPath.LastIndexOf('\\');
                        if (lastBackslash > 0)
                        {
                            var parentPath = subkeyPath.Substring(0, lastBackslash);
                            var subkeyToDelete = subkeyPath.Substring(lastBackslash + 1);
                            using var parentKey = hive.OpenSubKey(parentPath, writable: true);
                            parentKey?.DeleteSubKeyTree(subkeyToDelete, throwOnMissingSubKey: false);
                        }
                    }
                    else
                    {
                        using var key = hive.OpenSubKey(subkeyPath, writable: true);
                        key?.DeleteValue("", throwOnMissingValue: false);
                    }
                }
                else
                {
                    using var key = hive.OpenSubKey(subkeyPath, writable: true);
                    key?.DeleteValue(issue.ValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryCleanerService] Failed to clean issue {issue.Id}: {ex.Message}");
                overallSuccess = false;
            }
        }

        return overallSuccess;
    }

    private static void CreateRegBackup(RegistryIssue issue, string logsDir)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var safeCategory = issue.Category.Replace(" ", "_");
        var filename = $"registry-backup-{safeCategory}-{timestamp}.reg";
        var path = Path.Combine(logsDir, filename);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"[{issue.KeyPath}]");

        if (issue.ValueName == "(Default)" || string.IsNullOrEmpty(issue.ValueName))
        {
            sb.AppendLine($"@={EscapeRegistryValue(issue.ValueData)}");
        }
        else
        {
            sb.AppendLine($"\"{issue.ValueName}\"={EscapeRegistryValue(issue.ValueData)}");
        }

        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.Unicode);
    }

    private static string EscapeRegistryValue(string val)
    {
        return "\"" + val.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
