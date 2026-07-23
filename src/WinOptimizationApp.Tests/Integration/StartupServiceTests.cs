using Microsoft.Win32;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Integration;

public sealed class StartupServiceTests : IDisposable
{
    private const string TestEntryName = "__WinOptimizationApp_TestStartupEntry__";
    private const string TestCommand = @"C:\Windows\notepad.exe";

    public StartupServiceTests()
    {
        // Clean up any stale test values before starting
        CleanupRegistry();
    }

    public void Dispose()
    {
        // Clean up test values after run
        CleanupRegistry();
    }

    private static void CleanupRegistry()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            runKey?.DeleteValue(TestEntryName, throwOnMissingValue: false);
        }
        catch { }

        try
        {
            using var approvedKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", writable: true);
            approvedKey?.DeleteValue(TestEntryName, throwOnMissingValue: false);
        }
        catch { }
    }

    [Fact]
    public async Task ScanAsync_DetectsRegistryStartupEntriesAndCorrectlyChecksStatus()
    {
        // 1. Create a test registry startup entry (default: enabled)
        using (var runKey = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            RegistryKeyPermissionCheck.ReadWriteSubTree))
        {
            Assert.NotNull(runKey);
            runKey.SetValue(TestEntryName, TestCommand);
        }

        var service = new StartupService();
        var entriesBefore = await service.ScanAsync();
        var testEntryBefore = entriesBefore.FirstOrDefault(e => e.Name == TestEntryName && e.Source == "HKCU Run");

        Assert.NotNull(testEntryBefore);
        Assert.True(testEntryBefore.Enabled);
        Assert.Equal(TestCommand, testEntryBefore.Command);

        // 2. Disable the entry
        var disableResult = await service.DisableAsync(testEntryBefore);
        Assert.True(disableResult);

        // 3. Verify it is detected as disabled in scan
        var entriesAfterDisable = await service.ScanAsync();
        var testEntryAfterDisable = entriesAfterDisable.FirstOrDefault(e => e.Name == TestEntryName && e.Source == "HKCU Run");

        Assert.NotNull(testEntryAfterDisable);
        Assert.False(testEntryAfterDisable.Enabled);

        // 4. Enable it again
        var enableResult = await service.EnableAsync(testEntryAfterDisable);
        Assert.True(enableResult);

        // 5. Verify it is detected as enabled in scan
        var entriesAfterEnable = await service.ScanAsync();
        var testEntryAfterEnable = entriesAfterEnable.FirstOrDefault(e => e.Name == TestEntryName && e.Source == "HKCU Run");

        Assert.NotNull(testEntryAfterEnable);
        Assert.True(testEntryAfterEnable.Enabled);
    }
}
