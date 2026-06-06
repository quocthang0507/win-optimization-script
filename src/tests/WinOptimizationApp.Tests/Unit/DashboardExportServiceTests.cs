using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class DashboardExportServiceTests
{
    [Fact]
    public void FormatMarkdown_IncludesSystemRuntimeAndDriveSections()
    {
        var status = new DashboardStatus(
            "Windows 11 (X64)",
            "DESKTOP-TEST",
            "tester",
            true,
            TimeSpan.FromHours(5),
            "C:\\",
            500,
            1000,
            false,
            true,
            "logs/maintenance.json",
            "Test CPU",
            8,
            ".NET 10.0",
            "X64",
            "X64",
            42,
            16L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024,
            32L * 1024 * 1024 * 1024,
            20L * 1024 * 1024 * 1024,
            [new DashboardDriveStatus("C:\\", "Fixed", "NTFS", "System", 1000, 500)]);

        var markdown = DashboardExportService.FormatMarkdown(status, new DateTimeOffset(2026, 6, 6, 10, 30, 0, TimeSpan.Zero));

        Assert.Contains("# Windows System Maintenance Dashboard", markdown, StringComparison.Ordinal);
        Assert.Contains("## Health Check", markdown, StringComparison.Ordinal);
        Assert.Contains("- **Score:**", markdown, StringComparison.Ordinal);
        Assert.Contains("## System", markdown, StringComparison.Ordinal);
        Assert.Contains("- **Machine:** DESKTOP-TEST", markdown, StringComparison.Ordinal);
        Assert.Contains("## Hardware and Runtime", markdown, StringComparison.Ordinal);
        Assert.Contains("Test CPU", markdown, StringComparison.Ordinal);
        Assert.Contains("## Drives", markdown, StringComparison.Ordinal);
        Assert.Contains("| C:\\ | Fixed | NTFS | System |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMarkdown_UsesVietnameseLabelsWhenRequested()
    {
        var status = new DashboardStatus(
            "Windows 11 (X64)",
            "DESKTOP-TEST",
            "tester",
            true,
            TimeSpan.FromHours(5),
            "C:\\",
            2L * 1024 * 1024 * 1024,
            100L * 1024 * 1024 * 1024,
            false,
            true,
            null,
            "Test CPU",
            8,
            ".NET 10.0",
            "X64",
            "X64",
            42,
            16L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024,
            32L * 1024 * 1024 * 1024,
            20L * 1024 * 1024 * 1024,
            []);

        var markdown = DashboardExportService.FormatMarkdown(
            status,
            new DateTimeOffset(2026, 6, 6, 10, 30, 0, TimeSpan.Zero),
            AppLanguage.Vietnamese);

        Assert.Contains("# Báo cáo tổng quan bảo trì Windows", markdown, StringComparison.Ordinal);
        Assert.Contains("## Kiểm tra sức khỏe", markdown, StringComparison.Ordinal);
        Assert.Contains("Xem dung lượng lưu trữ", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveMarkdownAsync_WritesDashboardReportToLogsDirectory()
    {
        using var fixture = TempDirectory.Create();
        var status = new DashboardStatus(
            "Windows 11 (X64)",
            "DESKTOP-TEST",
            "tester",
            false,
            TimeSpan.FromMinutes(30),
            "C:\\",
            500,
            1000,
            true,
            false,
            null,
            "Test CPU",
            4,
            ".NET 10.0",
            "X64",
            "X64",
            55,
            8000,
            3000,
            12000,
            7000,
            []);

        var path = await DashboardExportService.SaveMarkdownAsync(status, fixture.Path);

        Assert.True(File.Exists(path));
        Assert.StartsWith(fixture.Path, path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dashboard-", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Health Check", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        Assert.Contains("Pending reboot", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
