using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class TweakSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveListAndDelete_RoundTripsSnapshot()
    {
        var service = new TweakSnapshotService(new PathService(_root));
        var values = new Dictionary<string, bool> { ["privacy.telemetry"] = false };

        var path = await service.SaveAsync("Before applying telemetry", values);
        var saved = Assert.Single(service.GetSnapshots());

        Assert.Equal(path, saved.Path);
        Assert.Equal("Before applying telemetry", saved.Snapshot.Label);
        Assert.False(saved.Snapshot.Values["privacy.telemetry"]);
        Assert.True(service.Delete(saved.Path));
        Assert.Empty(service.GetSnapshots());
    }

    [Fact]
    public void Delete_RejectsFileOutsideBackupDirectory()
    {
        var service = new TweakSnapshotService(new PathService(_root));
        Assert.False(service.Delete(Path.Combine(Path.GetTempPath(), "tweak-snapshot-outside.json")));
    }

    [Fact]
    public async Task GetSnapshots_StopsAfterRequestedValidSnapshotCount()
    {
        var service = new TweakSnapshotService(new PathService(_root));
        for (var index = 0; index < 5; index++)
        {
            await service.SaveAsync($"Snapshot {index}", new Dictionary<string, bool> { [$"tweak.{index}"] = true });
        }

        var snapshots = service.GetSnapshots(2);

        Assert.Equal(2, snapshots.Count);
        Assert.Empty(service.GetSnapshots(0));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Values\":null}")]
    [InlineData("{\"Values\":{\"a\":true,\"A\":false}}")]
    [InlineData("{\"Values\":{\"a\":\"true\"}}")]
    [InlineData("{\"Values\":{\"a\":true}}")]
    public async Task GetSnapshots_SkipsMalformedSnapshotsWithoutHidingValidOnes(string invalidJson)
    {
        var paths = new PathService(_root);
        var service = new TweakSnapshotService(paths);
        await service.SaveAsync("Valid", new Dictionary<string, bool> { ["a"] = false });
        await File.WriteAllTextAsync(Path.Combine(paths.BackupsDirectory, "tweak-snapshot-zzz.json"), invalidJson);
        Assert.Equal("Valid", Assert.Single(service.GetSnapshots()).Snapshot.Label);
    }

    [Fact]
    public async Task CaptureAndRestoreRegistryEntries_RestoresExactTypedValuesAndDeletesPreviouslyMissingOnes()
    {
        var paths = new PathService(_root);
        var registry = new InMemoryRegistryAccessor();
        // Before tweak: AllowTelemetry had OEM value 3 (not removed), and DisableSearchBoxSuggestions did not exist.
        registry.WriteValue("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", "DWord", "3");

        var snapshotService = new TweakSnapshotService(paths, registry);
        var values = new Dictionary<string, bool>
        {
            ["privacy.telemetry"] = false,
            ["privacy.webSearch"] = false
        };

        var capturedEntries = snapshotService.CaptureRegistryEntries(["privacy.telemetry", "privacy.webSearch"], TweakService.Tweaks);
        var path = await snapshotService.SaveAsync("Typed backup", values, capturedEntries);
        Assert.NotNull(path);

        // Simulate applying tweaks: AllowTelemetry becomes 0, DisableSearchBoxSuggestions becomes 1
        registry.WriteValue("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", "DWord", "0");
        registry.WriteValue("HKCU", @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", "DWord", "1");

        var saved = Assert.Single(snapshotService.GetSnapshots()).Snapshot;
        Assert.NotNull(saved.RegistryEntries);
        Assert.Equal(2, saved.RegistryEntries!.Count);

        snapshotService.RestoreRegistryEntries(saved);

        var restoredTelemetry = registry.ReadValue("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
        Assert.True(restoredTelemetry.Exists);
        Assert.Equal("DWord", restoredTelemetry.ValueKind);
        Assert.Equal("3", restoredTelemetry.RawValue);

        var restoredWebSearch = registry.ReadValue("HKCU", @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
        Assert.False(restoredWebSearch.Exists);
    }

    [Fact]
    public async Task PruneSnapshots_RemovesSnapshotsOlderThanRetentionDays()
    {
        var paths = new PathService(_root);
        var service = new TweakSnapshotService(paths);
        var oldPath = await service.SaveAsync("Old snapshot", new Dictionary<string, bool> { ["privacy.telemetry"] = false });
        var recentPath = await service.SaveAsync("Recent snapshot", new Dictionary<string, bool> { ["privacy.webSearch"] = true });

        Assert.NotNull(oldPath);
        Assert.NotNull(recentPath);
        File.SetLastWriteTimeUtc(oldPath!, DateTime.UtcNow.AddDays(-45));

        var deleted = service.PruneSnapshots(30);
        Assert.Equal(1, deleted);
        var remaining = Assert.Single(service.GetSnapshots());
        Assert.Equal("Recent snapshot", remaining.Snapshot.Label);
    }
}
