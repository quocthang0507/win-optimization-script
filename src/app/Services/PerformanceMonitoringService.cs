using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class PerformanceMonitoringService
{
    private readonly object _cpuLock = new();
    private readonly object _networkLock = new();
    private ulong _lastIdleTime;
    private ulong _lastKernelTime;
    private ulong _lastUserTime;
    private bool _hasCpuSample;
    private long _lastReceivedBytes;
    private long _lastSentBytes;
    private DateTimeOffset? _lastNetworkSample;

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
            return new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX))
            };
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    public Task<SystemMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 1. Measure CPU Usage
            double cpuUsage = GetCpuUsageInternal();

            // 2. Measure RAM Memory
            ulong totalRam = 0;
            ulong usedRam = 0;
            double ramPercent = 0;
            var memStatus = MEMORYSTATUSEX.Create();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                totalRam = memStatus.ullTotalPhys;
                usedRam = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
                ramPercent = memStatus.dwMemoryLoad;
            }

            // 3. Measure Disk Usage (System Drive)
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemDrivePath);
            long diskFree = drive.AvailableFreeSpace;
            long diskTotal = drive.TotalSize;
            long diskUsed = diskTotal - diskFree;
            double diskPercent = diskTotal > 0 ? (diskUsed * 100.0 / diskTotal) : 0;

            var (downloadBytesPerSecond, uploadBytesPerSecond) = GetNetworkThroughput();
            cancellationToken.ThrowIfCancellationRequested();

            return new SystemMetrics(
                cpuUsage,
                ramPercent,
                (long)usedRam,
                (long)totalRam,
                diskFree,
                diskTotal,
                diskPercent,
                downloadBytesPerSecond,
                uploadBytesPerSecond
            );
        }, cancellationToken);
    }

    private (double Download, double Upload) GetNetworkThroughput()
    {
        long received = 0;
        long sent = 0;

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            try
            {
                var statistics = networkInterface.GetIPv4Statistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // An adapter can disappear while metrics are being sampled.
            }
        }

        lock (_networkLock)
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = _lastNetworkSample is null ? 0 : (now - _lastNetworkSample.Value).TotalSeconds;
            var download = elapsed > 0 && received >= _lastReceivedBytes
                ? (received - _lastReceivedBytes) / elapsed
                : 0;
            var upload = elapsed > 0 && sent >= _lastSentBytes
                ? (sent - _lastSentBytes) / elapsed
                : 0;

            _lastReceivedBytes = received;
            _lastSentBytes = sent;
            _lastNetworkSample = now;
            return (download, upload);
        }
    }

    private double GetCpuUsageInternal()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return 0;
        }

        var idle = ConvertFileTime(idleTime);
        var kernel = ConvertFileTime(kernelTime);
        var user = ConvertFileTime(userTime);
        lock (_cpuLock)
        {
            var usage = _hasCpuSample
                ? CalculateCpuUsage(_lastIdleTime, _lastKernelTime, _lastUserTime, idle, kernel, user)
                : 0;
            _lastIdleTime = idle;
            _lastKernelTime = kernel;
            _lastUserTime = user;
            _hasCpuSample = true;
            return usage;
        }
    }

    internal static double CalculateCpuUsage(
        ulong previousIdle,
        ulong previousKernel,
        ulong previousUser,
        ulong currentIdle,
        ulong currentKernel,
        ulong currentUser)
    {
        if (currentIdle < previousIdle || currentKernel < previousKernel || currentUser < previousUser)
        {
            return 0;
        }

        var idleDiff = currentIdle - previousIdle;
        var kernelDiff = currentKernel - previousKernel;
        var userDiff = currentUser - previousUser;
        if (ulong.MaxValue - kernelDiff < userDiff)
        {
            return 0;
        }

        var systemDiff = kernelDiff + userDiff;
        if (systemDiff == 0 || idleDiff > systemDiff)
        {
            return 0;
        }

        return Math.Clamp((1.0 - ((double)idleDiff / systemDiff)) * 100.0, 0.0, 100.0);
    }

    private static ulong ConvertFileTime(System.Runtime.InteropServices.ComTypes.FILETIME filetime)
    {
        return ((ulong)filetime.dwHighDateTime << 32) | (uint)filetime.dwLowDateTime;
    }
}
