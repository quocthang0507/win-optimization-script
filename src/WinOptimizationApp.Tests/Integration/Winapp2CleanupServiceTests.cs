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

    [Fact]
    public async Task PreviewAsync_SkipsUserProtectedPaths()
    {
        var cache = Path.Combine(_root, "App", "cache");
        Directory.CreateDirectory(cache);
        var protectedFile = Path.Combine(cache, "keep.tmp");
        await File.WriteAllBytesAsync(protectedFile, new byte[32]);
        var entry = new CleanerEntry { Name = "Protected Example" };
        entry.FileKeys.Add(new FileKeyEntry { Path = cache, Extension = "*.tmp" });
        var service = new Winapp2CleanupService(new ReportService(new PathService(_root)));

        var preview = await service.PreviewAsync([entry], [cache]);

        Assert.Empty(preview.Candidates);
        Assert.Contains(preview.Warnings, warning => warning.Contains("user-protected", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(protectedFile));
    }

    [Fact]
    public async Task PreviewAsync_CustomDatabaseBlocksArbitraryNonDisposableDirectory()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Winapp2SafetyTests",
            Guid.NewGuid().ToString("N"),
            "UserContent");
        Directory.CreateDirectory(root);
        var importantFile = Path.Combine(root, "important.txt");
        await File.WriteAllTextAsync(importantFile, "keep");
        var entry = new CleanerEntry { Name = "Untrusted custom rule" };
        entry.FileKeys.Add(new FileKeyEntry { Path = root, Extension = "*", Recurse = true });
        var service = new Winapp2CleanupService(new ReportService(new PathService(_root)));

        try
        {
            var preview = await service.PreviewAsync([entry], restrictCustomDatabase: true);

            Assert.Empty(preview.Candidates);
            Assert.Contains(preview.Warnings, warning => warning.Contains("unsafe", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(importantFile));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Fact]
    public async Task PreviewAsync_CustomDatabaseAllowsRecognizedCacheDirectory()
    {
        var cache = Path.Combine(_root, "ExampleApp", "Cache");
        Directory.CreateDirectory(cache);
        var cacheFile = Path.Combine(cache, "session.tmp");
        await File.WriteAllTextAsync(cacheFile, "cache");
        var entry = new CleanerEntry { Name = "Trusted cache shape" };
        entry.FileKeys.Add(new FileKeyEntry { Path = cache, Extension = "*.tmp" });
        var service = new Winapp2CleanupService(new ReportService(new PathService(_root)));

        try
        {
            var preview = await service.PreviewAsync([entry], restrictCustomDatabase: true);

            Assert.Equal(cacheFile, Assert.Single(preview.Candidates).Path);
        }
        finally
        {
            Directory.Delete(Path.Combine(_root, "ExampleApp"), recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
