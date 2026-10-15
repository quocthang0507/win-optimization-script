using System.Diagnostics;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class SystemMaintenanceService
{
    private readonly CommandRunner _commands;

    public SystemMaintenanceService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<bool> RebuildIconCacheAsync(CancellationToken cancellationToken = default)
    {
        // Rebuilds Windows icon cache by stopping explorer, removing cached database files, and restarting explorer.
        var script = "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; " +
                     "Start-Sleep -Milliseconds 600; " +
                     "Remove-Item -Force -Path \"$env:LOCALAPPDATA\\IconCache.db\" -ErrorAction SilentlyContinue; " +
                     "Remove-Item -Force -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\iconcache*.db\" -ErrorAction SilentlyContinue; " +
                     "Start-Process explorer.exe";

        var result = await _commands.RunCaptureAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
            cancellationToken);

        return result.ExitCode == 0;
    }

    public async Task<bool> ResetStoreCacheAsync(CancellationToken cancellationToken = default)
    {
        // wsreset.exe resets the Windows Store cache without changing account settings or deleting installed apps.
        var result = await _commands.RunCaptureAsync("wsreset.exe", string.Empty, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ResetTcpIpStackAsync(CancellationToken cancellationToken = default)
    {
        if (!SystemStatusService.IsAdministrator())
        {
            return false;
        }

        var result = await _commands.RunCaptureAsync("netsh", "int ip reset", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ClearArpCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!SystemStatusService.IsAdministrator())
        {
            return false;
        }

        var result = await _commands.RunCaptureAsync("netsh", "interface ip delete arpcache", cancellationToken);
        return result.ExitCode == 0;
    }

    public static bool LaunchAdminTool(string toolKey)
    {
        var target = toolKey.ToLowerInvariant() switch
        {
            "taskmgr" => "taskmgr.exe",
            "devmgmt" => "devmgmt.msc",
            "diskmgmt" => "diskmgmt.msc",
            "services" => "services.msc",
            "resmon" => "resmon.exe",
            "eventvwr" => "eventvwr.msc",
            "dxdiag" => "dxdiag.exe",
            "sysdm" => "sysdm.cpl",
            _ => null
        };

        if (target is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
