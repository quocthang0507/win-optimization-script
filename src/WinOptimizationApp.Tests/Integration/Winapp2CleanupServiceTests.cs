using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Integration;

public sealed class Winapp2CleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PreviewAndRun_SupportWildcardDeduplicationAndExclusions()
    {
        var cache = Path.Combine(_root, "App1", "cache");
        Directory.CreateDirectory(cache);
        var removable = Path.Combine(cache, "session.tmp");
        var protectedFile = Path.Combine(cache, "protected.tmp");
        var protectedDirectory = Path.Combine(cache, "keep");
        var protectedByPath = Path.Combine(protectedDirectory, "session.tmp");
        Directory.CreateDirectory(protectedDirectory);
        await File.WriteAllBytesAsync(removable, new byte[120]);
        await File.WriteAllBytesAsync(protectedFile, new byte[80]);
        await File.WriteAllBytesAsync(protectedByPath, new byte[40]);

        var entry = new CleanerEntry { Name = "Example" };
        entry.FileKeys.Add(new FileKeyEntry { Path = Path.Combine(_root, "App*", "cache"), Extension = "*.tmp", Recurse = true });
        entry.FileKeys.Add(new FileKeyEntry { Path = cache, Extension = "session.tmp" });
        entry.ExcludeKeys.Add(ExcludeKeyEntry.Parse($"FILE|{cache}|protected.*"));
        entry.ExcludeKeys.Add(ExcludeKeyEntry.Parse($"PATH|{protectedDirectory}"));
        entry.RegKeys.Add(new RegKeyEntry { Root = "HKCU", Key = "Software\\Example" });

        var service = new Winapp2CleanupService(new ReportService(new PathService(_root)));
        var preview = await service.PreviewAsync([entry]);

        var candidate = Assert.Single(preview.Candidates);
        Assert.Equal(removable, candidate.Path);
        Assert.Equal(120, preview.TotalBytes);
        Assert.Contains(preview.Warnings, warning => warning.Contains("registry rule", StringComparison.OrdinalIgnoreCase));

        var result = await service.RunAsync(preview, 1);
        Assert.True(result.Success);
        Assert.False(File.Exists(removable));
        Assert.True(File.Exists(protectedFile));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "logs"), "maintenance-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
