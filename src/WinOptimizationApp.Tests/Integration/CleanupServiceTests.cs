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
