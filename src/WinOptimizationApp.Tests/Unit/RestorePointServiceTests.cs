using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class RestorePointServiceTests
{
    [Fact]
    public async Task CreateManualRestorePoint_ReturnsFalse_WhenNotAdministrator()
    {
        var service = new RestorePointService(new CommandRunner());
        if (!SystemStatusService.IsAdministrator())
        {
            var (success, message) = await service.CreateManualRestorePointAsync("Test Point");
            Assert.False(success);
            Assert.Contains("Administrator", message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
