using System.Text.Json;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class TweakSnapshotService
{
    private const string FilePrefix = "tweak-snapshot-";
    private readonly PathService _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public TweakSnapshotService(PathService paths)
    {
        _paths = paths;
    }

    public async Task<string?> SaveAsync(
        string label,
        IReadOnlyDictionary<string, bool> values,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_paths.BackupsDirectory);
        var snapshot = new TweakSnapshot(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Now,
            label,
            new Dictionary<string, bool>(values, StringComparer.OrdinalIgnoreCase));
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
