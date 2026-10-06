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

    [Fact]
    public void SaveAndLoad_PersistsScheduleAndRetention()
    {
        var service = new AppSettingsService(_tempFile);
        var settings = new AppSettings
        {
            ScheduleFrequency = MaintenanceScheduleFrequency.Weekly,
            ScheduleTime = new TimeSpan(21, 45, 0),
            ScheduleDayOfWeek = DayOfWeek.Saturday,
            ScheduledMaintenanceTaskIds = ["cleanup.temp", "CLEANUP.TEMP", " ", "network.dns"],
            ReportRetentionDays = 90
        };

        Assert.True(service.Save(settings));
        var loaded = service.Load();

        Assert.Equal(MaintenanceScheduleFrequency.Weekly, loaded.ScheduleFrequency);
        Assert.Equal(new TimeSpan(21, 45, 0), loaded.ScheduleTime);
        Assert.Equal(DayOfWeek.Saturday, loaded.ScheduleDayOfWeek);
        Assert.Equal(["cleanup.temp", "network.dns"], loaded.ScheduledMaintenanceTaskIds);
        Assert.Equal(90, loaded.ReportRetentionDays);
    }

    [Fact]
    public void Load_NormalizesOutOfRangeScheduleAndRetentionValues()
    {
        File.WriteAllText(_tempFile, """
            { "ScheduleTime": "1.02:00:00", "ReportRetentionDays": 45, "ScheduledMaintenanceTaskIds": null }
            """);

        var loaded = new AppSettingsService(_tempFile).Load();

        Assert.Equal(new TimeSpan(12, 0, 0), loaded.ScheduleTime);
        Assert.Equal(0, loaded.ReportRetentionDays);
        Assert.Empty(loaded.ScheduledMaintenanceTaskIds);
    }

    [Fact]
    public void Load_DefaultsToConservativeSchedule()
    {
        File.Delete(_tempFile);

        var loaded = new AppSettingsService(_tempFile).Load();

        Assert.Equal(MaintenanceScheduleFrequency.Off, loaded.ScheduleFrequency);
        Assert.Equal(["cleanup.temp"], loaded.ScheduledMaintenanceTaskIds);
        Assert.Equal(0, loaded.ReportRetentionDays);
    }

    [Fact]
    public void ExportAndImport_RoundTripPreferencesButKeepLocalWindowSize()
    {
        var exported = new AppSettings
        {
            Language = AppLanguage.Vietnamese,
            Theme = AppTheme.Dark,
            WindowWidth = 3000,
            WindowHeight = 2000,
            ProtectedPaths = [@"D:\Projects"],
            ScheduleFrequency = MaintenanceScheduleFrequency.Daily,
            ReportRetentionDays = 30
        };
        var current = new AppSettings { WindowWidth = 1180, WindowHeight = 760 };

        Assert.True(AppSettingsService.Export(exported, _tempFile));
        Assert.True(AppSettingsService.TryImport(_tempFile, current, out var imported));

        Assert.NotNull(imported);
        Assert.Equal(AppLanguage.Vietnamese, imported.Language);
        Assert.Equal(AppTheme.Dark, imported.Theme);
        Assert.Equal(MaintenanceScheduleFrequency.Daily, imported.ScheduleFrequency);
        Assert.Equal(30, imported.ReportRetentionDays);
        Assert.Single(imported.ProtectedPaths);
        Assert.Equal(1180, imported.WindowWidth);
        Assert.Equal(760, imported.WindowHeight);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    public void TryImport_RejectsInvalidFiles(string content)
    {
        File.WriteAllText(_tempFile, content);

        Assert.False(AppSettingsService.TryImport(_tempFile, new AppSettings(), out var imported));
        Assert.Null(imported);
    }

    [Fact]
    public void TryImport_RejectsMissingFile()
    {
        File.Delete(_tempFile);

        Assert.False(AppSettingsService.TryImport(_tempFile, new AppSettings(), out _));
    }
}
