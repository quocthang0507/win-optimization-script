using System.Text.Json;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class TweakSnapshotService
{
    private const string FilePrefix = "tweak-snapshot-";
    private readonly PathService _paths;
    private readonly IRegistryAccessor _registry;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public TweakSnapshotService(PathService paths, IRegistryAccessor? registry = null)
    {
        _paths = paths;
        _registry = registry ?? new WindowsRegistryAccessor();
    }

    public IReadOnlyList<RegistryValueSnapshot> CaptureRegistryEntries(
        IEnumerable<string> tweakIds,
        IReadOnlyList<SystemTweak>? catalog = null)
    {
        var allTweaks = catalog ?? TweakService.Tweaks;
        var idSet = tweakIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = new List<RegistryValueSnapshot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tweak in allTweaks.Where(t => idSet.Contains(t.Id)))
        {
            foreach (var rule in tweak.RegistryRules)
            {
                var keyId = $"{rule.Hive}\\{rule.SubKey}\\{rule.ValueName}";
                if (!seen.Add(keyId))
                {
                    continue;
                }

                try
                {
                    entries.Add(_registry.ReadValue(rule.Hive, rule.SubKey, rule.ValueName));
                }
                catch
                {
                    // Skip unreadable registry entries during snapshot capture.
                }
            }
        }

        return entries;
    }

    public void RestoreRegistryEntries(TweakSnapshot snapshot)
    {
        if (snapshot.RegistryEntries is null || snapshot.RegistryEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in snapshot.RegistryEntries)
        {
            if (entry.Exists)
            {
                _registry.WriteValue(entry.Hive, entry.SubKey, entry.ValueName, entry.ValueKind, entry.RawValue);
            }
            else
            {
                _registry.DeleteValue(entry.Hive, entry.SubKey, entry.ValueName);
            }
        }
    }

    public async Task<string?> SaveAsync(
        string label,
        IReadOnlyDictionary<string, bool> values,
        IReadOnlyList<RegistryValueSnapshot>? registryEntries = null,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_paths.BackupsDirectory);
        var entriesToSave = registryEntries ?? CaptureRegistryEntries(values.Keys);
        var snapshot = new TweakSnapshot(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Now,
            label,
            new Dictionary<string, bool>(values, StringComparer.OrdinalIgnoreCase),
            entriesToSave.Count > 0 ? entriesToSave : null);
        var path = Path.Combine(_paths.BackupsDirectory, $"{FilePrefix}{snapshot.CreatedAt:yyyyMMdd-HHmmss-fff}-{snapshot.Id}.json");
        var temporaryPath = $"{path}.tmp";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public IReadOnlyList<(string Path, TweakSnapshot Snapshot)> GetSnapshots(int maxCount = int.MaxValue)
    {
        if (maxCount <= 0 || !Directory.Exists(_paths.BackupsDirectory))
        {
            return [];
        }

        var snapshots = new List<(string Path, TweakSnapshot Snapshot)>();
        var files = new DirectoryInfo(_paths.BackupsDirectory)
            .EnumerateFiles($"{FilePrefix}*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                // Validate before deserializing; duplicate IDs must not silently overwrite values.
                if (file.Length > 1024 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Values", out var values)) continue;
                var validated = TweakChangePlanner.ParseValues(values);
                var snapshot = JsonSerializer.Deserialize<TweakSnapshot>(root.GetRawText());
                if (snapshot is not null && validated.Count > 0 &&
                    !string.IsNullOrWhiteSpace(snapshot.Id) && !string.IsNullOrWhiteSpace(snapshot.Label) &&
                    snapshot.CreatedAt != default)
                {
                    snapshots.Add((file.FullName, snapshot with { Values = validated }));
                    if (snapshots.Count >= maxCount)
                    {
                        break;
                    }
                }
            }
            catch (JsonException)
            {
                // A corrupt snapshot is ignored and left for manual inspection.
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                // A busy snapshot is skipped for this refresh.
            }
            catch (UnauthorizedAccessException)
            {
                // Inaccessible snapshots cannot be offered for restore.
            }
        }

        return snapshots;
    }

    public int PruneSnapshots(int retentionDays)
    {
        if (retentionDays <= 0 || !Directory.Exists(_paths.BackupsDirectory))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deletedCount = 0;
        foreach (var file in new DirectoryInfo(_paths.BackupsDirectory)
                     .EnumerateFiles($"{FilePrefix}*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if (file.LastWriteTimeUtc < cutoff && Delete(file.FullName))
                {
                    deletedCount++;
                }
            }
            catch
            {
                // Best-effort pruning.
            }
        }

        return deletedCount;
    }

    public bool Delete(string path)
    {
        if (!PathSafetyService.IsPathWithinOrEqual(path, _paths.BackupsDirectory) ||
            !Path.GetFileName(path).StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
