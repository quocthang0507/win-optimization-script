using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class Winapp2ServiceTests
{
    [Fact]
    public async Task GetDetectedEntriesAsync_UsesCompatibleCustomDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "custom.ini");
        var cachePath = Path.Combine(root, "cache");
        Directory.CreateDirectory(cachePath);
        await File.WriteAllTextAsync(databasePath, $"""
            [Custom Test Cache]
            DetectFile={cachePath}
            Default=True
            FileKey1={cachePath}|*.tmp
            """);

        try
        {
            var entries = await new Winapp2Service().GetDetectedEntriesAsync(databasePath);

            var entry = Assert.Single(entries);
            Assert.Equal("Custom Test Cache", entry.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
