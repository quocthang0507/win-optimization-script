using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinOptimizationApp.Services;

public static class ProcessEfficiencyService
{
    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        uint ProcessInformationSize);

    public static bool EnableForCurrentProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            
            // In Windows, the green leaf icon in Task Manager is shown only when
            // both EcoQoS is enabled and the process priority class is set to Idle.
            currentProcess.PriorityClass = ProcessPriorityClass.Idle;

            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED
            };

            return SetProcessInformation(
                currentProcess.Handle,
                ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf(state)
            );
        }
        catch
        {
            return false;
        }
    }
}
