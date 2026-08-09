using WinOptimizationApp.Models;
using System.Runtime.InteropServices;

namespace WinOptimizationApp.Services;

public sealed class DiskAnalysisService
{
    private const uint InvalidFileSize = 0xFFFFFFFF;
    private const int ProgressInterval = 250;
    private const int LargestFileLimit = 60;
    private const int DiscoveryFileLimit = 60;
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
        var fileTypes = new Dictionary<string, FileTypeAccumulator>(StringComparer.Ordinal);
        var largestFiles = new List<DiskItem>();
        var newestFiles = new List<DiskItem>();
        var oldestFiles = new List<DiskItem>();
        var developerArtifacts = new List<DiskItem>();
        var fileAgeBytes = new long[Enum.GetValues<FileAgeRange>().Length];
        var fileAgeCounts = new int[fileAgeBytes.Length];
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
            FileSystemInfo rootInfo = File.Exists(rootPath)
                ? new FileInfo(rootPath)
                : new DirectoryInfo(rootPath);
            currentRoot = ScanPath(rootInfo, null, keepChildren: true, isRoot: true);
            return BuildResult(currentRoot, isPartial: false);
        }
        catch (OperationCanceledException)
        {
            return BuildResult(currentRoot ?? CreateFallbackRoot(rootPath), isPartial: true);
        }

        DiskItem ScanPath(FileSystemInfo info, DiskItem? parent, bool keepChildren, bool isRoot = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return info is FileInfo fileInfo ? ScanFile(fileInfo, parent) : ScanDirectory((DirectoryInfo)info, parent, keepChildren, isRoot);
        }

        DiskItem ScanDirectory(DirectoryInfo directory, DiskItem? parent, bool keepChildren, bool isRoot = false)
        {
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

            IEnumerator<FileSystemInfo>? entries;
            try
            {
                entries = directory.EnumerateFileSystemInfos().GetEnumerator();
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
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        errors.Add($"{entry.FullName}: {ex.Message}");
                    }

                    if ((filesScanned + foldersScanned) % ProgressInterval == 0)
                    {
                        ReportProgress(directory.FullName);
                    }
                }
            }

            if (keepChildren)
            {
                item.Children.Sort((left, right) => right.Size.CompareTo(left.Size));
            }
            if (item.Size > 0 && DeveloperArtifactService.IsArtifactDirectory(directory))
            {
                TrackLargestItem(item, developerArtifacts, DiscoveryFileLimit);
            }
            ReportProgress(directory.FullName);
            return item;
        }

        bool TryMoveNext(IEnumerator<FileSystemInfo> entries, DirectoryInfo directory, DiskItem item, out FileSystemInfo entry)
        {
            entry = null!;
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

        DiskItem ScanFile(FileInfo file, DiskItem? parent)
        {
            filesScanned++;
            var logicalSize = SafeLength(file);

            var item = new DiskItem
            {
                Name = file.Name,
                FullPath = file.FullName,
                IsDirectory = false,
                Size = logicalSize,
                AllocatedSize = SafeAllocatedSize(file, logicalSize),
                FileCount = 1,
                LastModified = SafeLastWrite(file),
                Extension = string.IsNullOrWhiteSpace(file.Extension) ? "(no extension)" : file.Extension.ToLowerInvariant(),
                ScanStatus = "Scanned"
            };

            if (ShouldSkip(file, options) || item.Size < options.MinimumFileSize)
            {
                skipped++;
                item.ScanStatus = "Skipped";
                item.Size = 0;
                item.AllocatedSize = 0;
                return item;
            }

            totalBytes += item.Size;
            TrackLargestFile(item, largestFiles);
            TrackFileByAge(item, newestFiles, newestFirst: true);
            TrackFileByAge(item, oldestFiles, newestFirst: false);
            TrackFileAgeSummary(item, started, fileAgeBytes, fileAgeCounts);
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
            root.Children.Sort((left, right) => right.Size.CompareTo(left.Size));
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
                isPartial)
            {
                NewestFiles = newestFiles.Select(CloneDiskItem).ToList(),
                OldestFiles = oldestFiles.Select(CloneDiskItem).ToList(),
                FileAgeSummaries = Enum.GetValues<FileAgeRange>()
                    .Select(range => new FileAgeSummary(range, fileAgeBytes[(int)range], fileAgeCounts[(int)range]))
                    .Where(summary => summary.Count > 0)
                    .ToList(),
                DeveloperArtifacts = developerArtifacts
                    .OrderByDescending(item => item.Size)
                    .Select(CloneDiskItem)
                    .ToList()
            };
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
        return maxItems <= 0
            ? []
            : !root.IsDirectory
            ? [root]
            : root.Children
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
        TrackLargestItem(item, largestFiles, LargestFileLimit);
    }

    private static long SafeAllocatedSize(FileInfo file, long logicalSize)
    {
        if (!OperatingSystem.IsWindows())
        {
            return logicalSize;
        }

        try
        {
            var low = GetCompressedFileSize(file.FullName, out var high);
            if (low == InvalidFileSize && Marshal.GetLastWin32Error() != 0)
            {
                return logicalSize;
            }

            var allocatedSize = ((ulong)high << 32) | low;
            return allocatedSize <= long.MaxValue ? (long)allocatedSize : logicalSize;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return logicalSize;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSize(
        string fileName,
        out uint fileSizeHigh);

    private static void TrackLargestItem(DiskItem item, List<DiskItem> items, int limit)
    {
        if (items.Count < limit)
        {
            items.Add(item);
            if (items.Count == limit)
            {
                items.Sort((left, right) => right.Size.CompareTo(left.Size));
            }
            return;
        }

        if (item.Size <= items[^1].Size)
        {
            return;
        }

        int index = items.FindIndex(x => x.Size < item.Size);
        if (index >= 0)
        {
            items.Insert(index, item);
            items.RemoveAt(items.Count - 1);
        }
    }

    private static void TrackFileByAge(DiskItem item, List<DiskItem> files, bool newestFirst)
    {
        if (item.LastModified == DateTimeOffset.MinValue)
        {
            return;
        }

        var low = 0;
        var high = files.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = newestFirst
                ? files[middle].LastModified.CompareTo(item.LastModified)
                : item.LastModified.CompareTo(files[middle].LastModified);
            if (comparison < 0)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        if (files.Count >= DiscoveryFileLimit && low >= DiscoveryFileLimit)
        {
            return;
        }

        files.Insert(low, item);
        if (files.Count > DiscoveryFileLimit)
        {
            files.RemoveAt(files.Count - 1);
        }
    }

    private static void TrackFileAgeSummary(
        DiskItem item,
        DateTimeOffset scanStartedAt,
        long[] bytes,
        int[] counts)
    {
        var range = ClassifyFileAge(item.LastModified, scanStartedAt);
        bytes[(int)range] += item.Size;
        counts[(int)range]++;
    }

    internal static FileAgeRange ClassifyFileAge(DateTimeOffset lastModified, DateTimeOffset referenceTime)
    {
        if (lastModified == DateTimeOffset.MinValue)
        {
            return FileAgeRange.Unknown;
        }

        var age = referenceTime - lastModified;
        if (age <= TimeSpan.FromDays(7))
        {
            return FileAgeRange.Last7Days;
        }

        if (age <= TimeSpan.FromDays(30))
        {
            return FileAgeRange.Last30Days;
        }

        if (age <= TimeSpan.FromDays(365))
        {
            return FileAgeRange.LastYear;
        }

        return FileAgeRange.Older;
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
