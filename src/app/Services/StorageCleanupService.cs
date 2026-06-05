using Microsoft.VisualBasic.FileIO;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class StorageCleanupService
{
    private readonly ReportService _reports;

    public StorageCleanupService(ReportService reports)
    {
        _reports = reports;
    }

    public IReadOnlyList<StorageCleanupCandidate> CreateCandidates(DiskScanResult result)
    {
        var candidates = new List<StorageCleanupCandidate>();

        foreach (var file in result.LargestFiles
                     .Where(file => IsLikelyCleanupCandidate(file))
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
        if (extension is ".tmp" or ".log" or ".dmp" or ".bak")
        {
            return true;
        }

        if (extension is ".iso" or ".zip" or ".7z" or ".rar" or ".msi" or ".exe")
        {
            return IsInDownloads(item.FullPath);
        }

        return false;
    }

    private static bool IsLikelyCacheFolder(DiskItem item)
    {
        var name = item.Name.ToLowerInvariant();
        return name.Contains("cache", StringComparison.Ordinal) ||
               name.Contains("temp", StringComparison.Ordinal);
    }

    private static RiskLevel GetRisk(DiskItem item)
    {
        if (IsInDownloads(item.FullPath))
        {
            return RiskLevel.Medium;
        }

        return item.Extension is ".tmp" or ".log" ? RiskLevel.Safe : RiskLevel.Medium;
    }

    private static string GetReason(DiskItem item)
    {
        if (IsInDownloads(item.FullPath))
        {
            return "Large installer/archive in Downloads.";
        }

        return item.Extension switch
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
        return path.StartsWith(downloads, StringComparison.OrdinalIgnoreCase);
    }
}
