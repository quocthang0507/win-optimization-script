using System.Collections;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public static class CloudStorageDetector
{
    public static IReadOnlyList<CloudStorageLocation> Detect()
    {
        return Detect(ReadEnvironment(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Directory.Exists);
    }

    public static IReadOnlyList<CloudStorageLocation> Detect(
        IReadOnlyDictionary<string, string?> environment,
        string userProfile,
        Func<string, bool> directoryExists)
    {
        var result = new List<CloudStorageLocation>();
        AddOneDriveLocations(result, environment, directoryExists);
        AddKnownFolderLocation(result, CloudStorageProvider.GoogleDrive, "Google Drive", userProfile, ["Google Drive", "My Drive"], directoryExists);
        AddKnownFolderLocation(result, CloudStorageProvider.Dropbox, "Dropbox", userProfile, ["Dropbox"], directoryExists);
        EnsureMissingProvider(result, CloudStorageProvider.OneDrive, "OneDrive");
        EnsureMissingProvider(result, CloudStorageProvider.GoogleDrive, "Google Drive");
        EnsureMissingProvider(result, CloudStorageProvider.Dropbox, "Dropbox");
        return result
            .OrderBy(location => location.Provider)
            .ThenByDescending(location => location.IsDetected)
            .ThenBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddOneDriveLocations(
        List<CloudStorageLocation> result,
        IReadOnlyDictionary<string, string?> environment,
        Func<string, bool> directoryExists)
    {
        var paths = new[]
            {
                GetEnvironmentValue(environment, "OneDrive"),
                GetEnvironmentValue(environment, "OneDriveConsumer"),
                GetEnvironmentValue(environment, "OneDriveCommercial")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizePath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (directoryExists(path))
            {
                result.Add(new CloudStorageLocation(
                    CloudStorageProvider.OneDrive,
                    "OneDrive",
                    path,
                    true,
                    "Local sync folder detected."));
            }
        }
    }

    private static void AddKnownFolderLocation(
        List<CloudStorageLocation> result,
        CloudStorageProvider provider,
        string displayName,
        string userProfile,
        IReadOnlyList<string> folderNames,
        Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return;
        }

        foreach (var folderName in folderNames)
        {
            var path = NormalizePath(System.IO.Path.Combine(userProfile, folderName));
            if (!directoryExists(path))
            {
                continue;
            }

            result.Add(new CloudStorageLocation(
                provider,
                displayName,
                path,
                true,
                "Local sync folder detected."));
            return;
        }
    }

    private static void EnsureMissingProvider(List<CloudStorageLocation> result, CloudStorageProvider provider, string displayName)
    {
        if (result.Any(location => location.Provider == provider && location.IsDetected))
        {
            return;
        }

        result.Add(new CloudStorageLocation(
            provider,
            displayName,
            string.Empty,
            false,
            "Sync folder not found."));
    }

    private static IReadOnlyDictionary<string, string?> ReadEnvironment()
    {
        return Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(entry => entry.Key is string)
            .ToDictionary(
                entry => (string)entry.Key,
                entry => entry.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetEnvironmentValue(IReadOnlyDictionary<string, string?> environment, string key)
    {
        return environment.TryGetValue(key, out var value) ? value : null;
    }

    private static string NormalizePath(string path)
    {
        return System.IO.Path.GetFullPath(path)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }
}
