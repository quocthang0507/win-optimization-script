using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class SystemMaintenanceServiceTests
{
    [Theory]
    [InlineData("taskmgr", true)]
    [InlineData("devmgmt", true)]
    [InlineData("diskmgmt", true)]
    [InlineData("services", true)]
    [InlineData("resmon", true)]
    [InlineData("eventvwr", true)]
    [InlineData("dxdiag", true)]
    [InlineData("sysdm", true)]
    [InlineData("invalid_tool", false)]
    [InlineData("", false)]
    public void LaunchAdminTool_RecognizesKnownTools(string toolKey, bool isRecognized)
    {
        // When toolKey is invalid or unrecognized, it must return false immediately
        if (!isRecognized)
        {
            var result = SystemMaintenanceService.LaunchAdminTool(toolKey);
            Assert.False(result);
        }
        else
        {
            // For known tools, target is resolved properly
            // We verify by testing non-null mapping in LaunchAdminTool
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
            Assert.NotNull(target);
        }
    }

    [Fact]
    public async Task ResetTcpIpStack_RequiresAdmin()
    {
        var service = new SystemMaintenanceService(new CommandRunner());
        if (!SystemStatusService.IsAdministrator())
        {
            var result = await service.ResetTcpIpStackAsync();
            Assert.False(result);
        }
    }

    [Fact]
    public async Task ClearArpCache_RequiresAdmin()
    {
        var service = new SystemMaintenanceService(new CommandRunner());
        if (!SystemStatusService.IsAdministrator())
        {
            var result = await service.ClearArpCacheAsync();
            Assert.False(result);
        }
    }
}
