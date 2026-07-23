using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class UninstallerService
{
    private readonly CommandRunner _commands;
    private readonly Dictionary<string, InstalledApp> _recentlyUninstalled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _authorizedLeftoverPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _authorizationLock = new();

    public IpcClient? Client { get; set; }

    public UninstallerService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<IReadOnlyList<InstalledApp>> ScanInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("ScanInstalledApps", cancellationToken: cancellationToken);
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
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(app);
            var response = await Client.SendRequestAsync("UninstallApp", payload, cancellationToken);
            return response == "Success";
        }

        var trustedApp = await ResolveTrustedInstalledAppAsync(app, cancellationToken);
        if (trustedApp is null)
        {
            return false;
        }

        bool success;
        if (trustedApp.Source == "Winget")
        {
            var id = trustedApp.Id.Split('\\').Last();
            var result = await _commands.RunCaptureAsync(
                "winget.exe",
                $"uninstall --id {QuoteArgument(id)} --exact --silent --accept-source-agreements --disable-interactivity",
                cancellationToken);
            success = result.ExitCode == 0;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(trustedApp.UninstallString))
            {
                return false;
            }

            var (exe, args) = ParseUninstallCommand(trustedApp.UninstallString);
            if (Path.GetFileName(exe).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(exe).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                args = NormalizeMsiUninstallArguments(args);
            }
            var result = await _commands.RunCaptureAsync(exe, args, cancellationToken);
            success = result.ExitCode == 0;
        }

        if (success)
        {
            lock (_authorizationLock)
            {
                _recentlyUninstalled[trustedApp.Id] = trustedApp;
            }
        }

        return success;
    }

    public async Task<IReadOnlyList<string>> ScanLeftoversAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(app);
            var response = await Client.SendRequestAsync("ScanLeftovers", payload, cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(response) ?? [];
        }

        InstalledApp? trustedApp;
        lock (_authorizationLock)
        {
            _recentlyUninstalled.TryGetValue(app.Id, out trustedApp);
        }
        trustedApp ??= await ResolveTrustedInstalledAppAsync(app, cancellationToken);
        if (trustedApp is null)
        {
            return [];
        }

        // Winget package IDs are safe to uninstall exactly, but display metadata in the IPC
        // request is not an authority for filesystem cleanup. Do not infer leftovers from it.
        if (trustedApp.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        app = trustedApp;
        var leftovers = await Task.Run<IReadOnlyList<string>>(() =>
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
                                            if (IsSafeLeftoverPath(pubSub))
                                            {
                                                leftovers.Add(pubSub);
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore access errors on subdirectories
                                }
                            }
                            else if (IsSafeLeftoverPath(subdir))
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

            if (!string.IsNullOrEmpty(app.InstallLocation) &&
                Directory.Exists(app.InstallLocation) &&
                IsSafeLeftoverPath(app.InstallLocation))
            {
                if (!leftovers.Contains(app.InstallLocation))
                {
                    leftovers.Add(app.InstallLocation);
                }
            }

            return leftovers.Distinct().ToList();
        }, cancellationToken);

        lock (_authorizationLock)
        {
            foreach (var path in leftovers)
            {
                _authorizedLeftoverPaths.Add(Path.GetFullPath(path));
            }
        }

        return leftovers;
    }

    public async Task<bool> DeleteLeftoversAsync(List<string> paths, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(paths);
            var response = await Client.SendRequestAsync("CleanLeftovers", payload, cancellationToken);
            return response == "Success";
        }

        return await Task.Run(() =>
        {
            bool overallSuccess = true;
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(path);
                bool wasAuthorized;
                lock (_authorizationLock)
                {
                    wasAuthorized = _authorizedLeftoverPaths.Remove(fullPath);
                }

                if (!wasAuthorized || !IsSafeLeftoverPath(fullPath))
                {
                    Console.WriteLine($"[UninstallerService] Blocked unsafe leftover path: {path}");
                    overallSuccess = false;
                    continue;
                }

                try
                {
                    if (Directory.Exists(path))
                    {
                        FileSystem.DeleteDirectory(
                            path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin,
                            UICancelOption.ThrowException);
                    }
                    else if (File.Exists(path))
                    {
                        FileSystem.DeleteFile(
                            path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin,
                            UICancelOption.ThrowException);
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

    private Task<InstalledApp?> ResolveTrustedInstalledAppAsync(
        InstalledApp requestedApp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestedApp.Source.Equals("Winget", StringComparison.OrdinalIgnoreCase) &&
            requestedApp.Id.StartsWith("Winget\\", StringComparison.OrdinalIgnoreCase))
        {
            var packageId = requestedApp.Id[7..];
            var valid = packageId.Length is > 0 and <= 255 && packageId.All(character =>
                char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');
            return Task.FromResult<InstalledApp?>(valid ? requestedApp : null);
        }

        if (!requestedApp.Source.Equals("Registry", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<InstalledApp?>(null);
        }

        (RegistryKey Hive, string RootPath, string Prefix)? location = requestedApp.Id switch
        {
            var id when id.StartsWith("HKLM32\\", StringComparison.OrdinalIgnoreCase) =>
                (Registry.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM32\\"),
            var id when id.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase) =>
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM\\"),
            var id when id.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase) =>
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU\\"),
            _ => null
        };
        if (location is null) return Task.FromResult<InstalledApp?>(null);

        var (hive, rootPath, prefix) = location.Value;
        var subkeyName = requestedApp.Id[prefix.Length..];
        if (string.IsNullOrWhiteSpace(subkeyName) || subkeyName.Contains('\\'))
        {
            return Task.FromResult<InstalledApp?>(null);
        }

        try
        {
            using var key = hive.OpenSubKey($@"{rootPath}\{subkeyName}");
            var displayName = key?.GetValue("DisplayName")?.ToString();
            var uninstallString = key?.GetValue("QuietUninstallString")?.ToString();
            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                uninstallString = key?.GetValue("UninstallString")?.ToString();
            }
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(uninstallString))
            {
                return Task.FromResult<InstalledApp?>(null);
            }

            return Task.FromResult<InstalledApp?>(new InstalledApp
            {
                Id = requestedApp.Id,
                Name = displayName,
                Version = key?.GetValue("DisplayVersion")?.ToString() ?? "Unknown",
                Publisher = key?.GetValue("Publisher")?.ToString() ?? "Unknown",
                UninstallString = uninstallString,
                InstallLocation = key?.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                Source = "Registry"
            });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Task.FromResult<InstalledApp?>(null);
        }
    }

    public static bool IsSafeLeftoverPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var allowedRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        }.Where(root => !string.IsNullOrWhiteSpace(root));

        if (!allowedRoots.Any(root =>
                PathSafetyService.IsPathWithinOrEqual(fullPath, root) &&
                !fullPath.Equals(Path.GetFullPath(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(fullPath) && !File.Exists(fullPath)) return false;
            var info = Directory.Exists(fullPath)
                ? (FileSystemInfo)new DirectoryInfo(fullPath)
                : new FileInfo(fullPath);
            return (info.Attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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

    public static (string Exe, string Args) ParseUninstallCommand(string uninstallString)
    {
        var trimmed = Environment.ExpandEnvironmentVariables(uninstallString).Trim();
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

        string[] executableExtensions = [".exe", ".com", ".cmd", ".bat"];
        var executableEnd = executableExtensions
            .Select(extension => FindExecutableEnd(trimmed, extension))
            .Where(index => index > 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (executableEnd > 0)
        {
            return (trimmed[..executableEnd].Trim(), trimmed[executableEnd..].Trim());
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

    private static int FindExecutableEnd(string command, string extension)
    {
        var searchFrom = 0;
        while (searchFrom < command.Length)
        {
            var index = command.IndexOf(extension, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return -1;
            var end = index + extension.Length;
            if (end == command.Length || char.IsWhiteSpace(command[end])) return end;
            searchFrom = end;
        }

        return -1;
    }

    public static string NormalizeMsiUninstallArguments(string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.StartsWith("/I", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "/X" + trimmed[2..];
        }

        return trimmed.Contains("/norestart", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed} /norestart".Trim();
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

    public async Task<IReadOnlyList<InstalledApp>> ScanAppxPackagesAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("ScanAppxPackages", cancellationToken: cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<InstalledApp>>(response) ?? [];
        }

        return await Task.Run<IReadOnlyList<InstalledApp>>(async () =>
        {
            var apps = new List<InstalledApp>();
            try
            {
                var script = "$apps = @(Get-AppxPackage -AllUsers | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable } | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Publisher = $_.Publisher; Version = $_.Version.ToString(); PackageFullName = $_.PackageFullName } }); ConvertTo-Json -InputObject $apps -Depth 3 -Compress";
                var result = await _commands.RunCaptureAsync(
                    "powershell.exe",
                    $"-NoProfile -Command \"{script}\"",
                    cancellationToken);

                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
                    var elements = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                        ? doc.RootElement.EnumerateArray().ToArray()
                        : [doc.RootElement];

                    foreach (var element in elements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var name = element.GetProperty("Name").GetString() ?? "Unknown";
                        var publisher = element.GetProperty("Publisher").GetString() ?? "Unknown";
                        var version = element.GetProperty("Version").GetString() ?? "Unknown";
                        var fullName = element.GetProperty("PackageFullName").GetString() ?? string.Empty;
                        if (!IsSafeAppxRemovalCandidate(name, fullName))
                        {
                            continue;
                        }

                        apps.Add(new InstalledApp
                        {
                            Id = $"Appx\\{fullName}",
                            Name = name,
                            Publisher = publisher,
                            Version = version,
                            Source = "Appx",
                            UninstallString = $"Remove-AppxPackage -Package \"{fullName}\" -AllUsers",
                            InstallLocation = string.Empty
                        });
                    }
                }
                else
                {
                    var detail = string.IsNullOrWhiteSpace(result.StandardError)
                        ? result.StandardOutput.Trim()
                        : result.StandardError.Trim();
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? $"PowerShell Appx scan exited with code {result.ExitCode}."
                        : detail);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Appx package scan failed: {ex.Message}", ex);
            }

            return apps.GroupBy(a => a.Name).Select(g => g.First()).OrderBy(a => a.Name).ToList();
        }, cancellationToken);
    }

    public async Task<bool> RemoveAppxPackageAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(app);
            var response = await Client.SendRequestAsync("RemoveAppxPackage", payload, cancellationToken);
            return response == "Success";
        }

        var fullName = app.Id.StartsWith("Appx\\", StringComparison.OrdinalIgnoreCase)
            ? app.Id[5..]
            : string.Empty;
        if (!IsSafeAppxRemovalCandidate(app.Name, fullName))
        {
            return false;
        }

        var result = await _commands.RunCaptureAsync(
            "powershell.exe",
            $"-NoProfile -Command \"Remove-AppxPackage -Package '{fullName}' -AllUsers\"",
            cancellationToken);

        return result.ExitCode == 0;
    }

    public static bool IsSafeAppxRemovalCandidate(string packageName, string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageFullName))
        {
            return false;
        }

        string[] protectedTokens =
        [
            "Microsoft.AAD.BrokerPlugin",
            "Microsoft.AccountsControl",
            "Microsoft.AsyncTextService",
            "Microsoft.CredDialogHost",
            "Microsoft.DesktopAppInstaller",
            "Microsoft.LockApp",
            "Microsoft.NET.Native",
            "Microsoft.SecHealthUI",
            "Microsoft.ShellExperienceHost",
            "Microsoft.StartMenuExperienceHost",
            "Microsoft.StorePurchaseApp",
            "Microsoft.UI.Xaml",
            "Microsoft.VCLibs",
            "Microsoft.WindowsAppRuntime",
            "Microsoft.Windows.CloudExperienceHost",
            "Microsoft.Windows.ContentDeliveryManager",
            "Microsoft.Windows.Search",
            "Microsoft.WindowsStore",
            "Microsoft.Windows.ShellExperienceHost",
            "Microsoft.Windows.StartMenuExperienceHost",
            "MicrosoftWindows.Client",
            "MicrosoftWindows.CrossDevice",
            "MicrosoftWindows.UndockedDevKit"
        ];

        return !protectedTokens.Any(token =>
            packageName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
            packageFullName.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
