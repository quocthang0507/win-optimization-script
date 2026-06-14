using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class UninstallerServiceTests : IDisposable
{
    private const string TestAppName = "WinOptAppTestUninstallFolder";
    private readonly string _testDirPath;

    public UninstallerServiceTests()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _testDirPath = Path.Combine(localAppData, TestAppName);
        CleanupFolder();
    }

    public void Dispose()
    {
        CleanupFolder();
    }

    private void CleanupFolder()
    {
        try
        {
            if (Directory.Exists(_testDirPath))
            {
                Directory.Delete(_testDirPath, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task ScanLeftoversAsync_DetectsAppDataLeftoversForApp()
    {
        // 1. Create a dummy app directory in LocalAppData
        Directory.CreateDirectory(_testDirPath);
        File.WriteAllText(Path.Combine(_testDirPath, "dummy.txt"), "leftover file");

        var app = new InstalledApp
        {
            Id = "Registry\\WinOptAppTest",
            Name = TestAppName,
            Version = "1.0",
            Publisher = "WinOptPublisher",
            UninstallString = "dummy_uninstall.exe",
            InstallLocation = _testDirPath,
            Source = "Registry"
        };

        var commands = new CommandRunner();
        var service = new UninstallerService(commands);

        // 2. Scan leftovers
        var leftovers = await service.ScanLeftoversAsync(app);

        // 3. Verify it found our test directory
        Assert.NotNull(leftovers);
        Assert.Contains(_testDirPath, leftovers);

        // 4. Clean/Delete leftovers
        var deleteResult = await service.DeleteLeftoversAsync(leftovers.ToList());
        Assert.True(deleteResult);

        // 5. Verify the folder was actually deleted
        Assert.False(Directory.Exists(_testDirPath));
    }
}
