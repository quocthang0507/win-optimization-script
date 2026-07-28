using System;
using System.IO;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempFile;

    public AppSettingsServiceTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public void Load_ReturnsDefaultSettings_WhenFileDoesNotExist()
    {
        // Arrange
        File.Delete(_tempFile); // Ensure it doesn't exist
        var service = new AppSettingsService(_tempFile);

        // Act
        var settings = service.Load();

        // Assert
        Assert.NotNull(settings);
        Assert.Null(settings.Theme); // Check default value
    }

    [Fact]
    public void SaveAndLoad_Succeeds_ForValidSettings()
    {
        // Arrange
        var service = new AppSettingsService(_tempFile);
        var settings = new AppSettings
        {
            Theme = AppTheme.Dark,
            Language = AppLanguage.Vietnamese,
            ProtectedPaths = [Path.Combine(Path.GetTempPath(), "important")],
            CustomWinapp2DatabasePath = Path.Combine(Path.GetTempPath(), "custom-winapp2.ini")
        };

        // Act
        var saved = service.Save(settings);
        var loaded = service.Load();

        // Assert
        Assert.True(saved);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(AppLanguage.Vietnamese, loaded.Language);
        Assert.Single(loaded.ProtectedPaths);
        Assert.Equal(settings.CustomWinapp2DatabasePath, loaded.CustomWinapp2DatabasePath);
    }

    [Fact]
    public void Load_ReturnsDefaultSettings_WhenFileIsCorrupted()
    {
        // Arrange
        File.WriteAllText(_tempFile, "{ invalid json ]");
        var service = new AppSettingsService(_tempFile);

        // Act
        var settings = service.Load();

        // Assert
        Assert.NotNull(settings);
        Assert.Null(settings.Theme);
        Assert.False(File.Exists(_tempFile));
        Assert.NotNull(service.RecoveredCorruptSettingsPath);
        Assert.True(File.Exists(service.RecoveredCorruptSettingsPath));
        File.Delete(service.RecoveredCorruptSettingsPath);
    }
}
