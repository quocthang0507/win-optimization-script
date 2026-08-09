using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class RegistryCleanerService
{
    private readonly CommandRunner _commands;

    public RegistryCleanerService(CommandRunner? commands = null)
    {
        _commands = commands ?? new CommandRunner();
    }

    public IpcClient? Client { get; set; }

    public async Task<IReadOnlyList<RegistryIssue>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("ScanRegistry", cancellationToken: cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<RegistryIssue>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<RegistryIssue>>(() =>
        {
            var issues = new List<RegistryIssue>();
            ScanSharedDlls(issues, cancellationToken);
            ScanAppPaths(issues, cancellationToken);
            ScanFileExtensions(issues, cancellationToken);
            return issues;
        }, cancellationToken);
    }

    public async Task<bool> CleanAsync(List<RegistryIssue> issues, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(issues);
            var response = await Client.SendRequestAsync("CleanRegistry", payload, cancellationToken);
            return response == "Success";
        }

        var selected = issues.Where(issue => issue.IsSelected).ToList();
        var detected = await ScanAsync(cancellationToken);
        var trusted = selected
            .Select(requested => detected.FirstOrDefault(candidate =>
                candidate.Id.Equals(requested.Id, StringComparison.OrdinalIgnoreCase) &&
                candidate.Category.Equals(requested.Category, StringComparison.Ordinal) &&
                candidate.KeyPath.Equals(requested.KeyPath, StringComparison.OrdinalIgnoreCase) &&
                candidate.ValueName.Equals(requested.ValueName, StringComparison.OrdinalIgnoreCase) &&
                candidate.ValueData.Equals(requested.ValueData, StringComparison.Ordinal)))
            .Where(issue => issue is not null)
            .Cast<RegistryIssue>()
            .ToList();
        if (trusted.Count != selected.Count)
        {
            return false;
        }

        foreach (var issue in trusted) issue.IsSelected = true;
        return await CleanInternalAsync(trusted, cancellationToken);
    }

    private static void ScanSharedDlls(List<RegistryIssue> issues, CancellationToken cancellationToken)
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
                cancellationToken.ThrowIfCancellationRequested();
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore parsing errors for single values
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ignore key-level access errors
        }
    }

    private static void ScanAppPaths(List<RegistryIssue> issues, CancellationToken cancellationToken)
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
                cancellationToken.ThrowIfCancellationRequested();
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore individual subkey failures
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ignore key-level access errors
        }
    }

    private static void ScanFileExtensions(List<RegistryIssue> issues, CancellationToken cancellationToken)
    {
        ScanFileExtensionsForHive(issues, Registry.CurrentUser, @"Software\Classes", "HKEY_CURRENT_USER", cancellationToken);
        ScanFileExtensionsForHive(issues, Registry.LocalMachine, @"Software\Classes", "HKEY_LOCAL_MACHINE", cancellationToken);
    }

    private static void ScanFileExtensionsForHive(
        List<RegistryIssue> issues,
        RegistryKey hive,
        string path,
        string hiveName,
        CancellationToken cancellationToken)
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
                cancellationToken.ThrowIfCancellationRequested();
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore individual subkey failures
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ignore key-level access errors
        }
    }

    private async Task<bool> CleanInternalAsync(List<RegistryIssue> issues, CancellationToken cancellationToken)
    {
        var pathService = new PathService();
        var backupsDir = pathService.BackupsDirectory;
        if (!Directory.Exists(backupsDir))
        {
            Directory.CreateDirectory(backupsDir);
        }

        bool overallSuccess = true;

        foreach (var issue in issues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!issue.IsSelected)
            {
                continue;
            }

            try
            {
                var parts = issue.KeyPath.Split('\\');
                if (parts.Length < 2)
                {
                    overallSuccess = false;
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
                    overallSuccess = false;
                    continue;
                }

                var backupPath = await ExportRegistryBackupAsync(issue, backupsDir, cancellationToken);
                if (backupPath is null)
                {
                    Console.WriteLine($"[RegistryCleanerService] Backup failed; issue was not changed: {issue.Id}");
                    overallSuccess = false;
                    continue;
                }

                var changed = false;
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
                            if (parentKey is not null)
                            {
                                parentKey.DeleteSubKeyTree(subkeyToDelete, throwOnMissingSubKey: false);
                                changed = !parentKey.GetSubKeyNames().Contains(subkeyToDelete, StringComparer.OrdinalIgnoreCase);
                            }
                        }
                    }
                    else
                    {
                        using var key = hive.OpenSubKey(subkeyPath, writable: true);
                        if (key is not null)
                        {
                            key.DeleteValue("", throwOnMissingValue: false);
                            changed = !key.GetValueNames().Contains(string.Empty, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                else
                {
                    using var key = hive.OpenSubKey(subkeyPath, writable: true);
                    if (key is not null)
                    {
                        key.DeleteValue(issue.ValueName, throwOnMissingValue: false);
                        changed = !key.GetValueNames().Contains(issue.ValueName, StringComparer.OrdinalIgnoreCase);
                    }
                }

                if (!changed)
                {
                    Console.WriteLine($"[RegistryCleanerService] Registry change could not be verified: {issue.Id}");
                    overallSuccess = false;
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

    private async Task<string?> ExportRegistryBackupAsync(
        RegistryIssue issue,
        string backupsDir,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safeId = string.Concat(issue.Id.Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        var filename = $"registry-backup-{safeId}-{timestamp}.reg";
        var path = Path.Combine(backupsDir, filename);
        var exportKey = issue.KeyPath
            .Replace("HKEY_LOCAL_MACHINE", "HKLM", StringComparison.OrdinalIgnoreCase)
            .Replace("HKEY_CURRENT_USER", "HKCU", StringComparison.OrdinalIgnoreCase);
        var result = await _commands.RunCaptureAsync(
            "reg.exe",
            $"export \"{exportKey}\" \"{path}\" /y",
            cancellationToken);
        return result.ExitCode == 0 && File.Exists(path) ? path : null;
    }
}
