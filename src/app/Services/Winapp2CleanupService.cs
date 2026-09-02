using System.IO.Enumeration;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class Winapp2CleanupService(ReportService reports)
{
    private readonly ReportService _reports = reports;
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        MatchCasing = MatchCasing.CaseInsensitive
    };

    public Task<Winapp2CleanupPreview> PreviewAsync(
        IReadOnlyList<CleanerEntry> entries,
        IReadOnlyList<string>? protectedPaths = null,
        bool restrictCustomDatabase = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => CollectCandidates(entries, protectedPaths, restrictCustomDatabase, cancellationToken),
            cancellationToken);
    }

    public async Task<TaskRunResult> RunAsync(
        Winapp2CleanupPreview preview,
        int selectedEntryCount,
        IReadOnlyList<string>? protectedPaths = null,
        bool restrictCustomDatabase = false,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.Now;
        long freedBytes = 0;
        var filesRemoved = 0;
        var filesSkipped = 0;
        var errors = new List<string>();

        await Task.Run(() =>
        {
            foreach (var candidate in preview.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(candidate.Path))
                    {
                        filesSkipped++;
                        continue;
                    }

                    if (ProtectedPathService.IsProtectedPath(candidate.Path, protectedPaths))
                    {
                        filesSkipped++;
                        errors.Add($"{candidate.Entry}: blocked user-protected target {candidate.Path}");
                        continue;
                    }

                    var parent = Path.GetDirectoryName(candidate.Path);
                    if (string.IsNullOrWhiteSpace(parent) ||
                        !IsSafeCleanupFile(candidate.Path, parent, restrictCustomDatabase))
                    {
                        filesSkipped++;
                        errors.Add($"{candidate.Entry}: blocked unsafe target {candidate.Path}");
                        continue;
                    }

                    var actualBytes = new FileInfo(candidate.Path).Length;
                    File.Delete(candidate.Path);
                    freedBytes += actualBytes;
                    filesRemoved++;
                }
                catch (Exception ex) when (IsExpectedFileSystemException(ex))
                {
                    filesSkipped++;
                    errors.Add($"{candidate.Entry}: {ex.Message}");
                }
            }
        }, cancellationToken);

        var result = new TaskRunResult(
            "winapp2.cleanup",
            "Third-Party App Cleanup",
            started,
            DateTimeOffset.Now,
            errors.Count == 0,
            freedBytes,
            filesRemoved,
            filesSkipped,
            [$"Cleaned {filesRemoved:N0} files across {selectedEntryCount:N0} apps.", .. preview.Warnings],
            errors);
        await _reports.SaveAsync(result, cancellationToken);
        return result;
    }

    private static Winapp2CleanupPreview CollectCandidates(
        IReadOnlyList<CleanerEntry> entries,
        IReadOnlyList<string>? protectedPaths,
        bool restrictCustomDatabase,
        CancellationToken cancellationToken)
    {
        var candidates = new List<Winapp2CleanupCandidate>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suppressedWarningCount = 0;

        void AddWarning(string warning)
        {
            if (warnings.Count < 200)
            {
                warnings.Add(warning);
            }
            else
            {
                suppressedWarningCount++;
            }
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(entry.Warning))
                AddWarning($"{entry.Name}: {entry.Warning}");
            if (entry.RegKeys.Count > 0)
            {
                AddWarning($"{entry.Name}: {entry.RegKeys.Count:N0} registry rule(s) were previewed but intentionally not deleted.");
            }

            foreach (var fileKey in entry.FileKeys)
            {
                try
                {
                    foreach (var directory in ExpandDirectories(PathExpander.Expand(fileKey.Path)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var options = new EnumerationOptions
                            {
                                RecurseSubdirectories = fileKey.Recurse,
                                IgnoreInaccessible = true,
                                AttributesToSkip = FileAttributes.ReparsePoint,
                                MatchCasing = MatchCasing.CaseInsensitive
                            };
                            var pattern = fileKey.Extension == "*.*" ? "*" : fileKey.Extension;
                            foreach (var file in Directory.EnumerateFiles(directory, pattern, options))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var fullPath = Path.GetFullPath(file);
                                if (!IsSafeCleanupFile(fullPath, directory, restrictCustomDatabase))
                                {
                                    AddWarning($"{entry.Name}: blocked unsafe cleanup target {fullPath}");
                                    continue;
                                }

                                if (ProtectedPathService.IsProtectedPath(fullPath, protectedPaths))
                                {
                                    AddWarning($"{entry.Name}: skipped user-protected target {fullPath}");
                                    continue;
                                }

                                if (!IsExcluded(fullPath, entry.ExcludeKeys) && seen.Add(fullPath))
                                {
                                    candidates.Add(new Winapp2CleanupCandidate(entry.Name, fullPath, new FileInfo(fullPath).Length));
                                }
                            }
                        }
                        catch (Exception ex) when (IsExpectedFileSystemException(ex))
                        {
                            AddWarning($"{entry.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex) when (IsExpectedFileSystemException(ex))
                {
                    AddWarning($"{entry.Name}: blocked invalid rule {ex.Message}");
                }
            }
        }

        if (suppressedWarningCount > 0)
        {
            warnings.Add($"{suppressedWarningCount:N0} additional warning(s) were suppressed.");
        }
        return new Winapp2CleanupPreview(candidates, warnings.Distinct().ToList());
    }

    private static bool IsSafeCleanupFile(
        string filePath,
        string sourceDirectory,
        bool restrictCustomDatabase)
    {
        if (!PathSafetyService.IsPathWithinOrEqual(filePath, sourceDirectory) ||
            IsNetworkPath(filePath) ||
            HasReparsePointInPath(sourceDirectory))
        {
            return false;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows) && PathSafetyService.IsPathWithinOrEqual(filePath, windows))
        {
            return false;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (new[] { programFiles, programFilesX86 }
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Any(root => PathSafetyService.IsPathWithinOrEqual(filePath, root)))
        {
            return false;
        }

        string[] protectedRoots =
        [
            Path.GetPathRoot(filePath) ?? string.Empty,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        ];
        var normalizedSource = Path.GetFullPath(sourceDirectory).TrimEnd('\\', '/');
        if (protectedRoots.Where(root => !string.IsNullOrWhiteSpace(root)).Any(root =>
                normalizedSource.Equals(Path.GetFullPath(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (IsWithinPersonalFolder(filePath))
        {
            return false;
        }

        if (restrictCustomDatabase && !IsRecognizedDisposableDirectory(sourceDirectory))
        {
            return false;
        }

        try
        {
            return (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsNetworkPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        try
        {
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }

    private static bool HasReparsePointInPath(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current != null)
        {
            try
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsWithinPersonalFolder(string path)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var personalRoots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            personalRoots.Add(Path.Combine(userProfile, "Downloads"));
            personalRoots.Add(Path.Combine(userProfile, "Saved Games"));
        }

        return personalRoots
            .Where(Directory.Exists)
            .Any(root => PathSafetyService.IsPathWithinOrEqual(path, root));
    }

    private static bool IsRecognizedDisposableDirectory(string sourceDirectory)
    {
        var fullPath = Path.GetFullPath(sourceDirectory);
        var knownRoots = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache))
        };
        if (knownRoots.Where(root => !string.IsNullOrWhiteSpace(root)).Any(root =>
                PathSafetyService.IsPathWithinOrEqual(fullPath, root)))
        {
            return true;
        }

        var applicationDataRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };
        if (!applicationDataRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Any(root => PathSafetyService.IsPathWithinOrEqual(fullPath, root)))
        {
            return false;
        }

        string[] disposableSegments =
        [
            "cache", "caches", "temp", "tmp", "logs", "log", "crash", "crashes",
            "crashdumps", "dumps", "inetcache", "gpucache", "code cache"
        ];
        return fullPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => disposableSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsExpectedFileSystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or System.Security.SecurityException;

    private static IEnumerable<string> ExpandDirectories(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) yield break;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.Contains('*') && !fullPath.Contains('?'))
        {
            if (Directory.Exists(fullPath)) yield return fullPath;
            yield break;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root)) yield break;
        IEnumerable<string> current = [root];
        foreach (var segment in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var wildcard = segment.Contains('*') || segment.Contains('?');
            current = wildcard
                ? current.SelectMany(directory => Directory.Exists(directory)
                    ? Directory.EnumerateDirectories(directory, segment, Enumeration)
                    : [])
                : current.Select(directory => Path.Combine(directory, segment)).Where(Directory.Exists);
        }

        foreach (var directory in current) yield return directory;
    }

    private static bool IsExcluded(string filePath, IReadOnlyList<ExcludeKeyEntry> exclusions)
    {
        foreach (var exclusion in exclusions)
        {
            var excludedRoot = PathExpander.Expand(exclusion.Path);
            if (string.IsNullOrWhiteSpace(excludedRoot) ||
                !PathSafetyService.IsPathWithinOrEqual(filePath, excludedRoot))
            {
                continue;
            }

            if (exclusion.Type.Equals("PATH", StringComparison.OrdinalIgnoreCase) ||
                (exclusion.Type.Equals("FILE", StringComparison.OrdinalIgnoreCase) &&
                 FileSystemName.MatchesSimpleExpression(exclusion.Expression, Path.GetFileName(filePath), ignoreCase: true)))
            {
                return true;
            }
        }

        return false;
    }
}
