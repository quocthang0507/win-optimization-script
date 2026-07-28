using Microsoft.VisualBasic.FileIO;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class StorageCleanupService(ReportService reports)
{
    private readonly ReportService _reports = reports;

    public static IReadOnlyList<StorageCleanupCandidate> CreateCandidates(DiskScanResult result)
    {
        var candidates = new List<StorageCleanupCandidate>();

        foreach (var file in result.LargestFiles
                     .Where(IsLikelyCleanupCandidate)
                     .OrderByDescending(file => file.Size)
                     .Take(30))
        {
            candidates.Add(new StorageCleanupCandidate(
                $"file:{file.FullPath}",
                file.Name,
                file.FullPath,
                file.Size,
                GetRisk(file),
                StorageCleanupMode.MoveToRecycleBin,
                GetReason(file),
                false));
        }

        foreach (var folder in result.Root.Children
                     .Where(child => child.IsDirectory && IsLikelyCacheFolder(child))
                     .OrderByDescending(child => child.Size)
                     .Take(12))
        {
            candidates.Add(new StorageCleanupCandidate(
                $"folder:{folder.FullPath}",
                folder.Name,
                folder.FullPath,
                folder.Size,
                RiskLevel.Medium,
                StorageCleanupMode.MoveToRecycleBin,
                "Large cache/temp-like folder.",
                true));
        }

        return candidates;
    }

    public async Task<TaskRunResult> CleanupAsync(
        IReadOnlyList<StorageCleanupCandidate> candidates,
        IReadOnlyList<string>? protectedPaths = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.Now;
        var messages = new List<string>();
        var errors = new List<string>();
        long freedBytes = 0;
        var removed = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(candidate.SourcePath) && !Directory.Exists(candidate.SourcePath))
            {
                skipped++;
                messages.Add($"Skipped missing item: {candidate.SourcePath}");
                continue;
            }

            if (!IsSafeCandidate(candidate, out var safetyReason, protectedPaths))
            {
                skipped++;
                errors.Add($"Blocked unsafe cleanup target: {candidate.SourcePath}. {safetyReason}");
                continue;
            }

            try
            {
                if (candidate.CleanupMode == StorageCleanupMode.MoveToRecycleBin)
                {
                    MoveToRecycleBin(candidate);
                }
                else
                {
                    DeletePermanently(candidate);
                }

                freedBytes += candidate.EstimatedBytes;
                removed++;
                messages.Add($"{candidate.Label}: {Formatters.FormatBytes(candidate.EstimatedBytes)}");
            }
            catch (Exception ex)
            {
                skipped++;
                errors.Add($"{candidate.SourcePath}: {ex.Message}");
            }
        }

        var result = new TaskRunResult(
            "storage.cleanup",
            "Storage Analyzer Cleanup",
            started,
            DateTimeOffset.Now,
            errors.Count == 0,
            freedBytes,
            removed,
            skipped,
            messages,
            errors);

        await _reports.SaveAsync(result, cancellationToken);
        return result;
    }

    public static bool IsSafeCandidate(
        StorageCleanupCandidate candidate,
        out string reason,
        IReadOnlyList<string>? protectedPaths = null)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate.SourcePath))
        {
            reason = "The target path is empty.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate.SourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "The target path is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fullPath) || fullPath.Equals(Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            reason = "Drive roots cannot be cleaned.";
            return false;
        }

        var protectedTrees = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        }.Where(path => !string.IsNullOrWhiteSpace(path));

        if (protectedTrees.Any(root => PathSafetyService.IsPathWithinOrEqual(fullPath, root)))
        {
            reason = "Windows, Program Files, and ProgramData are protected from manual cleanup.";
            return false;
        }

        if (ProtectedPathService.IntersectsProtectedTree(fullPath, protectedPaths))
        {
            reason = "The target intersects a user-protected path.";
            return false;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var usersRoot = Path.GetDirectoryName(userProfile);
        if (fullPath.Equals(userProfile.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(usersRoot) && fullPath.Equals(usersRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
        {
            reason = "User profile roots cannot be cleaned.";
            return false;
        }

        if (candidate.IsDirectory != Directory.Exists(fullPath) || (!candidate.IsDirectory && !File.Exists(fullPath)))
        {
            reason = "The target type changed after the scan.";
            return false;
        }

        if (ContainsReparsePoint(fullPath))
        {
            reason = "Linked or cloud-backed paths require manual review in File Explorer.";
            return false;
        }

        return true;
    }

    private static bool ContainsReparsePoint(string path)
    {
        FileSystemInfo? current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        while (current is not null && current.Exists)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            current = current switch
            {
                DirectoryInfo directory => directory.Parent,
                FileInfo file => file.Directory,
                _ => null
            };
        }

        return false;
    }

    private static void MoveToRecycleBin(StorageCleanupCandidate candidate)
    {
        if (candidate.IsDirectory)
        {
            FileSystem.DeleteDirectory(
                candidate.SourcePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
            return;
        }

        FileSystem.DeleteFile(
            candidate.SourcePath,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
    }

    private static void DeletePermanently(StorageCleanupCandidate candidate)
    {
        if (candidate.IsDirectory)
        {
            Directory.Delete(candidate.SourcePath, true);
        }
        else
        {
            File.Delete(candidate.SourcePath);
        }
    }

    private static bool IsLikelyCleanupCandidate(DiskItem item)
    {
        var extension = item.Extension.ToLowerInvariant();
        return extension is ".tmp" or ".log" or ".dmp" or ".bak" || (extension is ".iso" or ".zip" or ".7z" or ".rar" or ".msi" or ".exe" && IsInDownloads(item.FullPath));
    }

    private static bool IsLikelyCacheFolder(DiskItem item)
    {
        var name = item.Name.ToLowerInvariant();
        return name.Contains("cache", StringComparison.Ordinal) ||
               name.Contains("temp", StringComparison.Ordinal);
    }

    private static RiskLevel GetRisk(DiskItem item)
    {
        var extension = item.Extension.ToLowerInvariant();
        return IsInDownloads(item.FullPath) ? RiskLevel.Medium : extension is ".tmp" or ".log" ? RiskLevel.Safe : RiskLevel.Medium;
    }

    private static string GetReason(DiskItem item)
    {
        var extension = item.Extension.ToLowerInvariant();
        return IsInDownloads(item.FullPath)
            ? "Large installer/archive in Downloads."
            : extension switch
            {
                ".tmp" => "Temporary file.",
                ".log" => "Log file.",
                ".dmp" => "Crash dump.",
                ".bak" => "Backup-like file.",
                _ => "Large cleanup candidate."
            };
    }

    private static bool IsInDownloads(string path)
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return PathSafetyService.IsPathWithinOrEqual(path, downloads);
    }
}
