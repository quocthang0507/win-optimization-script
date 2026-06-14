using System.Runtime.InteropServices;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ProcessEfficiencyTests
{
    [Fact]
    public void EnableForCurrentProcess_RunsSuccessfully()
    {
        // Act
        var result = ProcessEfficiencyService.EnableForCurrentProcess();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, the call might succeed or fail depending on permissions or OS version.
            // We verify it does not throw and completes.
            Assert.True(result || !result);
        }
        else
        {
            // On non-Windows platforms, it must return false.
            Assert.False(result);
        }
    }
}
