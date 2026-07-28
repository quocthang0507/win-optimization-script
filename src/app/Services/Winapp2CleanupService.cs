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
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CollectCandidates(entries, protectedPaths, cancellationToken), cancellationToken);
    }

    public async Task<TaskRunResult> RunAsync(
        Winapp2CleanupPreview preview,
        int selectedEntryCount,
        IReadOnlyList<string>? protectedPaths = null,
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
                    if (string.IsNullOrWhiteSpace(parent) || !IsSafeCleanupFile(candidate.Path, parent))
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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
        CancellationToken cancellationToken)
    {
        var candidates = new List<Winapp2CleanupCandidate>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.RegKeys.Count > 0)
            {
                warnings.Add($"{entry.Name}: {entry.RegKeys.Count:N0} registry rule(s) were previewed but intentionally not deleted.");
            }

            foreach (var fileKey in entry.FileKeys)
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
                            if (!IsSafeCleanupFile(fullPath, directory))
                            {
                                warnings.Add($"{entry.Name}: blocked unsafe cleanup target {fullPath}");
                                continue;
                            }

                            if (ProtectedPathService.IsProtectedPath(fullPath, protectedPaths))
                            {
                                warnings.Add($"{entry.Name}: skipped user-protected target {fullPath}");
                                continue;
                            }

                            if (!IsExcluded(fullPath, entry.ExcludeKeys) && seen.Add(fullPath))
                            {
                                candidates.Add(new Winapp2CleanupCandidate(entry.Name, fullPath, new FileInfo(fullPath).Length));
                            }
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        warnings.Add($"{entry.Name}: {ex.Message}");
                    }
                }
            }
        }

        return new Winapp2CleanupPreview(candidates, warnings.Distinct().ToList());
    }

    private static bool IsSafeCleanupFile(string filePath, string sourceDirectory)
    {
        if (!PathSafetyService.IsPathWithinOrEqual(filePath, sourceDirectory)) return false;

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows) && PathSafetyService.IsPathWithinOrEqual(filePath, windows))
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

        try
        {
            return (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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
