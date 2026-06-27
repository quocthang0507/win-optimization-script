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
            Language = AppLanguage.Vietnamese
        };

        // Act
        var saved = service.Save(settings);
        var loaded = service.Load();

        // Assert
        Assert.True(saved);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(AppLanguage.Vietnamese, loaded.Language);
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
    }
}
