using System.Diagnostics;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class CleanupService(CommandRunner commands)
{
    private readonly CommandRunner _commands = commands;
    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public IpcClient? Client { get; set; }

    public Task<TaskPreview> PreviewAsync(MaintenanceTask task, CancellationToken cancellationToken = default) =>
        PreviewAsync(task, null, cancellationToken);

    public async Task<TaskPreview> PreviewAsync(
        MaintenanceTask task,
        IReadOnlyList<string>? protectedPaths,
        CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new PreviewTaskRequestPayload
            {
                TaskId = task.Id,
                ProtectedPaths = ProtectedPathService.NormalizePaths(protectedPaths).ToList()
            });
            var response = await Client.SendRequestAsync("PreviewTask", payload, cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<TaskPreview>(response) ?? throw new InvalidOperationException("Failed to deserialize TaskPreview");
        }

        return await Task.Run(() =>
        {
            var normalizedProtectedPaths = ProtectedPathService.NormalizePaths(protectedPaths);
            var targets = GetTargets(task.Id)
                .Select(target => ProtectedPathService.IntersectsProtectedTree(target.Path, normalizedProtectedPaths)
                    ? new CleanupTargetPreview(target.Name, target.Path, File.Exists(target.Path) || Directory.Exists(target.Path), 0, 0, "Protected")
                    : PreviewTarget(target, cancellationToken))
                .ToList();

            var warnings = GetWarnings(task.Id).ToList();
            var commands = GetPlannedCommands(task.Id).ToList();
            var bytes = targets.Sum(target => target.Bytes);
            var files = targets.Sum(target => target.FileCount);

            var summary = commands.Count > 0 && targets.Count == 0
                ? $"{commands.Count} command(s) ready"
                : $"{Formatters.FormatBytes(bytes)} across {files:N0} file(s)";

            return new TaskPreview(task.Id, summary, bytes, files, targets, warnings, commands);
        }, cancellationToken);
    }

    public Task<TaskRunResult> RunAsync(MaintenanceTask task, CancellationToken cancellationToken = default) =>
        RunAsync(task, null, cancellationToken);

    public async Task<TaskRunResult> RunAsync(
        MaintenanceTask task,
        IReadOnlyList<string>? protectedPaths,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.Now;
        var messages = new List<string>();
        var errors = new List<string>();
        long freedBytes = 0;
        var filesRemoved = 0;
        var filesSkipped = 0;
        var normalizedProtectedPaths = ProtectedPathService.NormalizePaths(protectedPaths);

        try
        {
            switch (task.Id)
            {
                case "cleanup.dev":
                    foreach (var command in GetPlannedCommands(task.Id))
                    {
                        var (fileName, arguments) = SplitCommand(command);
                        var result = await _commands.RunCaptureAsync(fileName, arguments, cancellationToken);
                        messages.Add($"{command}: exit {result.ExitCode}");
                        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                        {
                            messages.Add(result.StandardOutput.Trim());
                        }

                        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StandardError))
                        {
                            errors.Add(result.StandardError.Trim());
                        }
                    }
                    break;

                case "cleanup.recyclebin":
                    {
                        var result = await _commands.RunCaptureAsync("powershell.exe", "-NoProfile -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"", cancellationToken);
                        messages.Add($"Clear-RecycleBin exit {result.ExitCode}");
                        if (!string.IsNullOrWhiteSpace(result.StandardError))
                        {
                            errors.Add(result.StandardError.Trim());
                        }
                        break;
                    }

                case "cleanup.diskcleanup":
                    {
                        foreach (var command in GetPlannedCommands(task.Id))
                        {
                            var (fileName, arguments) = SplitCommand(command);
                            var result = await _commands.RunCaptureAsync(fileName, arguments, cancellationToken);
                            messages.Add($"{command}: exit {result.ExitCode}");
                            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                            {
                                messages.Add(result.StandardOutput.Trim());
                            }

                            if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StandardError))
                            {
                                errors.Add(result.StandardError.Trim());
                                break;
                            }
                        }
                        break;
                    }

                case "cleanup.windowsupdate":
                    {
                        var stoppedServices = new List<string>();
                        try
                        {
                            foreach (var serviceName in new[] { "wuauserv", "bits", "dosvc" })
                            {
                                var stopResult = await _commands.RunCaptureAsync(
                                    "sc.exe",
                                    $"stop {serviceName}",
                                    cancellationToken);
                                if (stopResult.ExitCode == 0)
                                {
                                    stoppedServices.Add(serviceName);
                                }
                            }
                            if (stoppedServices.Count > 0)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                            }

                            foreach (var target in GetTargets(task.Id))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (ProtectedPathService.IntersectsProtectedTree(target.Path, normalizedProtectedPaths))
                                {
                                    filesSkipped++;
                                    messages.Add($"Skipped protected target: {target.Path}");
                                    continue;
                                }

                                if (!IsSafeCleanupPath(target.Path))
                                {
                                    filesSkipped++;
                                    errors.Add($"Skipped unsafe path: {target.Path}");
                                    continue;
                                }

                                var (removedCount, skippedCount, removedBytes) =
                                    DeleteContents(target, errors, cancellationToken);
                                freedBytes += removedBytes;
                                filesRemoved += removedCount;
                                filesSkipped += skippedCount;
                                messages.Add($"Cleaned {target.Name}: {Formatters.FormatBytes(removedBytes)}.");
                            }
                        }
                        finally
                        {
                            foreach (var serviceName in stoppedServices.AsEnumerable().Reverse())
                            {
                                var startResult = await _commands.RunCaptureAsync(
                                    "sc.exe",
                                    $"start {serviceName}",
                                    CancellationToken.None);
                                if (startResult.ExitCode != 0)
                                {
                                    errors.Add($"Could not restart Windows service {serviceName}.");
                                }
                            }
                        }
                        break;
                    }

                case "privacy.clipboard":
                    await _commands.RunCaptureAsync("cmd.exe", "/c echo off | clip", cancellationToken);
                    messages.Add("Clipboard cleared.");
                    break;

                case "privacy.powershell":
                    {
                        var historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "PowerShell", "PSReadLine", "ConsoleHost_history.txt");
                        var preview = PreviewTarget("PowerShell history", historyPath);
                        if (File.Exists(historyPath) && IsSafeFile(historyPath) &&
                            !ProtectedPathService.IntersectsProtectedTree(historyPath, normalizedProtectedPaths))
                        {
                            File.Delete(historyPath);
                            freedBytes += preview.Bytes;
                            filesRemoved++;
                        }
                        break;
                    }

                case "network.dns":
                    {
                        var result = await _commands.RunCaptureAsync("ipconfig.exe", "/flushdns", cancellationToken);
                        messages.Add(result.StandardOutput.Trim());
                        if (result.ExitCode != 0)
                        {
                            errors.Add(result.StandardError.Trim());
                        }
                        break;
                    }

                case "settings.storage":
                    if (await CommandRunner.StartShellAsync("ms-settings:storagesense", string.Empty))
                    {
                        messages.Add("Opened Storage Sense settings.");
                    }
                    else
                    {
                        errors.Add("Windows Storage Sense settings could not be opened.");
                    }
                    break;

                default:
                    foreach (var target in GetTargets(task.Id))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ProtectedPathService.IntersectsProtectedTree(target.Path, normalizedProtectedPaths))
                        {
                            filesSkipped++;
                            messages.Add($"Skipped protected target: {target.Path}");
                            continue;
                        }

                        var preview = PreviewTarget(target, cancellationToken);
                        if (!preview.Exists)
                        {
                            continue;
                        }

                        if (!IsSafeCleanupPath(target.Path))
                        {
                            filesSkipped += preview.FileCount;
                            errors.Add($"Skipped unsafe path: {target.Path}");
                            continue;
                        }

                        var (removedCount, skippedCount, removedBytes) = DeleteContents(target, errors, cancellationToken);
                        freedBytes += removedBytes;
                        filesRemoved += removedCount;
                        filesSkipped += skippedCount;
                        messages.Add($"Cleaned {target.Name}: {Formatters.FormatBytes(removedBytes)}.");
                    }
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        return new TaskRunResult(
            task.Id,
            task.Label,
            started,
            DateTimeOffset.Now,
            errors.Count == 0,
            freedBytes,
            filesRemoved,
            filesSkipped,
            messages,
            errors);
    }

    private static IEnumerable<CleanupTargetDefinition> GetTargets(string taskId)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

        return taskId switch
        {
            "cleanup.temp" =>
            [
                ("User temp", Path.GetTempPath()),
                ("Windows temp", Path.Combine(windir, "Temp"))
            ],
            "cleanup.shaders" =>
            [
                ("DirectX shader cache", Path.Combine(localAppData, "D3DSCache"))
            ],
            "cleanup.crashdumps" =>
            [
                ("User crash dumps", Path.Combine(localAppData, "CrashDumps"))
            ],
            "cleanup.errorreports" =>
            [
                new CleanupTargetDefinition("System WER archive", Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"), "*", TimeSpan.FromDays(14)),
                new CleanupTargetDefinition("System WER queue", Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"), "*", TimeSpan.FromDays(14)),
                new CleanupTargetDefinition("User WER archive", Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"), "*", TimeSpan.FromDays(14)),
                new CleanupTargetDefinition("User WER queue", Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue"), "*", TimeSpan.FromDays(14))
            ],
            "cleanup.prefetch" =>
            [
                new CleanupTargetDefinition("Stale Windows Prefetch", Path.Combine(windir, "Prefetch"), "*.pf", TimeSpan.FromDays(30))
            ],
            "cleanup.defenderlogs" =>
            [
                new CleanupTargetDefinition("Defender support logs", Path.Combine(programData, "Microsoft", "Windows Defender", "Support"), "*.log", TimeSpan.FromDays(30)),
                new CleanupTargetDefinition("Defender support traces", Path.Combine(programData, "Microsoft", "Windows Defender", "Support"), "*.etl", TimeSpan.FromDays(30))
            ],
            "cleanup.systemdumps" =>
            [
                new CleanupTargetDefinition("System memory dump", Path.Combine(windir, "MEMORY.DMP"), "*", TimeSpan.FromDays(7)),
                new CleanupTargetDefinition("System minidumps", Path.Combine(windir, "Minidump"), "*.dmp", TimeSpan.FromDays(7))
            ],
            "cleanup.browser" => GetBrowserTargets(localAppData, appData),
            "cleanup.windowsupdate" =>
            [
                ("Windows Update downloads", Path.Combine(windir, "SoftwareDistribution", "Download")),
                ("Delivery Optimization", Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization")),
                ("ProgramData Delivery Optimization", Path.Combine(programData, "Microsoft", "Windows", "DeliveryOptimization", "Cache"))
            ],
            "cleanup.windowsold" =>
            [
                ("Windows.old", Path.Combine(systemDrive, "Windows.old"))
            ],
            "privacy.recentFiles" => GetRecentFileTargets(appData),
            "privacy.powershell" =>
            [
                ("PowerShell history", Path.Combine(appData, "Microsoft", "Windows", "PowerShell", "PSReadLine", "ConsoleHost_history.txt"))
            ],
            "privacy.browserHistory" => GetBrowserHistoryTargets(localAppData, appData),
            "privacy.browserCookies" => GetBrowserCookieAndSessionTargets(localAppData, appData),
            _ => []
        };
    }

    private static IEnumerable<CleanupTargetDefinition> GetRecentFileTargets(string appData)
    {
        var recentRoot = Path.Combine(appData, "Microsoft", "Windows", "Recent");
        yield return ("Recent documents", recentRoot);
        yield return ("Automatic jump lists", Path.Combine(recentRoot, "AutomaticDestinations"));
        yield return ("Custom jump lists", Path.Combine(recentRoot, "CustomDestinations"));
    }

    private static IEnumerable<CleanupTargetDefinition> GetBrowserTargets(string localAppData, string appData)
    {
        var chromiumRoots = new (string Name, string Root)[]
        {
            ("Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data")),
            ("Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data")),
            ("Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")),
            ("Opera", Path.Combine(appData, "Opera Software", "Opera Stable"))
        };

        string[] chromiumCachePaths = ["Cache", "Code Cache", "GPUCache", Path.Combine("Service Worker", "CacheStorage"), Path.Combine("Service Worker", "ScriptCache")];

        foreach (var (name, root) in chromiumRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var profiles = Directory.GetDirectories(root)
                .Where(path => Path.GetFileName(path).Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                               Path.GetFileName(path).StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                .DefaultIfEmpty(root);

            foreach (var profile in profiles)
            {
                foreach (var cachePath in chromiumCachePaths)
                {
                    yield return ($"{name} {Path.GetFileName(profile)} {cachePath}", Path.Combine(profile, cachePath));
                }
            }
        }

        var firefoxProfiles = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                yield return ($"Firefox {Path.GetFileName(profile)} cache2", Path.Combine(profile, "cache2"));
                yield return ($"Firefox {Path.GetFileName(profile)} startupCache", Path.Combine(profile, "startupCache"));
            }
        }
    }

    private static IEnumerable<CleanupTargetDefinition> GetBrowserHistoryTargets(string localAppData, string appData)
    {
        string[] chromiumHistoryFiles = ["History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal"];

        foreach (var (browser, profile) in GetChromiumProfiles(localAppData, appData))
        {
            foreach (var file in chromiumHistoryFiles)
            {
                yield return ($"{browser} {Path.GetFileName(profile)} {file}", Path.Combine(profile, file));
            }
        }

        foreach (var profile in GetFirefoxProfiles(appData))
        {
            yield return ($"Firefox {Path.GetFileName(profile)} history", Path.Combine(profile, "places.sqlite"));
            yield return ($"Firefox {Path.GetFileName(profile)} history wal", Path.Combine(profile, "places.sqlite-wal"));
            yield return ($"Firefox {Path.GetFileName(profile)} history shm", Path.Combine(profile, "places.sqlite-shm"));
            yield return ($"Firefox {Path.GetFileName(profile)} form history", Path.Combine(profile, "formhistory.sqlite"));
        }
    }

    private static IEnumerable<CleanupTargetDefinition> GetBrowserCookieAndSessionTargets(string localAppData, string appData)
    {
        foreach (var (browser, profile) in GetChromiumProfiles(localAppData, appData))
        {
            yield return ($"{browser} {Path.GetFileName(profile)} cookies", Path.Combine(profile, "Network", "Cookies"));
            yield return ($"{browser} {Path.GetFileName(profile)} cookies journal", Path.Combine(profile, "Network", "Cookies-journal"));
            yield return ($"{browser} {Path.GetFileName(profile)} legacy cookies", Path.Combine(profile, "Cookies"));
            yield return ($"{browser} {Path.GetFileName(profile)} legacy cookies journal", Path.Combine(profile, "Cookies-journal"));
            yield return ($"{browser} {Path.GetFileName(profile)} sessions", Path.Combine(profile, "Sessions"));
            yield return ($"{browser} {Path.GetFileName(profile)} session storage", Path.Combine(profile, "Session Storage"));
        }

        foreach (var profile in GetFirefoxProfiles(appData))
        {
            yield return ($"Firefox {Path.GetFileName(profile)} cookies", Path.Combine(profile, "cookies.sqlite"));
            yield return ($"Firefox {Path.GetFileName(profile)} cookies wal", Path.Combine(profile, "cookies.sqlite-wal"));
            yield return ($"Firefox {Path.GetFileName(profile)} cookies shm", Path.Combine(profile, "cookies.sqlite-shm"));
            yield return ($"Firefox {Path.GetFileName(profile)} session", Path.Combine(profile, "sessionstore.jsonlz4"));
            yield return ($"Firefox {Path.GetFileName(profile)} session backups", Path.Combine(profile, "sessionstore-backups"));
        }
    }

    private static IEnumerable<(string Browser, string Profile)> GetChromiumProfiles(string localAppData, string appData)
    {
        var chromiumRoots = new (string Browser, string Root)[]
        {
            ("Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data")),
            ("Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data")),
            ("Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")),
            ("Opera", Path.Combine(appData, "Opera Software", "Opera Stable"))
        };

        foreach (var (browser, root) in chromiumRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var profiles = Directory.GetDirectories(root)
                .Where(path => Path.GetFileName(path).Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                               Path.GetFileName(path).StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                .DefaultIfEmpty(root);

            foreach (var profile in profiles)
            {
                yield return (browser, profile);
            }
        }
    }

    private static IEnumerable<string> GetFirefoxProfiles(string appData)
    {
        var firefoxProfiles = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");
        return Directory.Exists(firefoxProfiles)
            ? Directory.GetDirectories(firefoxProfiles)
            : [];
    }

    private static IEnumerable<string> GetWarnings(string taskId)
    {
        if (taskId is "cleanup.browser" or "privacy.browserHistory" or "privacy.browserCookies")
        {
            string[] names = ["msedge", "chrome", "firefox", "brave", "opera"];
            foreach (var processName in names)
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    yield return $"{processName}.exe is running; close it for a more complete cleanup.";
                }
            }
        }

        if (taskId is "cleanup.windowsupdate" or "cleanup.windowsold")
        {
            yield return "High-risk cleanup: create a restore point before running.";
        }

        if (taskId == "cleanup.prefetch")
        {
            yield return "Only Prefetch files older than 30 days are eligible; recent launch data is preserved.";
        }

        if (taskId == "cleanup.defenderlogs")
        {
            yield return "Protection history and quarantine are not included; only old support logs are eligible.";
        }

        if (taskId is "cleanup.errorreports" or "cleanup.systemdumps")
        {
            yield return "These diagnostic files may be useful when investigating recent Windows failures.";
        }
    }

    private IEnumerable<string> GetPlannedCommands(string taskId)
    {
        if (taskId == "cleanup.diskcleanup")
        {
            yield return "cleanmgr.exe /sageset:7307";
            yield return "cleanmgr.exe /sagerun:7307";
            yield break;
        }

        if (taskId != "cleanup.dev")
        {
            yield break;
        }

        if (_commands.Exists("dotnet"))
        {
            yield return "dotnet nuget locals all --clear";
        }

        if (_commands.Exists("pip"))
        {
            yield return "pip cache purge";
        }

        if (_commands.Exists("npm"))
        {
            yield return "npm cache clean --force";
        }

        if (_commands.Exists("yarn"))
        {
            yield return "yarn cache clean";
        }
    }

    internal static CleanupTargetPreview PreviewTarget(string name, string path)
    {
        return PreviewTarget(new CleanupTargetDefinition(name, path), CancellationToken.None);
    }

    private static CleanupTargetPreview PreviewTarget(
        CleanupTargetDefinition target,
        CancellationToken cancellationToken)
    {
        var name = target.Name;
        var path = target.Path;
        try
        {
            if (File.Exists(path))
            {
                var file = new FileInfo(path);
                if (!MatchesTargetFilter(file, target))
                {
                    return new CleanupTargetPreview(name, path, true, 0, 0, "No eligible files");
                }
                return new CleanupTargetPreview(name, path, true, file.Length, 1, TargetStatus(target));
            }

            if (!Directory.Exists(path))
            {
                return new CleanupTargetPreview(name, path, false, 0, 0, "Not found");
            }

            long bytes = 0;
            var fileCount = 0;
            foreach (var file in Directory.EnumerateFiles(path, target.Pattern, RecursiveEnumeration))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (!MatchesTargetFilter(info, target))
                    {
                        continue;
                    }
                    bytes += info.Length;
                    fileCount++;
                }
                catch
                {
                    // Locked or inaccessible files are counted as skipped during cleanup.
                }
            }

            return new CleanupTargetPreview(name, path, true, bytes, fileCount, TargetStatus(target));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CleanupTargetPreview(name, path, Directory.Exists(path) || File.Exists(path), 0, 0, ex.Message);
        }
    }

    private static (int Removed, int Skipped, long RemovedBytes) DeleteContents(
        CleanupTargetDefinition target,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var path = target.Path;
        var removed = 0;
        var skipped = 0;
        long removedBytes = 0;
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(path))
        {
            try
            {
                var info = new FileInfo(path);
                if (!MatchesTargetFilter(info, target))
                {
                    return (0, 0, 0);
                }
                var bytes = info.Length;
                File.Delete(path);
                return (1, 0, bytes);
            }
            catch (Exception ex)
            {
                errors.Add($"{path}: {ex.Message}");
                return (0, 1, 0);
            }
        }

        if (!Directory.Exists(path))
        {
            return (0, 0, 0);
        }

        List<string> files;
        List<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(path, target.Pattern, RecursiveEnumeration)
                .Where(file =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return MatchesTargetFilter(new FileInfo(file), target);
                })
                .ToList();
            dirs = target.Pattern == "*" && target.MinimumAge is null
                ? Directory.EnumerateDirectories(path, "*", RecursiveEnumeration)
                    .OrderByDescending(directory => directory.Length)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add($"{path}: {ex.Message}");
            return (0, 1, 0);
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                if (fileName == "f01b4d95cf55d32a.automaticdestinations-ms" || 
                    fileName == "5b39b05c22c0cbb0.customdestinations-ms" ||
                    fileName == "f01b4d95cf55d32a.customdestinations-ms")
                {
                    continue;
                }

                var bytes = new FileInfo(file).Length;
                File.Delete(file);
                removed++;
                removedBytes += bytes;
            }
            catch (Exception ex)
            {
                skipped++;
                errors.Add($"{file}: {ex.Message}");
            }
        }

        foreach (var dir in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Delete(dir, false);
            }
            catch
            {
                // Directory not empty due to skipped files, or access denied.
            }
        }

        return (removed, skipped, removedBytes);
    }

    private static bool MatchesTargetFilter(FileInfo file, CleanupTargetDefinition target)
    {
        return target.MinimumAge is null ||
               file.LastWriteTimeUtc <= DateTime.UtcNow - target.MinimumAge.Value;
    }

    private static string TargetStatus(CleanupTargetDefinition target)
    {
        return target.MinimumAge is { } age
            ? $"Ready (older than {Math.Max(1, (int)age.TotalDays)} days)"
            : "Ready";
    }

    private static bool IsSafeFile(string path)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return PathSafetyService.IsPathWithinOrEqual(path, appData);
    }

    private static bool IsSafeCleanupPath(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(fullPath) || fullPath.Length <= 3)
        {
            return false;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

        string[] allowedRoots =
        [
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.Combine(windir, "Temp"),
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"),
            Path.Combine(appData, "Opera Software", "Opera Stable"),
            Path.Combine(appData, "Mozilla", "Firefox", "Profiles"),
            Path.Combine(appData, "Microsoft", "Windows", "Recent"),
            Path.Combine(windir, "SoftwareDistribution", "Download"),
            Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization"),
            Path.Combine(programData, "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
            Path.Combine(programData, "Microsoft", "Windows", "WER"),
            Path.Combine(programData, "Microsoft", "Windows Defender", "Support"),
            Path.Combine(windir, "Prefetch"),
            Path.Combine(windir, "Minidump"),
            Path.Combine(windir, "MEMORY.DMP"),
            Path.Combine(systemDrive, "Windows.old")
        ];

        return allowedRoots.Any(root => PathSafetyService.IsPathWithinOrEqual(fullPath, root));
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        var trimmed = command.Trim();
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

        var firstSpace = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return firstSpace < 0
            ? (trimmed, string.Empty)
            : (trimmed[..firstSpace], trimmed[(firstSpace + 1)..]);
    }

    private sealed record CleanupTargetDefinition(
        string Name,
        string Path,
        string Pattern = "*",
        TimeSpan? MinimumAge = null)
    {
        public static implicit operator CleanupTargetDefinition((string Name, string Path) target) =>
            new(target.Name, target.Path);
    }
}
