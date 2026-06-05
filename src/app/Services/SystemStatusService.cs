using Microsoft.Win32;
using System.Security.Principal;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class SystemStatusService
{
    private readonly CommandRunner _commands;
    private readonly ReportService _reports;

    public SystemStatusService(CommandRunner commands, ReportService reports)
    {
        _commands = commands;
        _reports = reports;
    }

    public Task<DashboardStatus> GetAsync()
    {
        return Task.Run(() =>
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var systemDrive = new DriveInfo(systemDrivePath);
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            return new DashboardStatus(
                $"{Environment.OSVersion.VersionString} ({RuntimeInformationHelper.ProcessArchitecture})",
                IsAdministrator(),
                uptime,
                systemDrive.Name,
                systemDrive.AvailableFreeSpace,
                systemDrive.TotalSize,
                HasPendingReboot(),
                _commands.Exists("winget"),
                _reports.GetLastReportPath());
        });
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool HasPendingReboot()
    {
        string[] keys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
            @"SYSTEM\CurrentControlSet\Control\Session Manager"
        ];

        try
        {
            foreach (var key in keys)
            {
                using var subKey = Registry.LocalMachine.OpenSubKey(key);
                if (subKey is null)
                {
                    continue;
                }

                if (key.EndsWith("Session Manager", StringComparison.OrdinalIgnoreCase) &&
                    subKey.GetValue("PendingFileRenameOperations") is null)
                {
                    continue;
                }

                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static class RuntimeInformationHelper
    {
        public static string ProcessArchitecture => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
    }
}
