using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class StorageCleanupServiceTests
{
    [Fact]
    public void CreateCandidates_IncludesTempLogDumpBackupFilesAndCacheFolders()
    {
        var result = CreateScanResult(
            largestFiles:
            [
                CreateFile("session.tmp", 10),
                CreateFile("trace.log", 20),
                CreateFile("crash.dmp", 30),
                CreateFile("backup.bak", 40),
                CreateFile("notes.txt", 100)
            ],
            rootChildren:
            [
                CreateDirectory("browser-cache", 500),
                CreateDirectory("plain-folder", 600)
            ]);

        var candidates = StorageCleanupService.CreateCandidates(result);

        Assert.Contains(candidates, item => item.Label == "session.tmp" && item.RiskLevel == RiskLevel.Safe && item.Reason == "Temporary file.");
        Assert.Contains(candidates, item => item.Label == "trace.log" && item.RiskLevel == RiskLevel.Safe && item.Reason == "Log file.");
        Assert.Contains(candidates, item => item.Label == "crash.dmp" && item.RiskLevel == RiskLevel.Medium && item.Reason == "Crash dump.");
        Assert.Contains(candidates, item => item.Label == "backup.bak" && item.RiskLevel == RiskLevel.Medium && item.Reason == "Backup-like file.");
        Assert.Contains(candidates, item => item.Label == "browser-cache" && item.IsDirectory && item.RiskLevel == RiskLevel.Medium);
        Assert.DoesNotContain(candidates, item => item.Label == "notes.txt");
        Assert.DoesNotContain(candidates, item => item.Label == "plain-folder");
    }

    [Fact]
    public void CreateCandidates_TreatsFileExtensionsCaseInsensitively()
    {
        var result = CreateScanResult(
            largestFiles:
            [
                CreateFile("UPPER.LOG", 64, extension: ".LOG"),
                CreateFile("UPPER.TMP", 32, extension: ".TMP")
            ],
            rootChildren: []);

        var candidates = StorageCleanupService.CreateCandidates(result);

        Assert.Contains(candidates, item => item.Label == "UPPER.LOG" && item.RiskLevel == RiskLevel.Safe && item.Reason == "Log file.");
        Assert.Contains(candidates, item => item.Label == "UPPER.TMP" && item.RiskLevel == RiskLevel.Safe && item.Reason == "Temporary file.");
    }

    [Fact]
    public void CreateCandidates_DoesNotSuggestInstallersOrArchivesOutsideDownloads()
    {
        var result = CreateScanResult(
            largestFiles:
            [
                CreateFile("setup.exe", 100),
                CreateFile("archive.zip", 120),
                CreateFile("bundle.msi", 140)
            ],
            rootChildren: []);

        var candidates = StorageCleanupService.CreateCandidates(result);

        Assert.Empty(candidates);
    }

    [Fact]
    public void CreateCandidates_DoesNotTreatDownloadsSiblingFolderAsDownloads()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(userProfile, "DownloadsBackup", "archive.zip");
        var result = CreateScanResult(
            largestFiles:
            [
                CreateFile("archive.zip", 120, fullPath: path)
            ],
            rootChildren: []);

        var candidates = StorageCleanupService.CreateCandidates(result);

        Assert.Empty(candidates);
    }

    [Fact]
    public void CreateCandidates_OrdersAndLimitsFileAndFolderCandidates()
    {
        var files = Enumerable.Range(0, 40)
            .Select(index => CreateFile($"trace-{index:00}.log", index + 1))
            .ToList();
        var folders = Enumerable.Range(0, 20)
            .Select(index => CreateDirectory($"cache-{index:00}", index + 1))
            .ToList();
        var result = CreateScanResult(files, folders);

        var candidates = StorageCleanupService.CreateCandidates(result);

        var fileCandidates = candidates.Where(item => !item.IsDirectory).ToList();
        var folderCandidates = candidates.Where(item => item.IsDirectory).ToList();
        Assert.Equal(30, fileCandidates.Count);
        Assert.Equal(12, folderCandidates.Count);
        Assert.Equal("trace-39.log", fileCandidates[0].Label);
        Assert.Equal("cache-19", folderCandidates[0].Label);
    }

    private static DiskScanResult CreateScanResult(IReadOnlyList<DiskItem> largestFiles, IReadOnlyList<DiskItem> rootChildren)
    {
        var root = CreateDirectory("root", rootChildren.Sum(item => item.Size));
        foreach (var child in rootChildren)
        {
            root.Children.Add(child);
        }

        return new DiskScanResult(
            root,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            root.Size + largestFiles.Sum(item => item.Size),
            largestFiles.Count,
            rootChildren.Count,
            0,
            [],
            largestFiles,
            [],
            false);
    }

    private static DiskItem CreateFile(string name, long size, string? extension = null, string? fullPath = null)
    {
        return new DiskItem
        {
            Name = name,
            FullPath = fullPath ?? Path.Combine("C:\\scan-root", name),
            IsDirectory = false,
            Size = size,
            AllocatedSize = size,
            FileCount = 1,
            Extension = extension ?? Path.GetExtension(name),
            LastModified = DateTimeOffset.Now,
            ScanStatus = "Scanned"
        };
    }

    private static DiskItem CreateDirectory(string name, long size)
    {
        return new DiskItem
        {
            Name = name,
            FullPath = Path.Combine("C:\\scan-root", name),
            IsDirectory = true,
            Size = size,
            AllocatedSize = size,
            FileCount = 1,
            FolderCount = 0,
            LastModified = DateTimeOffset.Now,
            ScanStatus = "Scanned"
        };
    }
}
