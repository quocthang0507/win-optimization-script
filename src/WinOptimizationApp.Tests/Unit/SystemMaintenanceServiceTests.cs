using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class SystemMaintenanceServiceTests
{
    [Theory]
    [InlineData("taskmgr", "taskmgr.exe")]
    [InlineData("TASKMGR", "taskmgr.exe")]
    [InlineData("devmgmt", "devmgmt.msc")]
    [InlineData("diskmgmt", "diskmgmt.msc")]
    [InlineData("services", "services.msc")]
    [InlineData("resmon", "resmon.exe")]
    [InlineData("eventvwr", "eventvwr.msc")]
    [InlineData("dxdiag", "dxdiag.exe")]
    [InlineData("sysdm", "sysdm.cpl")]
    public void ResolveAdminToolTarget_MapsKnownKeys(string key, string expectedTarget)
    {
        var target = SystemMaintenanceService.ResolveAdminToolTarget(key);
        Assert.Equal(expectedTarget, target);
    }

    [Theory]
    [InlineData("invalid_tool")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveAdminToolTarget_ReturnsNullForUnknownOrBlank(string? key)
    {
        Assert.Null(SystemMaintenanceService.ResolveAdminToolTarget(key));
    }

    [Fact]
    public void AdminToolKeys_ContainsAllExpectedTools()
    {
        var keys = SystemMaintenanceService.AdminToolKeys;
        Assert.Contains("taskmgr", keys);
        Assert.Contains("devmgmt", keys);
        Assert.Contains("diskmgmt", keys);
        Assert.Contains("services", keys);
        Assert.Contains("resmon", keys);
        Assert.Contains("eventvwr", keys);
        Assert.Contains("dxdiag", keys);
        Assert.Contains("sysdm", keys);
    }

    [Fact]
    public void LaunchAdminTool_ReturnsFalse_ForUnknownTool()
    {
        Assert.False(SystemMaintenanceService.LaunchAdminTool("nonexistent_tool_xyz"));
    }

    [Fact]
    public void GetIconCacheFiles_FindsLegacyAndModernFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "WinOpt_IconCache_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var legacyFile = Path.Combine(tempDir, "IconCache.db");
            File.WriteAllText(legacyFile, "dummy");

            var modernDir = Path.Combine(tempDir, "Microsoft", "Windows", "Explorer");
            Directory.CreateDirectory(modernDir);
            var modernFile1 = Path.Combine(modernDir, "iconcache_32.db");
            var modernFile2 = Path.Combine(modernDir, "iconcache_idx.db");
            var nonCacheFile = Path.Combine(modernDir, "thumbcache_32.db");
            File.WriteAllText(modernFile1, "dummy32");
            File.WriteAllText(modernFile2, "dummyidx");
            File.WriteAllText(nonCacheFile, "dummythumb");

            var discovered = SystemMaintenanceService.GetIconCacheFiles(tempDir);
            Assert.Contains(legacyFile, discovered);
            Assert.Contains(modernFile1, discovered);
            Assert.Contains(modernFile2, discovered);
            Assert.DoesNotContain(nonCacheFile, discovered);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void DeleteFiles_DeletesExistingFilesAndReportsCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "WinOpt_Delete_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var file1 = Path.Combine(tempDir, "file1.db");
            var file2 = Path.Combine(tempDir, "file2.db");
            var nonExistent = Path.Combine(tempDir, "file3.db");
            File.WriteAllText(file1, "a");
            File.WriteAllText(file2, "b");

            var (deleted, failed) = SystemMaintenanceService.DeleteFiles([file1, file2, nonExistent]);
            Assert.Equal(2, deleted);
            Assert.Empty(failed);
            Assert.False(File.Exists(file1));
            Assert.False(File.Exists(file2));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ResetTcpIpStack_ReturnsNeedsAdmin_WhenNotAdministrator()
    {
        var service = new SystemMaintenanceService(new CommandRunner(), isAdministrator: () => false);
        var result = await service.ResetTcpIpStackAsync();
        Assert.Equal(SystemToolOutcome.NeedsAdmin, result.Outcome);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ClearArpCache_ReturnsNeedsAdmin_WhenNotAdministrator()
    {
        var service = new SystemMaintenanceService(new CommandRunner(), isAdministrator: () => false);
        var result = await service.ClearArpCacheAsync();
        Assert.Equal(SystemToolOutcome.NeedsAdmin, result.Outcome);
        Assert.False(result.IsSuccess);
    }
}
