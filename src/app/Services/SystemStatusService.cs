using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class SystemStatusService
{
    private readonly CommandRunner _commands;
    private readonly ReportService _reports;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        
        public static MEMORYSTATUSEX Create()
        {
            var result = new MEMORYSTATUSEX();
            result.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            return result;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public IpcClient? Client { get; set; }

    public SystemStatusService(CommandRunner commands, ReportService reports)
    {
        _commands = commands;
        _reports = reports;
    }

    public async Task<DashboardStatus> GetAsync()
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("GetStatus");
            return System.Text.Json.JsonSerializer.Deserialize<DashboardStatus>(response) ?? throw new InvalidOperationException("Failed to deserialize DashboardStatus");
        }
        return await Task.Run(() =>
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var systemDrive = new DriveInfo(systemDrivePath);
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            // Fetch RAM memory
            ulong totalRam = 0;
            ulong availRam = 0;
            var memStatus = MEMORYSTATUSEX.Create();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                totalRam = memStatus.ullTotalPhys;
                availRam = memStatus.ullAvailPhys;
            }

            // Fetch CPU Processor Name
            string cpuName = "Unknown CPU";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    cpuName = (key.GetValue("ProcessorNameString") as string ?? "Unknown CPU").Trim();
                }
            }
            catch
            {
                // Fallback
            }

            return new DashboardStatus(
                $"{Environment.OSVersion.VersionString} ({RuntimeInformationHelper.ProcessArchitecture})",
                IsAdministrator(),
                uptime,
                systemDrive.Name,
                systemDrive.AvailableFreeSpace,
                systemDrive.TotalSize,
                HasPendingReboot(),
                _commands.Exists("winget"),
                _reports.GetLastReportPath(),
                cpuName,
                totalRam,
                availRam);
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
