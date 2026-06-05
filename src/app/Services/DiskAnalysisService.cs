using System.Collections.Concurrent;
using System.IO.Enumeration;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class DiskAnalysisService
{
    private const int ProgressInterval = 250;
    private const int LargestFileLimit = 250;

    public Task<DiskScanResult> ScanAsync(
        DiskScanOptions options,
        IProgress<DiskScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(options, progress, cancellationToken), cancellationToken);
    }

    private DiskScanResult Scan(DiskScanOptions options, IProgress<DiskScanProgress>? progress, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var errors = new List<string>();
        var fileTypes = new Dictionary<string, FileTypeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var largestFiles = new List<DiskItem>();
        var skipped = 0;
        var filesScanned = 0;
        var foldersScanned = 0;
        long totalBytes = 0;

        var rootPath = NormalizeRoot(options.RootPath);
        var root = ScanPath(rootPath, null);
        CalculatePercentages(root);

        return new DiskScanResult(
            root,
            started,
            DateTimeOffset.Now,
            totalBytes,
            filesScanned,
            foldersScanned,
            skipped,
            errors,
            largestFiles.OrderByDescending(item => item.Size).Take(LargestFileLimit).ToList(),
            fileTypes
                .Select(pair => pair.Value.ToSummary(pair.Key))
                .OrderByDescending(summary => summary.TotalBytes)
                .ToList());

        DiskItem ScanPath(string path, DiskItem? parent)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                return ScanFile(path, parent);
            }

            return ScanDirectory(path, parent);
        }

        DiskItem ScanDirectory(string path, DiskItem? parent)
        {
            var directory = new DirectoryInfo(path);
            foldersScanned++;

            var item = new DiskItem
            {
                Name = string.IsNullOrWhiteSpace(directory.Name) ? directory.FullName : directory.Name,
                FullPath = directory.FullName,
                IsDirectory = true,
                LastModified = SafeLastWrite(directory),
                ScanStatus = "Scanned"
            };

            if (ShouldSkip(directory, options))
            {
                skipped++;
                item.ScanStatus = "Skipped";
                return item;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory.FullName);
            }
            catch (Exception ex)
            {
                skipped++;
                item.ScanStatus = "Access denied";
                errors.Add($"{directory.FullName}: {ex.Message}");
                return item;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var child = ScanPath(entry, item);
                    item.Children.Add(child);
                    item.Size += child.Size;
                    item.AllocatedSize += child.AllocatedSize;
                    item.FileCount += child.IsDirectory ? child.FileCount : 1;
                    item.FolderCount += child.IsDirectory ? child.FolderCount + 1 : 0;
                    if (child.LastModified > item.LastModified)
                    {
                        item.LastModified = child.LastModified;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"{entry}: {ex.Message}");
                }

                if ((filesScanned + foldersScanned) % ProgressInterval == 0)
                {
                    progress?.Report(new DiskScanProgress(directory.FullName, totalBytes, filesScanned, foldersScanned, skipped));
                }
            }

            item.Children.Sort((left, right) => right.Size.CompareTo(left.Size));
            progress?.Report(new DiskScanProgress(directory.FullName, totalBytes, filesScanned, foldersScanned, skipped));
            return item;
        }

        DiskItem ScanFile(string path, DiskItem? parent)
        {
            var file = new FileInfo(path);
            filesScanned++;

            var item = new DiskItem
            {
                Name = file.Name,
                FullPath = file.FullName,
                IsDirectory = false,
                Size = SafeLength(file),
                AllocatedSize = SafeLength(file),
                FileCount = 1,
                LastModified = SafeLastWrite(file),
                Extension = string.IsNullOrWhiteSpace(file.Extension) ? "(no extension)" : file.Extension.ToLowerInvariant(),
                ScanStatus = "Scanned"
            };

            if (ShouldSkip(file, options) || item.Size < options.MinimumFileSize)
            {
                skipped++;
                item.ScanStatus = "Skipped";
                return item;
            }

            totalBytes += item.Size;
            TrackLargestFile(item, largestFiles);
            TrackFileType(item, fileTypes);

            return item;
        }
    }

    public IReadOnlyList<DiskItem> GetLargestDirectories(DiskScanResult result, int count)
    {
        var directories = new List<DiskItem>();
        CollectDirectories(result.Root, directories);
        return directories
            .Where(item => item.FullPath != result.Root.FullPath)
            .OrderByDescending(item => item.Size)
            .Take(count)
            .ToList();
    }

    public IReadOnlyList<DiskItem> FlattenVisibleTree(DiskItem root, int maxItems)
    {
        var items = new List<DiskItem>();
        Collect(root, items, maxItems);
        return items;

        static void Collect(DiskItem item, List<DiskItem> items, int maxItems)
        {
            if (items.Count >= maxItems)
            {
                return;
            }

            items.Add(item);
            foreach (var child in item.Children.Where(child => child.IsDirectory).OrderByDescending(child => child.Size).Take(80))
            {
                Collect(child, items, maxItems);
            }
        }
    }

    private static void CollectDirectories(DiskItem item, List<DiskItem> directories)
    {
        if (item.IsDirectory)
        {
            directories.Add(item);
        }

        foreach (var child in item.Children)
        {
            CollectDirectories(child, directories);
        }
    }

    private static void CalculatePercentages(DiskItem item)
    {
        foreach (var child in item.Children)
        {
            child.PercentOfParent = item.Size > 0 ? child.Size * 100d / item.Size : 0;
            CalculatePercentages(child);
        }
    }

    private static string NormalizeRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootPath));
    }

    private static bool ShouldSkip(FileSystemInfo info, DiskScanOptions options)
    {
        if (!options.IncludeHidden && info.Attributes.HasFlag(FileAttributes.Hidden))
        {
            return true;
        }

        if (!options.IncludeSystem && info.Attributes.HasFlag(FileAttributes.System))
        {
            return true;
        }

        if (!options.FollowReparsePoints && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return true;
        }

        if (options.ExcludedPaths is null)
        {
            return false;
        }

        return options.ExcludedPaths.Any(excluded =>
            info.FullName.StartsWith(Path.GetFullPath(excluded), StringComparison.OrdinalIgnoreCase));
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTimeOffset SafeLastWrite(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTime;
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }

    private static void TrackLargestFile(DiskItem item, List<DiskItem> largestFiles)
    {
        largestFiles.Add(item);
        if (largestFiles.Count <= LargestFileLimit * 2)
        {
            return;
        }

        largestFiles.Sort((left, right) => right.Size.CompareTo(left.Size));
        largestFiles.RemoveRange(LargestFileLimit, largestFiles.Count - LargestFileLimit);
    }

    private static void TrackFileType(DiskItem item, Dictionary<string, FileTypeAccumulator> fileTypes)
    {
        if (!fileTypes.TryGetValue(item.Extension, out var accumulator))
        {
            accumulator = new FileTypeAccumulator();
            fileTypes[item.Extension] = accumulator;
        }

        accumulator.TotalBytes += item.Size;
        accumulator.Count++;
        if (item.Size > accumulator.LargestBytes)
        {
            accumulator.LargestBytes = item.Size;
            accumulator.LargestItemPath = item.FullPath;
        }

        if (item.LastModified > accumulator.LastModified)
        {
            accumulator.LastModified = item.LastModified;
        }
    }

    private sealed class FileTypeAccumulator
    {
        public long TotalBytes { get; set; }
        public int Count { get; set; }
        public long LargestBytes { get; set; }
        public string LargestItemPath { get; set; } = string.Empty;
        public DateTimeOffset LastModified { get; set; }

        public FileTypeSummary ToSummary(string extension)
        {
            return new FileTypeSummary(extension, TotalBytes, Count, LargestItemPath, LastModified);
        }
    }
}
