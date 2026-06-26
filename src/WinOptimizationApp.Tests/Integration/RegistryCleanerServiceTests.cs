using Microsoft.Win32;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Integration;

public sealed class RegistryCleanerServiceTests : IDisposable
{
    private const string TestExtension = ".winoptapptest";
    private const string TestProgId = "WinOptAppTestMissingProgID";

    public RegistryCleanerServiceTests()
    {
        CleanupRegistry();
    }

    public void Dispose()
    {
        CleanupRegistry();
    }

    private static void CleanupRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            key?.DeleteSubKeyTree(TestExtension, throwOnMissingSubKey: false);
        }
        catch { }
    }

    [Fact]
    public async Task ScanAndClean_DetectsAndCleansFileExtensionRegistryIssue()
    {
        // 1. Create a dummy file extension key in HKCU referring to a missing ProgID
        using (var classesKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true))
        {
            Assert.NotNull(classesKey);
            using var extKey = classesKey.CreateSubKey(TestExtension);
            extKey.SetValue("", TestProgId);
        }

        var service = new RegistryCleanerService();
        var issues = await service.ScanAsync();

        // 2. Verify that our test issue is detected
        var targetIssue = issues.FirstOrDefault(i => i.KeyPath.EndsWith(TestExtension) && i.Category == "File Extensions");
        Assert.NotNull(targetIssue);
        Assert.Equal(TestProgId, targetIssue.ValueData);

        // 3. Perform clean (this should also create a backup .reg file)
        targetIssue.IsSelected = true;
        var cleanResult = await service.CleanAsync([targetIssue]);
        Assert.True(cleanResult);

        // 4. Verify that the key was deleted from registry
        using (var checkKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{TestExtension}"))
        {
            Assert.Null(checkKey);
        }

        // 5. Verify that a backup .reg file was created in backups directory
        var pathService = new PathService();
        var backupsDir = pathService.BackupsDirectory;
        Assert.True(Directory.Exists(backupsDir));

        var regFiles = Directory.GetFiles(backupsDir, "registry-backup-*.reg");
        Assert.NotEmpty(regFiles);

        // Verify content of the latest backup file contains the registry details
        var latestFile = regFiles.OrderByDescending(File.GetCreationTime).First();
        var fileContent = File.ReadAllText(latestFile, System.Text.Encoding.Unicode);
        Assert.Contains(TestExtension, fileContent);
        Assert.Contains(TestProgId, fileContent);
    }
}
