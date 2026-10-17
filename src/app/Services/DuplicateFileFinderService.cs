using System.Security.Cryptography;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class DuplicateFileFinderService
{
    private const int PartialHashSampleSize = 4096;

    private static readonly string[] ExcludedDirectoryNames =
    [
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "$Recycle.Bin",
        "System Volume Information",
        ".git",
        "node_modules"
    ];

    public Task<DuplicateScanResult> ScanAsync(
        string rootPath,
        long minFileSizeBytes = 1024,
        IEnumerable<string>? protectedPaths = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return new DuplicateScanResult(rootPath ?? string.Empty, [], 0);
            }

            var fullRoot = Path.GetFullPath(rootPath);
            var protectedList = (protectedPaths ?? []).ToList();
            var sizeBuckets = new Dictionary<long, List<FileInfo>>();
            var totalScanned = 0;

            // Stage 1: Enumerate files and group by exact byte size
            var dirsToVisit = new Stack<DirectoryInfo>();
            dirsToVisit.Push(new DirectoryInfo(fullRoot));

            while (dirsToVisit.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDir = dirsToVisit.Pop();

                if (ShouldSkipDirectory(currentDir, protectedList))
                {
                    continue;
                }

                FileInfo[] files;
                try
                {
                    files = currentDir.GetFiles();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
                        {
                            continue;
                        }

                        if (file.Length < Math.Max(1, minFileSizeBytes))
                        {
                            continue;
                        }

                        if (ProtectedPathService.IsProtectedPath(file.FullName, protectedList))
                        {
                            continue;
                        }

                        totalScanned++;
                        if (!sizeBuckets.TryGetValue(file.Length, out var bucket))
                        {
                            bucket = [];
                            sizeBuckets[file.Length] = bucket;
                        }
                        bucket.Add(file);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        // Ignore inaccessible file
                    }
                }

                DirectoryInfo[] subDirs;
                try
                {
                    subDirs = currentDir.GetDirectories();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    dirsToVisit.Push(subDir);
                }
            }

            // Stage 2: Partial hash (first 4KB + last 4KB) for size collisions
            var candidateSizeGroups = sizeBuckets.Values.Where(list => list.Count > 1).ToList();
            var partialHashBuckets = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);

            foreach (var sizeGroup in candidateSizeGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in sizeGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var partialHash = ComputePartialHash(file);
                    if (partialHash is null)
                    {
                        continue;
                    }

                    var key = $"{file.Length}:{partialHash}";
                    if (!partialHashBuckets.TryGetValue(key, out var pBucket))
                    {
                        pBucket = [];
                        partialHashBuckets[key] = pBucket;
                    }
                    pBucket.Add(file);
                }
            }

            // Stage 3: Full SHA-256 hash for partial-hash collisions
            var duplicateGroups = new List<DuplicateFileGroup>();
            var candidatePartialGroups = partialHashBuckets.Values.Where(list => list.Count > 1).ToList();

            foreach (var partialGroup in candidatePartialGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullHashBuckets = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);

                foreach (var file in partialGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(file.Name);
                    var fullHash = ComputeFullSha256(file);
                    if (fullHash is null)
                    {
                        continue;
                    }

                    if (!fullHashBuckets.TryGetValue(fullHash, out var fBucket))
                    {
                        fBucket = [];
                        fullHashBuckets[fullHash] = fBucket;
                    }
                    fBucket.Add(file);
                }

                foreach (var (hash, matchedFiles) in fullHashBuckets)
                {
                    if (matchedFiles.Count < 2)
                    {
                        continue;
                    }

                    // Order by oldest modified time first (then shortest path) to pick the canonical original
                    var ordered = matchedFiles
                        .OrderBy(f => f.LastWriteTimeUtc)
                        .ThenBy(f => f.FullName.Length)
                        .ThenBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var items = new List<DuplicateFileItem>(ordered.Count);
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var info = ordered[i];
                        var isOriginal = i == 0;
                        items.Add(new DuplicateFileItem
                        {
                            FullPath = info.FullName,
                            FileName = info.Name,
                            DirectoryPath = info.DirectoryName ?? string.Empty,
                            SizeBytes = info.Length,
                            LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                            IsOriginal = isOriginal,
                            IsSelected = !isOriginal
                        });
                    }

                    duplicateGroups.Add(new DuplicateFileGroup
                    {
                        Hash = hash,
                        FileSizeBytes = ordered[0].Length,
                        Files = items
                    });
                }
            }

            var sortedGroups = duplicateGroups
                .OrderByDescending(g => g.ReclaimableBytes)
                .ThenBy(g => g.Files[0].FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new DuplicateScanResult(fullRoot, sortedGroups, totalScanned);
        }, cancellationToken);
    }

    /// <summary>
    /// Ensures that in every group, at least one file remains unselected so all copies are never deleted at once.
    /// </summary>
    public static IReadOnlyList<StorageCleanupCandidate> BuildSafeCleanupCandidates(IEnumerable<DuplicateFileGroup> groups)
    {
        var candidates = new List<StorageCleanupCandidate>();

        foreach (var group in groups)
        {
            if (group.Files.Count < 2)
            {
                continue;
            }

            var selected = group.Files.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0)
            {
                continue;
            }

            // Safety guard: never allow deleting every file in a duplicate group
            if (selected.Count >= group.Files.Count)
            {
                selected = selected.Where(f => !f.IsOriginal).ToList();
                if (selected.Count >= group.Files.Count)
                {
                    selected.RemoveAt(0);
                }
            }

            foreach (var item in selected)
            {
                candidates.Add(new StorageCleanupCandidate(
                    $"dup:{item.FullPath}",
                    item.FileName,
                    item.FullPath,
                    item.SizeBytes,
                    RiskLevel.Safe,
                    StorageCleanupMode.MoveToRecycleBin,
                    $"Duplicate of {group.Files.First(f => !selected.Contains(f)).FullPath}",
                    false));
            }
        }

        return candidates;
    }

    private static bool ShouldSkipDirectory(DirectoryInfo dir, IReadOnlyList<string> protectedPaths)
    {
        try
        {
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            if (ExcludedDirectoryNames.Any(excluded => dir.Name.Equals(excluded, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (ProtectedPathService.IsProtectedPath(dir.FullName, protectedPaths))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    internal static string? ComputePartialHash(FileInfo file)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = stream.Length;
            if (length <= PartialHashSampleSize * 2)
            {
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(stream));
            }

            var buffer = new byte[PartialHashSampleSize * 2];
            var firstRead = stream.Read(buffer, 0, PartialHashSampleSize);
            stream.Seek(-PartialHashSampleSize, SeekOrigin.End);
            var lastRead = stream.Read(buffer, firstRead, PartialHashSampleSize);

            using var shaPartial = SHA256.Create();
            return Convert.ToHexString(shaPartial.ComputeHash(buffer, 0, firstRead + lastRead));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? ComputeFullSha256(FileInfo file)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
