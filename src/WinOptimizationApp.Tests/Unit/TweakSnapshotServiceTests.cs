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
}
