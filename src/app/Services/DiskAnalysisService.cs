using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class DiskAnalysisService
{
    private const int ProgressInterval = 250;
    private const int LargestFileLimit = 60;
    private const int ProgressUpdateIntervalMilliseconds = 150;
    private const int SnapshotIntervalMilliseconds = 500;

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
        var lastProgressAt = DateTimeOffset.MinValue;
        var lastSnapshotAt = DateTimeOffset.MinValue;

        var rootPath = NormalizeRoot(options.RootPath);
        DiskItem? currentRoot = null;
        try
        {
            currentRoot = ScanPath(rootPath, null, keepChildren: true, isRoot: true);
            return BuildResult(currentRoot, isPartial: false);
        }
        catch (OperationCanceledException)
        {
            return BuildResult(currentRoot ?? CreateFallbackRoot(rootPath), isPartial: true);
        }

        DiskItem ScanPath(string path, DiskItem? parent, bool keepChildren, bool isRoot = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return File.Exists(path) ? ScanFile(path, parent) : ScanDirectory(path, parent, keepChildren, isRoot);
        }

        DiskItem ScanDirectory(string path, DiskItem? parent, bool keepChildren, bool isRoot = false)
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
            if (isRoot)
            {
                currentRoot = item;
            }

            if (!isRoot && ShouldSkip(directory, options))
            {
                skipped++;
                item.ScanStatus = "Skipped";
                return item;
            }

            IEnumerator<string>? entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory.FullName).GetEnumerator();
            }
            catch (Exception ex)
            {
                skipped++;
                item.ScanStatus = "Access denied";
                errors.Add($"{directory.FullName}: {ex.Message}");
                return item;
            }

            using (entries)
            {
                while (TryMoveNext(entries, directory, item, out var entry))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var child = ScanPath(entry, item, keepChildren: false);
                        AddChildStats(item, child);
                        if (keepChildren)
                        {
                            item.Children.Add(child);
                            item.Children.Sort((left, right) => right.Size.CompareTo(left.Size));
                            ReportProgress(entry);
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
                        ReportProgress(directory.FullName);
                    }
                }
            }

            item.Children.Sort((left, right) => right.Size.CompareTo(left.Size));
            ReportProgress(directory.FullName);
            return item;
        }

        bool TryMoveNext(IEnumerator<string> entries, DirectoryInfo directory, DiskItem item, out string entry)
        {
            entry = string.Empty;
            try
            {
                if (!entries.MoveNext())
                {
                    return false;
                }

                entry = entries.Current;
                return true;
            }
            catch (Exception ex)
            {
                skipped++;
                item.ScanStatus = "Partial";
                errors.Add($"{directory.FullName}: {ex.Message}");
                return false;
            }
        }

        void AddChildStats(DiskItem item, DiskItem child)
        {
            item.Size += child.Size;
            item.AllocatedSize += child.AllocatedSize;
            item.FileCount += child.IsDirectory ? child.FileCount : 1;
            item.FolderCount += child.IsDirectory ? child.FolderCount + 1 : 0;
            if (child.LastModified > item.LastModified)
            {
                item.LastModified = child.LastModified;
            }
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

        void ReportProgress(string currentPath)
        {
            var now = DateTimeOffset.Now;
            var shouldSendProgress = lastProgressAt == DateTimeOffset.MinValue ||
                                     (now - lastProgressAt).TotalMilliseconds >= ProgressUpdateIntervalMilliseconds;
            if (!shouldSendProgress)
            {
                return;
            }

            lastProgressAt = now;
            if (currentRoot is null)
            {
                progress?.Report(new DiskScanProgress(currentPath, totalBytes, filesScanned, foldersScanned, skipped));
                return;
            }

            DiskScanResult? partialResult = null;
            if (lastSnapshotAt == DateTimeOffset.MinValue || (now - lastSnapshotAt).TotalMilliseconds >= SnapshotIntervalMilliseconds)
            {
                lastSnapshotAt = now;
                partialResult = BuildResult(currentRoot, isPartial: true);
            }

            progress?.Report(new DiskScanProgress(
                currentPath,
                totalBytes,
                filesScanned,
                foldersScanned,
                skipped,
                partialResult));
        }

        DiskScanResult BuildResult(DiskItem root, bool isPartial)
        {
            var snapshotRoot = CloneDiskItem(root);
            CalculatePercentages(snapshotRoot);
            return new DiskScanResult(
                snapshotRoot,
                started,
                DateTimeOffset.Now,
                totalBytes,
                filesScanned,
                foldersScanned,
                skipped,
                errors.ToList(),
                largestFiles.OrderByDescending(item => item.Size).Take(LargestFileLimit).Select(CloneDiskItem).ToList(),
                fileTypes
                    .Select(pair => pair.Value.ToSummary(pair.Key))
                    .OrderByDescending(summary => summary.TotalBytes)
                    .ToList(),
                isPartial);
        }
    }

    public IReadOnlyList<DiskItem> GetLargestDirectories(DiskScanResult result, int count)
    {
        return result.Root.Children
            .Where(item => item.IsDirectory)
            .OrderByDescending(item => item.Size)
            .Take(count)
            .ToList();
    }

    public IReadOnlyList<DiskItem> FlattenVisibleTree(DiskItem root, int maxItems)
    {
        if (maxItems <= 0)
        {
            return [];
        }

        if (!root.IsDirectory)
        {
            return [root];
        }

        return root.Children
            .OrderByDescending(child => child.Size)
            .Take(maxItems)
            .ToList();
    }

    private static void CalculatePercentages(DiskItem item)
    {
        foreach (var child in item.Children)
        {
            child.PercentOfParent = item.Size > 0 ? child.Size * 100d / item.Size : 0;
            CalculatePercentages(child);
        }
    }

    private static DiskItem CloneDiskItem(DiskItem item)
    {
        var clone = new DiskItem
        {
            Name = item.Name,
            FullPath = item.FullPath,
            IsDirectory = item.IsDirectory,
            Size = item.Size,
            AllocatedSize = item.AllocatedSize,
            PercentOfParent = item.PercentOfParent,
            FileCount = item.FileCount,
            FolderCount = item.FolderCount,
            LastModified = item.LastModified,
            Extension = item.Extension,
            ScanStatus = item.ScanStatus
        };

        foreach (var child in item.Children)
        {
            clone.Children.Add(CloneDiskItem(child));
        }

        return clone;
    }

    private static DiskItem CreateFallbackRoot(string rootPath)
    {
        return new DiskItem
        {
            Name = string.IsNullOrWhiteSpace(Path.GetFileName(rootPath)) ? rootPath : Path.GetFileName(rootPath),
            FullPath = rootPath,
            IsDirectory = Directory.Exists(rootPath),
            LastModified = DateTimeOffset.MinValue,
            ScanStatus = "Canceled"
        };
    }

    private static string NormalizeRoot(string rootPath)
    {
        return string.IsNullOrWhiteSpace(rootPath)
            ? Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\"
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootPath));
    }

    private static bool ShouldSkip(FileSystemInfo info, DiskScanOptions options)
    {
        return (!options.IncludeHidden && info.Attributes.HasFlag(FileAttributes.Hidden)) ||
               (!options.IncludeSystem && info.Attributes.HasFlag(FileAttributes.System)) ||
               (!options.FollowReparsePoints && info.Attributes.HasFlag(FileAttributes.ReparsePoint)) ||
               (options.ExcludedPaths is not null && options.ExcludedPaths.Any(excluded =>
                   PathSafetyService.IsPathWithinOrEqual(info.FullName, excluded)));
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
