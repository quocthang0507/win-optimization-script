using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Integration;

public sealed class DiskAnalysisServiceTests
{
    [Fact]
    public async Task ScanAsync_ComputesFolderSizesAndPercentOfParent()
    {
        using var fixture = TempDirectory.Create();
        var parent = Directory.CreateDirectory(Path.Combine(fixture.Path, "parent"));
        var child = Directory.CreateDirectory(Path.Combine(parent.FullName, "child"));
        await File.WriteAllBytesAsync(Path.Combine(parent.FullName, "parent.bin"), new byte[300]);
        await File.WriteAllBytesAsync(Path.Combine(child.FullName, "child.bin"), new byte[700]);

        var result = await new DiskAnalysisService().ScanAsync(new DiskScanOptions(fixture.Path));

        var parentItem = Assert.Single(result.Root.Children, item => item.Name == "parent");
        Assert.Equal(1000, result.TotalBytes);
        Assert.Equal(1000, parentItem.Size);
        Assert.Equal(100, parentItem.PercentOfParent);
        Assert.Equal(2, parentItem.FileCount);
        Assert.Equal(1, parentItem.FolderCount);
        Assert.Empty(parentItem.Children);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(3, result.FolderCount);
    }

    [Fact]
    public async Task ScanAsync_SkipsHiddenChildrenButStillScansRootWhenRootIsHidden()
    {
        using var fixture = TempDirectory.Create();
        var visibleFile = Path.Combine(fixture.Path, "visible.dat");
        var hiddenFile = Path.Combine(fixture.Path, "hidden.dat");
        await File.WriteAllBytesAsync(visibleFile, new byte[128]);
        await File.WriteAllBytesAsync(hiddenFile, new byte[256]);

        File.SetAttributes(fixture.Path, File.GetAttributes(fixture.Path) | FileAttributes.Hidden);
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);

        try
        {
            var result = await new DiskAnalysisService().ScanAsync(new DiskScanOptions(fixture.Path));

            Assert.Equal("Scanned", result.Root.ScanStatus);
            Assert.Equal(128, result.TotalBytes);
            Assert.Contains(result.Root.Children, item => item.Name == "visible.dat" && item.ScanStatus == "Scanned");
            Assert.Contains(result.Root.Children, item => item.Name == "hidden.dat" && item.ScanStatus == "Skipped");
        }
        finally
        {
            File.SetAttributes(hiddenFile, FileAttributes.Normal);
            File.SetAttributes(fixture.Path, FileAttributes.Directory);
        }
    }

    [Fact]
    public async Task ScanAsync_TracksLargestFilesAndFileTypes()
    {
        using var fixture = TempDirectory.Create();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "small.log"), new byte[10]);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "large.log"), new byte[90]);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "data.tmp"), new byte[40]);

        var result = await new DiskAnalysisService().ScanAsync(new DiskScanOptions(fixture.Path));

        Assert.Equal("large.log", result.LargestFiles[0].Name);
        var logSummary = Assert.Single(result.FileTypes, item => item.Extension == ".log");
        Assert.Equal(100, logSummary.TotalBytes);
        Assert.Equal(2, logSummary.Count);
    }

    [Fact]
    public async Task FlattenVisibleTree_ReturnsOnlyDirectChildrenAndExcludesRoot()
    {
        using var fixture = TempDirectory.Create();
        var directory = Directory.CreateDirectory(Path.Combine(fixture.Path, "content"));
        await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "nested.bin"), new byte[20]);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "root.bin"), new byte[10]);
        var service = new DiskAnalysisService();
        var result = await service.ScanAsync(new DiskScanOptions(fixture.Path));

        var items = service.FlattenVisibleTree(result.Root, 10);

        Assert.Contains(items, item => item.Name == "content" && item.IsDirectory);
        Assert.Contains(items, item => item.Name == "root.bin" && !item.IsDirectory);
        Assert.DoesNotContain(items, item => item.FullPath == result.Root.FullPath);
        Assert.DoesNotContain(items, item => item.Name == "nested.bin");
    }

    [Fact]
    public async Task ScanAsync_WhenCanceled_ReturnsPartialResultAndReportsSnapshots()
    {
        using var fixture = TempDirectory.Create();
        for (var directoryIndex = 0; directoryIndex < 12; directoryIndex++)
        {
            var directory = Directory.CreateDirectory(Path.Combine(fixture.Path, $"dir-{directoryIndex:00}"));
            for (var fileIndex = 0; fileIndex < 60; fileIndex++)
            {
                await File.WriteAllBytesAsync(Path.Combine(directory.FullName, $"file-{fileIndex:00}.dat"), new byte[16]);
            }
        }

        using var cts = new CancellationTokenSource();
        var partialProgressCount = 0;
        var progress = new SynchronousProgress<DiskScanProgress>(value =>
        {
            if (value.PartialResult is not null)
            {
                partialProgressCount++;
                cts.Cancel();
            }
        });

        var result = await new DiskAnalysisService().ScanAsync(new DiskScanOptions(fixture.Path), progress, cts.Token);

        Assert.True(result.IsPartial);
        Assert.True(result.TotalBytes > 0);
        Assert.True(partialProgressCount > 0);
        Assert.NotEmpty(result.Root.Children);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            foreach (var directory in Directory.EnumerateDirectories(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(directory, FileAttributes.Directory);
            }

            Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value)
        {
            handler(value);
        }
    }
}
