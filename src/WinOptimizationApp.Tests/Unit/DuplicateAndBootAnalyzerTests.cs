using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class DuplicateAndBootAnalyzerTests
{
    [Fact]
    public async Task DuplicateFileFinder_GroupsIdenticalFiles_AndMarksOldestAsOriginal()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"WinOptDupTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var payload = new byte[8192];
            new Random(42).NextBytes(payload);

            var olderFile = Path.Combine(tempRoot, "original-report.bin");
            var newerFile = Path.Combine(tempRoot, "copy-report.bin");
            var uniqueFile = Path.Combine(tempRoot, "unique.bin");

            await File.WriteAllBytesAsync(olderFile, payload);
            File.SetLastWriteTimeUtc(olderFile, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            await File.WriteAllBytesAsync(newerFile, payload);
            File.SetLastWriteTimeUtc(newerFile, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            await File.WriteAllBytesAsync(uniqueFile, [1, 2, 3, 4, 5, 6, 7, 8]);

            var finder = new DuplicateFileFinderService();
            var result = await finder.ScanAsync(tempRoot, minFileSizeBytes: 4);

            Assert.Single(result.Groups);
            var group = result.Groups[0];
            Assert.Equal(2, group.Files.Count);
            Assert.Equal(8192, group.FileSizeBytes);
            Assert.Equal(8192, group.ReclaimableBytes);

            var original = group.Files.Single(f => f.IsOriginal);
            var duplicate = group.Files.Single(f => !f.IsOriginal);

            Assert.Equal(olderFile, original.FullPath, ignoreCase: true);
            Assert.False(original.IsSelected);
            Assert.Equal(newerFile, duplicate.FullPath, ignoreCase: true);
            Assert.True(duplicate.IsSelected);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    [Fact]
    public void BuildSafeCleanupCandidates_NeverAllowsDeletingAllCopiesInGroup()
    {
        var group = new DuplicateFileGroup
        {
            Hash = "ABC123",
            FileSizeBytes = 4096,
            Files =
            [
                new DuplicateFileItem
                {
                    FullPath = @"C:\Temp\a.dat",
                    FileName = "a.dat",
                    DirectoryPath = @"C:\Temp",
                    SizeBytes = 4096,
                    LastModified = DateTimeOffset.UtcNow.AddDays(-2),
                    IsOriginal = true,
                    IsSelected = true // User manually checked even the original!
                },
                new DuplicateFileItem
                {
                    FullPath = @"C:\Temp\b.dat",
                    FileName = "b.dat",
                    DirectoryPath = @"C:\Temp",
                    SizeBytes = 4096,
                    LastModified = DateTimeOffset.UtcNow.AddDays(-1),
                    IsOriginal = false,
                    IsSelected = true
                }
            ]
        };

        var candidates = DuplicateFileFinderService.BuildSafeCleanupCandidates([group]);

        Assert.Single(candidates);
        Assert.Equal(@"C:\Temp\b.dat", candidates[0].SourcePath);
    }

    [Fact]
    public void BootTimeAnalyzer_ParsesDiagnosticEventXml_AndClassifiesSpeed()
    {
        const string sampleXml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System>
                <EventID>100</EventID>
                <TimeCreated SystemTime="2026-10-15T08:30:00.000Z" />
              </System>
              <EventData>
                <Data Name="BootTime">18400</Data>
                <Data Name="MainPathBootTime">11200</Data>
                <Data Name="BootPostBootTime">7200</Data>
              </EventData>
            </Event>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System>
                <EventID>100</EventID>
                <TimeCreated SystemTime="2026-10-14T08:30:00.000Z" />
              </System>
              <EventData>
                <Data Name="BootTime">24600</Data>
                <Data Name="MainPathBootTime">14000</Data>
                <Data Name="BootPostBootTime">10600</Data>
              </EventData>
            </Event>
            """;

        var summary = BootTimeAnalyzerService.ParseBootEventsXml(sampleXml);

        Assert.True(summary.IsAvailable);
        Assert.NotNull(summary.LastBoot);
        Assert.Equal(18.4, summary.LastBoot!.TotalBootSeconds);
        Assert.Equal(11.2, summary.LastBoot.MainPathSeconds);
        Assert.Equal(7.2, summary.LastBoot.PostBootSeconds);
        Assert.Equal(21.5, summary.AverageBootSeconds);
        Assert.Equal(BootSpeedRating.Fast, summary.Rating);
        Assert.Equal(2, summary.RecentBoots.Count);
    }

    [Theory]
    [InlineData(15.0, BootSpeedRating.Fast)]
    [InlineData(30.0, BootSpeedRating.Normal)]
    [InlineData(55.0, BootSpeedRating.Slow)]
    [InlineData(0.0, BootSpeedRating.Unknown)]
    public void ClassifyBootRating_ReturnsExpectedRating(double seconds, BootSpeedRating expected)
    {
        Assert.Equal(expected, BootTimeAnalyzerService.ClassifyBootRating(seconds));
    }

    [Fact]
    public void SmartAutopilot_ShouldTriggerOnlyWhenEnabledAndBelowThreshold()
    {
        var settings = new AppSettings
        {
            AutopilotEnabled = true,
            AutopilotCleanTempFiles = true,
            AutopilotMinFreeSpaceGb = 20
        };

        long tenGb = 10L * 1024L * 1024L * 1024L;
        long twentyFiveGb = 25L * 1024L * 1024L * 1024L;

        Assert.True(ScheduledMaintenanceService.ShouldTriggerLowDiskAutopilot(settings, tenGb));
        Assert.False(ScheduledMaintenanceService.ShouldTriggerLowDiskAutopilot(settings, twentyFiveGb));

        settings.AutopilotEnabled = false;
        Assert.False(ScheduledMaintenanceService.ShouldTriggerLowDiskAutopilot(settings, tenGb));
    }
}
