using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class UninstallerService
{
    private readonly CommandRunner _commands;

    public IpcClient? Client { get; set; }

    public UninstallerService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<IReadOnlyList<InstalledApp>> ScanInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("ScanInstalledApps");
            return System.Text.Json.JsonSerializer.Deserialize<List<InstalledApp>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<InstalledApp>>(async () =>
        {
            var apps = new List<InstalledApp>();

            ScanRegistryUninstall(apps, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM");
            if (Environment.Is64BitOperatingSystem)
            {
                ScanRegistryUninstall(apps, Registry.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM32");
            }
            ScanRegistryUninstall(apps, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU");

            try
            {
                if (_commands.Exists("winget"))
                {
                    var result = await _commands.RunCaptureAsync("winget.exe", "list --accept-source-agreements", cancellationToken);
                    if (result.ExitCode == 0)
                    {
                        ParseWingetList(result.StandardOutput, apps);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UninstallerService] Winget scan failed: {ex.Message}");
            }

            return apps.OrderBy(a => a.Name).ToList();
        }, cancellationToken);
    }

    public async Task<bool> UninstallAppAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(app);
            var response = await Client.SendRequestAsync("UninstallApp", payload);
            return response == "Success";
        }

        if (app.Source == "Winget")
        {
            var id = app.Id.Split('\\').Last();
            var result = await _commands.RunCaptureAsync(
                "winget.exe",
                $"uninstall --id {QuoteArgument(id)} --exact --silent --accept-source-agreements --disable-interactivity",
                cancellationToken);
            return result.ExitCode == 0;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(app.UninstallString))
            {
                return false;
            }

            var (exe, args) = ParseUninstallString(app.UninstallString);
            var result = await _commands.RunCaptureAsync(exe, args, cancellationToken);
            return result.ExitCode == 0;
        }
    }

    public async Task<IReadOnlyList<string>> ScanLeftoversAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(app);
            var response = await Client.SendRequestAsync("ScanLeftovers", payload);
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<string>>(() =>
        {
            var leftovers = new List<string>();
            var searchNames = new List<string>();

            var cleanAppName = CleanAppNameForSearch(app.Name);
            if (cleanAppName.Length >= 3)
            {
                searchNames.Add(cleanAppName);
            }

            var cleanPublisher = CleanPublisherForSearch(app.Publisher);
            if (cleanPublisher.Length >= 3 && cleanPublisher != "unknown")
            {
                searchNames.Add(cleanPublisher);
            }

            var roots = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    var subdirs = Directory.GetDirectories(root);
                    foreach (var subdir in subdirs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var dirName = Path.GetFileName(subdir);
                        bool matched = false;
                        foreach (var name in searchNames)
                        {
                            if (dirName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                (dirName.Contains(name, StringComparison.OrdinalIgnoreCase) && name.Length >= 5))
                            {
                                matched = true;
                                break;
                            }
                        }

                        if (matched)
                        {
                            if (dirName.Equals(cleanPublisher, StringComparison.OrdinalIgnoreCase) && !dirName.Equals(cleanAppName, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    var publisherSubdirs = Directory.GetDirectories(subdir);
                                    foreach (var pubSub in publisherSubdirs)
                                    {
                                        var pubSubName = Path.GetFileName(pubSub);
                                        if (pubSubName.Equals(cleanAppName, StringComparison.OrdinalIgnoreCase) ||
                                            (pubSubName.Contains(cleanAppName, StringComparison.OrdinalIgnoreCase) && cleanAppName.Length >= 5))
                                        {
                                            leftovers.Add(pubSub);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore access errors on subdirectories
                                }
                            }
                            else
                            {
                                leftovers.Add(subdir);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore directory list errors
                }
            }

            if (!string.IsNullOrEmpty(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            {
                if (!leftovers.Contains(app.InstallLocation))
                {
                    leftovers.Add(app.InstallLocation);
                }
            }

            return leftovers.Distinct().ToList();
        }, cancellationToken);
    }

    public async Task<bool> DeleteLeftoversAsync(List<string> paths, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(paths);
            var response = await Client.SendRequestAsync("CleanLeftovers", payload);
            return response == "Success";
        }

        return await Task.Run(() =>
        {
            bool overallSuccess = true;
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UninstallerService] Failed to delete leftover: {path}. Error: {ex.Message}");
                    overallSuccess = false;
                }
            }
            return overallSuccess;
        }, cancellationToken);
    }

    private static void ScanRegistryUninstall(List<InstalledApp> apps, RegistryKey hive, string path, string sourceName)
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
                try
                {
                    using var subkey = key.OpenSubKey(subkeyName);
                    if (subkey == null)
                    {
                        continue;
                    }

                    var displayName = subkey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    var uninstallString = subkey.GetValue("UninstallString")?.ToString();
                    if (string.IsNullOrWhiteSpace(uninstallString))
                    {
                        continue;
                    }

                    var displayVersion = subkey.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                    var publisher = subkey.GetValue("Publisher")?.ToString() ?? "Unknown";
                    var installLocation = subkey.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                    var quietUninstall = subkey.GetValue("QuietUninstallString")?.ToString() ?? string.Empty;

                    if (apps.Any(a => a.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    apps.Add(new InstalledApp
                    {
                        Id = $"{sourceName}\\{subkeyName}",
                        Name = displayName,
                        Version = displayVersion,
                        Publisher = publisher,
                        UninstallString = !string.IsNullOrEmpty(quietUninstall) ? quietUninstall : uninstallString,
                        InstallLocation = installLocation,
                        Source = "Registry"
                    });
                }
                catch
                {
                    // Ignore subkey failures
                }
            }
        }
        catch
        {
            // Ignore key access failures
        }
    }

    private static void ParseWingetList(string output, List<InstalledApp> apps)
    {
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3)
        {
            return;
        }

        int separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("---"))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex <= 0)
        {
            return;
        }

        var header = lines[separatorIndex - 1];
        var idIndex = header.IndexOf("Id", StringComparison.Ordinal);
        var versionIndex = header.IndexOf("Version", StringComparison.Ordinal);
        var sourceIndex = header.IndexOf("Source", StringComparison.Ordinal);

        if (idIndex < 0 || versionIndex < 0)
        {
            return;
        }

        foreach (var line in lines.Skip(separatorIndex + 1))
        {
            if (line.Length <= idIndex)
            {
                continue;
            }

            var name = line.Substring(0, idIndex).Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            string id = line.Length > versionIndex ? line.Substring(idIndex, versionIndex - idIndex).Trim() : line.Substring(idIndex).Trim();
            string version = "";
            if (line.Length > versionIndex)
            {
                if (sourceIndex > versionIndex && line.Length > sourceIndex)
                {
                    var availIndex = header.IndexOf("Available", StringComparison.Ordinal);
                    int endOfVersion = availIndex > 0 ? availIndex : sourceIndex;
                    version = line.Substring(versionIndex, endOfVersion - versionIndex).Trim();
                }
                else
                {
                    version = line.Substring(versionIndex).Trim();
                }
            }

            if (apps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                              a.Id.EndsWith($"\\{id}", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            apps.Add(new InstalledApp
            {
                Id = $"Winget\\{id}",
                Name = name,
                Version = version,
                Publisher = "Unknown",
                UninstallString = $"winget uninstall --id {id} --silent",
                InstallLocation = string.Empty,
                Source = "Winget"
            });
        }
    }

    private static (string Exe, string Args) ParseUninstallString(string uninstallString)
    {
        var trimmed = uninstallString.Trim();
        if (trimmed.StartsWith('"'))
        {
            var nextQuote = trimmed.IndexOf('"', 1);
            if (nextQuote > 0)
            {
                var exe = trimmed.Substring(1, nextQuote - 1);
                var args = trimmed.Substring(nextQuote + 1).Trim();
                return (exe, args);
            }
        }

        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0)
        {
            var exe = trimmed.Substring(0, firstSpace);
            var args = trimmed.Substring(firstSpace + 1).Trim();
            return (exe, args);
        }

        return (trimmed, string.Empty);
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string CleanAppNameForSearch(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var clean = name;
        var badSuffixes = new[] { "version", "v1.", "v2.", "v3.", "v4.", "v5.", "v6.", "v7.", "v8.", "v9.", "v0.", "x64", "x86", "64-bit", "32-bit", "32bit", "64bit", "edition", "community", "professional", "enterprise" };
        foreach (var suffix in badSuffixes)
        {
            var idx = clean.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                clean = clean.Substring(0, idx);
            }
        }

        clean = new string(clean.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray());
        var parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts).Trim();
    }

    private static string CleanPublisherForSearch(string publisher)
    {
        if (string.IsNullOrEmpty(publisher))
        {
            return string.Empty;
        }

        var clean = publisher;
        var badSuffixes = new[] { "inc", "ltd", "corp", "corporation", "software", "technologies", "llc", "co." };
        foreach (var suffix in badSuffixes)
        {
            var idx = clean.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                clean = clean.Substring(0, idx);
            }
        }
        clean = new string(clean.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray());
        var parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts).Trim().ToLowerInvariant();
    }
}
