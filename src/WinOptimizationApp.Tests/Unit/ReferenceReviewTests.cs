using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ReferenceReviewTests
{
    [Theory]
    [InlineData(true, null, false, true)]
    [InlineData(true, "Closes sessions", false, false)]
    [InlineData(true, null, true, false)]
    [InlineData(false, null, false, false)]
    public void Winapp2_RiskyOrCustomEntriesStayOptIn(bool selected, string? warning, bool custom, bool expected)
    {
        var entry = new CleanerEntry { Default = selected, Warning = warning };
        Assert.Equal(expected, entry.CanSelectByDefault(custom));
    }

    [Fact]
    public void StorageFilter_FindsItemsBeyondFormer400ItemLimit()
    {
        var items = Enumerable.Range(0, 500).Select(index => new DiskItem
        {
            Name = $"item-{index}.log", FullPath = $"C:\\data\\item-{index}.log", Size = 500 - index
        });
        Assert.Equal("item-499.log", Assert.Single(DiskAnalysisService.FilterItems(items, "ITEM-499").Take(160)).Name);
    }

    [Fact]
    public void StorageFilter_CombinesPathTypeAndMinimumSize()
    {
        DiskItem[] items =
        [
            new() { Name = "a", FullPath = "C:\\logs\\a", Size = 200, IsDirectory = true },
            new() { Name = "b", FullPath = "C:\\logs\\b", Size = 200 },
            new() { Name = "c", FullPath = "C:\\logs\\c", Size = 20, IsDirectory = true }
        ];
        Assert.Equal("a", Assert.Single(DiskAnalysisService.FilterItems(items, "LOGS", true, 100)).Name);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, true)]
    [InlineData(false, 1, true)]
    public void StorageTotals_MarkPartialOrExcludedResults(bool partial, int skipped, bool incomplete)
    {
        var result = new DiskScanResult(new DiskItem { Name = "root", FullPath = "C:\\root" },
            DateTimeOffset.Now, DateTimeOffset.Now, 0, 0, 0, skipped, [], [], [], partial);
        Assert.Equal(incomplete, result.HasIncompleteTotals);
    }

    [Fact]
    public void StorageFilter_SupportsWildcardSizeAndAgeExpressions()
    {
        var now = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
        DiskItem[] items =
        [
            new() { Name = "ubuntu.iso", FullPath = @"C:\Downloads\ubuntu.iso", Size = 200 * 1024 * 1024L, LastModified = now.AddDays(-120) },
            new() { Name = "tiny.iso", FullPath = @"C:\Downloads\tiny.iso", Size = 2 * 1024 * 1024L, LastModified = now.AddDays(-120) },
            new() { Name = "fresh.iso", FullPath = @"C:\Downloads\fresh.iso", Size = 300 * 1024 * 1024L, LastModified = now.AddDays(-5) },
            new() { Name = "archive.zip", FullPath = @"C:\Downloads\archive.zip", Size = 500 * 1024 * 1024L, LastModified = now.AddDays(-200) }
        ];

        var matches = DiskAnalysisService.FilterItems(items, "*.iso > 50MB > 30d", referenceTime: now).ToList();
        var match = Assert.Single(matches);
        Assert.Equal("ubuntu.iso", match.Name);
    }

    [Fact]
    public async Task SearchRecursive_AndBookmarks_WorkAcrossNestedScanTree()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var nestedDir = Path.Combine(tempRoot, "level1", "level2", "deep");
            Directory.CreateDirectory(nestedDir);
            var deepFile = Path.Combine(nestedDir, "deep-target.iso");
            await File.WriteAllBytesAsync(deepFile, new byte[4096]);

            var service = new DiskAnalysisService();
            var scanResult = await service.ScanAsync(new DiskScanOptions(tempRoot));

            var recursiveMatches = service.SearchRecursive(scanResult, "*.iso > 1KB");
            var found = Assert.Single(recursiveMatches);
            Assert.Equal("deep-target.iso", found.Name);

            var bookmarks = DiskAnalysisService.ToggleBookmark([], nestedDir, out var added);
            Assert.True(added);
            Assert.True(DiskAnalysisService.IsBookmarked(nestedDir, bookmarks));

            bookmarks = DiskAnalysisService.ToggleBookmark(bookmarks, nestedDir, out var stillAdded);
            Assert.False(stillAdded);
            Assert.False(DiskAnalysisService.IsBookmarked(nestedDir, bookmarks));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
