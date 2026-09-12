using System;
using System.IO;
using Xunit;
using WinOptimizationApp.Services;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Tests.Integration;

public class CleanupServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly CleanupService _service;

    public CleanupServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WinOptCleanupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _service = new CleanupService(new CommandRunner());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void PreviewTarget_ReturnsCorrectMetrics_ForValidDirectory()
    {
        // Arrange
        var file1 = Path.Combine(_tempDirectory, "test1.txt");
        var file2 = Path.Combine(_tempDirectory, "test2.log");
        File.WriteAllText(file1, "Hello World"); // 11 bytes
        File.WriteAllText(file2, "Another File"); // 12 bytes

        // Act
        var result = CleanupService.PreviewTarget("TestTarget", _tempDirectory);

        // Assert
        Assert.Equal("TestTarget", result.Name);
        Assert.Equal(_tempDirectory, result.Path);
        Assert.True(result.Exists);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(23, result.Bytes);
        Assert.Equal("Ready", result.Status);
    }

    [Fact]
    public void DeleteContents_PreservesRecentAndLockedFiles_ReportsActualBytes()
    {
        var old = Path.Combine(_tempDirectory, "old.tmp");
        var recent = Path.Combine(_tempDirectory, "recent.tmp");
        var locked = Path.Combine(_tempDirectory, "locked.tmp");
        foreach (var path in new[] { old, recent, locked }) File.WriteAllText(path, "sample");
        foreach (var path in new[] { old, locked })
        {
            File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));
        }
        using var heldFile = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var errors = new List<string>();
        var target = new CleanupService.CleanupTargetDefinition("fixture", _tempDirectory, "*", TimeSpan.FromDays(7));
        var result = CleanupService.DeleteContents(target, errors, default);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(locked));
        Assert.Equal(1, result.Removed);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(6, result.RemovedBytes);
    }

    [Fact]
    public void DeleteContents_RechecksAgeAfterPreview()
    {
        var path = Path.Combine(_tempDirectory, "changed.tmp");
        File.WriteAllText(path, "sample");
        File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));
        var target = new CleanupService.CleanupTargetDefinition("fixture", _tempDirectory, "*", TimeSpan.FromDays(7));
        Assert.Equal(1, CleanupService.PreviewTarget(target, default).FileCount);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        var result = CleanupService.DeleteContents(target, [], default);
        Assert.True(File.Exists(path));
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void DefaultTempTargets_RequireSevenDays_AndDumpsRequireFourteenDays()
    {
        Assert.All(CleanupService.GetTargets("cleanup.temp"), target => Assert.Equal(TimeSpan.FromDays(7), target.MinimumAge));
        Assert.All(CleanupService.GetTargets("cleanup.crashdumps"), target =>
        {
            Assert.Equal(TimeSpan.FromDays(14), target.MinimumAge);
            Assert.Equal("*.dmp", target.Pattern);
        });
    }

    [Fact]
    public void PreviewTarget_ReturnsExistsFalse_ForMissingDirectory()
    {
        // Arrange
        var missingPath = Path.Combine(_tempDirectory, "DoesNotExist");

        // Act
        var result = CleanupService.PreviewTarget("MissingTarget", missingPath);

        // Assert
        Assert.False(result.Exists);
        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.Bytes);
    }
}
