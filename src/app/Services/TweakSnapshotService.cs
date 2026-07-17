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

    public IReadOnlyList<(string Path, TweakSnapshot Snapshot)> GetSnapshots()
    {
        if (!Directory.Exists(_paths.BackupsDirectory))
        {
            return [];
        }

        var snapshots = new List<(string Path, TweakSnapshot Snapshot)>();
        foreach (var path in Directory.EnumerateFiles(_paths.BackupsDirectory, $"{FilePrefix}*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<TweakSnapshot>(File.ReadAllText(path));
                if (snapshot is not null && snapshot.Values.Count > 0)
                {
                    snapshots.Add((path, snapshot));
                }
            }
            catch (JsonException)
            {
                // A corrupt snapshot is ignored and left for manual inspection.
            }
            catch (IOException)
            {
                // A busy snapshot is skipped for this refresh.
            }
        }

        return snapshots.OrderByDescending(item => item.Snapshot.CreatedAt).ToList();
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
