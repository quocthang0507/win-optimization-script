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
}
