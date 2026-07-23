using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class StartupService
{
    public IpcClient? Client { get; set; }

    public async Task<IReadOnlyList<StartupEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("ScanStartup", cancellationToken: cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<StartupEntry>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = new List<StartupEntry>();
            ReadRunKey(entries, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run");
            ReadRunKey(entries, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "User Startup folder");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup folder");
            return entries;
        }, cancellationToken);
    }

    public async Task<bool> EnableAsync(StartupEntry entry)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(entry);
            var response = await Client.SendRequestAsync("EnableStartup", payload);
            return response == "Success";
        }

        var trustedEntry = await ResolveTrustedEntryAsync(entry);
        return trustedEntry is not null && await Task.Run(() => EnableInternal(trustedEntry));
    }

    public async Task<bool> DisableAsync(StartupEntry entry)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(entry);
            var response = await Client.SendRequestAsync("DisableStartup", payload);
            return response == "Success";
        }

        var trustedEntry = await ResolveTrustedEntryAsync(entry);
        return trustedEntry is not null && await Task.Run(() => DisableInternal(trustedEntry));
    }

    private async Task<StartupEntry?> ResolveTrustedEntryAsync(StartupEntry requested)
    {
        var entries = await ScanAsync();
        return entries.FirstOrDefault(candidate =>
            candidate.Name.Equals(requested.Name, StringComparison.OrdinalIgnoreCase) &&
            candidate.Source.Equals(requested.Source, StringComparison.Ordinal) &&
            candidate.Command.Equals(requested.Command, StringComparison.OrdinalIgnoreCase));
    }

    private bool EnableInternal(StartupEntry entry)
    {
        try
        {
            if (entry.Source == "HKCU Run")
            {
                return SetStartupApprovedStatus(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entry.Name, true);
            }
            else if (entry.Source == "HKLM Run")
            {
                var ok1 = SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entry.Name, true);
                var ok2 = SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", entry.Name, true);
                return ok1 || ok2;
            }
            else if (entry.Source == "User Startup folder")
            {
                var fileName = Path.GetFileName(entry.Command);
                return SetStartupApprovedStatus(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", fileName, true);
            }
            else if (entry.Source == "Common Startup folder")
            {
                var fileName = Path.GetFileName(entry.Command);
                return SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", fileName, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Enable failed: {ex.Message}");
        }
        return false;
    }

    private bool DisableInternal(StartupEntry entry)
    {
        try
        {
            CreateBackup(entry);

            if (entry.Source == "HKCU Run")
            {
                return SetStartupApprovedStatus(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entry.Name, false);
            }
            else if (entry.Source == "HKLM Run")
            {
                var ok1 = SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entry.Name, false);
                SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", entry.Name, false);
                return ok1;
            }
            else if (entry.Source == "User Startup folder")
            {
                var fileName = Path.GetFileName(entry.Command);
                return SetStartupApprovedStatus(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", fileName, false);
            }
            else if (entry.Source == "Common Startup folder")
            {
                var fileName = Path.GetFileName(entry.Command);
                return SetStartupApprovedStatus(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", fileName, false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Disable failed: {ex.Message}");
        }
        return false;
    }

    private static bool SetStartupApprovedStatus(RegistryKey hive, string subkeyPath, string valueName, bool enabled)
    {
        try
        {
            using var key = hive.CreateSubKey(subkeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (key == null)
            {
                return false;
            }

            if (enabled)
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
            else
            {
                var existing = key.GetValue(valueName);
                byte[] data;
                if (existing is byte[] bytes && bytes.Length > 0)
                {
                    data = (byte[])bytes.Clone();
                    data[0] = 3;
                }
                else
                {
                    data = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                }
                key.SetValue(valueName, data, RegistryValueKind.Binary);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CreateBackup(StartupEntry entry)
    {
        try
        {
            var pathService = new PathService();
            var backupsDir = pathService.BackupsDirectory;
            if (!Directory.Exists(backupsDir))
            {
                Directory.CreateDirectory(backupsDir);
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var filename = $"startup-backup-{entry.Name.Replace(" ", "_")}-{timestamp}.json";
            var backupPath = Path.Combine(backupsDir, filename);

            var backupContent = System.Text.Json.JsonSerializer.Serialize(entry, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(backupPath, backupContent, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Failed to create backup: {ex.Message}");
        }
    }

    private static bool IsEntryEnabled(string source, string name, string command)
    {
        try
        {
            if (source == "HKCU Run")
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                if (key != null)
                {
                    var val = key.GetValue(name);
                    if (val is byte[] bytes)
                    {
                        return IsBytesEnabled(bytes);
                    }
                }
            }
            else if (source == "HKLM Run")
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                if (key != null)
                {
                    var val = key.GetValue(name);
                    if (val is byte[] bytes)
                    {
                        return IsBytesEnabled(bytes);
                    }
                }
                using var key32 = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32");
                if (key32 != null)
                {
                    var val = key32.GetValue(name);
                    if (val is byte[] bytes)
                    {
                        return IsBytesEnabled(bytes);
                    }
                }
            }
            else if (source == "User Startup folder")
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder");
                if (key != null)
                {
                    var fileName = Path.GetFileName(command);
                    var val = key.GetValue(fileName);
                    if (val is byte[] bytes)
                    {
                        return IsBytesEnabled(bytes);
                    }
                }
            }
            else if (source == "Common Startup folder")
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder");
                if (key != null)
                {
                    var fileName = Path.GetFileName(command);
                    var val = key.GetValue(fileName);
                    if (val is byte[] bytes)
                    {
                        return IsBytesEnabled(bytes);
                    }
                }
            }
        }
        catch
        {
            // Fallback to true if we cannot read registry
        }
        return true;
    }

    private static bool IsBytesEnabled(byte[] bytes)
    {
        return bytes == null || bytes.Length == 0 || (bytes[0] & 1) == 0;
    }

    private static void ReadRunKey(List<StartupEntry> entries, RegistryKey hive, string path, string source)
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

                var enabled = IsEntryEnabled(source, name, value);
                entries.Add(CreateEntry(name, source, value, enabled));
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
                var name = Path.GetFileNameWithoutExtension(file);
                var enabled = IsEntryEnabled(source, name, file);
                entries.Add(CreateEntry(name, source, file, enabled));
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

    private static StartupEntry CreateEntry(string name, string source, string command, bool enabled)
    {
        var analysis = StartupImpactClassifier.Analyze(name, source, command, enabled);
        return new StartupEntry(
            name,
            source,
            command,
            enabled,
            GetRiskHint(command),
            analysis.Impact,
            analysis.Recommendation,
            analysis.CanDisable,
            analysis.CanRollback);
    }
}
