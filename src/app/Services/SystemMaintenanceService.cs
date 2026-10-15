using System.Diagnostics;

namespace WinOptimizationApp.Services;

public enum SystemToolOutcome
{
    /// <summary>The operation completed and its effect was verified.</summary>
    Succeeded,
    /// <summary>The operation was launched but runs on its own; completion is not tracked.</summary>
    Started,
    /// <summary>The operation was refused because the app is not elevated.</summary>
    NeedsAdmin,
    /// <summary>The operation ran but did not fully succeed.</summary>
    Failed
}

public sealed record SystemToolResult(SystemToolOutcome Outcome, string Detail = "")
{
    public bool IsSuccess => Outcome is SystemToolOutcome.Succeeded or SystemToolOutcome.Started;
}

public sealed class SystemMaintenanceService
{
    private static readonly IReadOnlyDictionary<string, string> AdminTools =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["taskmgr"] = "taskmgr.exe",
            ["devmgmt"] = "devmgmt.msc",
            ["diskmgmt"] = "diskmgmt.msc",
            ["services"] = "services.msc",
            ["resmon"] = "resmon.exe",
            ["eventvwr"] = "eventvwr.msc",
            ["dxdiag"] = "dxdiag.exe",
            ["sysdm"] = "sysdm.cpl"
        };

    private readonly CommandRunner _commands;
    private readonly Func<bool> _isAdministrator;

    public SystemMaintenanceService(CommandRunner commands, Func<bool>? isAdministrator = null)
    {
        _commands = commands;
        _isAdministrator = isAdministrator ?? SystemStatusService.IsAdministrator;
    }

    public static IReadOnlyCollection<string> AdminToolKeys => AdminTools.Keys.ToArray();

    public static string? ResolveAdminToolTarget(string? toolKey)
    {
        return !string.IsNullOrWhiteSpace(toolKey) && AdminTools.TryGetValue(toolKey.Trim(), out var target)
            ? target
            : null;
    }

    public static bool LaunchAdminTool(string toolKey)
    {
        var target = ResolveAdminToolTarget(toolKey);
        if (target is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the icon cache database files Windows keeps for the current user.
    /// </summary>
    public static IReadOnlyList<string> GetIconCacheFiles(string localAppData)
    {
        var files = new List<string>();
        var legacy = Path.Combine(localAppData, "IconCache.db");
        if (File.Exists(legacy))
        {
            files.Add(legacy);
        }

        var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
        if (Directory.Exists(explorerDir))
        {
            files.AddRange(Directory.EnumerateFiles(explorerDir, "iconcache*.db", SearchOption.TopDirectoryOnly));
        }

        return files;
    }

    /// <summary>
    /// Deletes the given files, returning the number deleted and the files that could not be removed.
    /// </summary>
    public static (int Deleted, IReadOnlyList<string> Failed) DeleteFiles(IEnumerable<string> files)
    {
        var deleted = 0;
        var failed = new List<string>();
        foreach (var file in files)
        {
            try
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(file);
            }
        }

        return (deleted, failed);
    }

    public async Task<SystemToolResult> RebuildIconCacheAsync(CancellationToken cancellationToken = default)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        try
        {
            foreach (var explorer in Process.GetProcessesByName("explorer"))
            {
                try { explorer.Kill(); } catch { /* Process may already be gone. */ }
                finally { explorer.Dispose(); }
            }

            // Explorer holds the cache files open; give it a moment to release them.
            await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);

            var (deleted, failed) = DeleteFiles(GetIconCacheFiles(localAppData));
            return failed.Count == 0
                ? new SystemToolResult(SystemToolOutcome.Succeeded, deleted.ToString())
                : new SystemToolResult(SystemToolOutcome.Failed, string.Join(Environment.NewLine, failed));
        }
        finally
        {
            await EnsureExplorerRunningAsync();
        }
    }

    public async Task<SystemToolResult> ResetStoreCacheAsync()
    {
        // wsreset.exe opens the Store when it finishes and may not exit promptly, so launch it
        // detached instead of waiting on it; the user sees the Store open when the reset is done.
        var started = await CommandRunner.StartShellAsync("wsreset.exe", string.Empty);
        return new SystemToolResult(started ? SystemToolOutcome.Started : SystemToolOutcome.Failed);
    }

    public Task<SystemToolResult> ResetTcpIpStackAsync(CancellationToken cancellationToken = default)
    {
        return RunAdminCommandAsync("netsh", "int ip reset", cancellationToken);
    }

    public Task<SystemToolResult> ClearArpCacheAsync(CancellationToken cancellationToken = default)
    {
        return RunAdminCommandAsync("netsh", "interface ip delete arpcache", cancellationToken);
    }

    private async Task<SystemToolResult> RunAdminCommandAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        if (!_isAdministrator())
        {
            return new SystemToolResult(SystemToolOutcome.NeedsAdmin);
        }

        var result = await _commands.RunCaptureAsync(fileName, arguments, cancellationToken);
        if (result.ExitCode == 0)
        {
            return new SystemToolResult(SystemToolOutcome.Succeeded);
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return new SystemToolResult(SystemToolOutcome.Failed, detail.Trim());
    }

    private static async Task EnsureExplorerRunningAsync()
    {
        // Windows usually restarts the shell itself; only start it when it did not come back,
        // otherwise an extra File Explorer window would open.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (IsExplorerRunning())
            {
                return;
            }

            await Task.Delay(300);
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true });
        }
        catch
        {
            // Nothing more can be done from here; the user can start explorer from Task Manager.
        }
    }

    private static bool IsExplorerRunning()
    {
        var processes = Process.GetProcessesByName("explorer");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
