using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class CloudStorageDetectorTests
{
    [Fact]
    public void Detect_FindsOneDriveFromEnvironment()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Users\test\OneDrive"
        };

        var locations = CloudStorageDetector.Detect(
            new Dictionary<string, string?> { ["OneDrive"] = @"C:\Users\test\OneDrive" },
            @"C:\Users\test",
            existing.Contains);

        Assert.Contains(locations, location =>
            location.Provider == CloudStorageProvider.OneDrive &&
            location.IsDetected &&
            location.Path == @"C:\Users\test\OneDrive");
    }

    [Fact]
    public void Detect_DeduplicatesOneDriveEnvironmentPaths()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Users\test\OneDrive"
        };

        var locations = CloudStorageDetector.Detect(
            new Dictionary<string, string?>
            {
                ["OneDrive"] = @"C:\Users\test\OneDrive",
                ["OneDriveConsumer"] = @"C:\Users\test\OneDrive\"
            },
            @"C:\Users\test",
            existing.Contains);

        Assert.Single(locations, location => location.Provider == CloudStorageProvider.OneDrive && location.IsDetected);
    }

    [Fact]
    public void Detect_FindsGoogleDriveAndDropboxFromKnownUserFolders()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Users\test\Google Drive",
            @"C:\Users\test\Dropbox"
        };

        var locations = CloudStorageDetector.Detect(
            new Dictionary<string, string?>(),
            @"C:\Users\test",
            existing.Contains);

        Assert.Contains(locations, location => location.Provider == CloudStorageProvider.GoogleDrive && location.IsDetected);
        Assert.Contains(locations, location => location.Provider == CloudStorageProvider.Dropbox && location.IsDetected);
    }

    [Fact]
    public void Detect_ReturnsMissingProviderRowsWhenSyncFoldersAreNotFound()
    {
        var locations = CloudStorageDetector.Detect(
            new Dictionary<string, string?>(),
            @"C:\Users\test",
            _ => false);

        Assert.Contains(locations, location => location.Provider == CloudStorageProvider.OneDrive && !location.IsDetected);
        Assert.Contains(locations, location => location.Provider == CloudStorageProvider.GoogleDrive && !location.IsDetected);
        Assert.Contains(locations, location => location.Provider == CloudStorageProvider.Dropbox && !location.IsDetected);
    }
}
