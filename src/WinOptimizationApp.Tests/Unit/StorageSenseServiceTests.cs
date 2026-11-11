using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class StorageSenseServiceTests
{
    [Fact]
    public void ConfigureStorageSense_UpdatesSafePolicies_AndNeverEnablesDownloadsDeletion()
    {
        var registry = new InMemoryRegistryAccessor();
        var service = new StorageSenseService(new CommandRunner(), registry);

        var initial = service.GetStorageSenseState();
        Assert.False(initial.IsEnabled);

        var updated = service.ConfigureStorageSense(
            enabled: true,
            cleanTemporaryFiles: true,
            cleanRecycleBin: true,
            recycleBinThresholdDays: 14,
            runFrequencyDays: 7);

        Assert.True(updated.IsEnabled);
        Assert.True(updated.CleanTemporaryFiles);
        Assert.True(updated.CleanRecycleBin);
        Assert.Equal(14, updated.RecycleBinThresholdDays);
        Assert.Equal(7, updated.RunFrequencyDays);

        // Downloads deletion policy ("32") must never be automatically enabled
        var downloadsSnapshot = registry.ReadValue(
            StorageSenseService.StoragePolicyHive,
            StorageSenseService.StoragePolicySubKey,
            "32");
        Assert.False(downloadsSnapshot.Exists);
    }

    [Fact]
    public void ParseCompactOsQueryOutput_DetectsCompressedAndUncompressedStates()
    {
        var compressed = StorageSenseService.ParseCompactOsQueryOutput(
            "The system is in the Compact state. It will remain in this state unless an administrator changes it.",
            string.Empty,
            0);
        Assert.True(compressed.IsSupported);
        Assert.True(compressed.IsCompressed);

        var uncompressed = StorageSenseService.ParseCompactOsQueryOutput(
            "The system is not in the Compact state because Windows has determined that it is not beneficial for this system.",
            string.Empty,
            0);
        Assert.True(uncompressed.IsSupported);
        Assert.False(uncompressed.IsCompressed);
    }
}
