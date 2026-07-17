using System;
using System.IO;
using System.Linq;

namespace WinOptimizationApp.Services;

public static class PathExpander
{
    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // Standard Windows environment variables
        var expanded = Environment.ExpandEnvironmentVariables(path);

        // Winapp2 specific custom variables
        if (expanded.Contains("%Documents%"))
            expanded = expanded.Replace("%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            
        if (expanded.Contains("%CommonAppData%"))
            expanded = expanded.Replace("%CommonAppData%", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        if (expanded.Contains("%LocalAppDataLow%"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = expanded.Replace("%LocalAppDataLow%", Path.Combine(userProfile, "AppData", "LocalLow"));
        }

        if (expanded.Contains("%ProgramFiles%"))
            expanded = expanded.Replace("%ProgramFiles%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        if (expanded.Contains("%ProgramFiles(x86)%"))
            expanded = expanded.Replace("%ProgramFiles(x86)%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return expanded;
    }

    public static bool Exists(string path)
    {
        var expanded = Expand(path);
        if (string.IsNullOrWhiteSpace(expanded)) return false;

        try
        {
            var fullPath = Path.GetFullPath(expanded);
            if (!fullPath.Contains('*') && !fullPath.Contains('?'))
            {
                return File.Exists(fullPath) || Directory.Exists(fullPath);
            }

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root)) return false;
            var segments = fullPath[root.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<string> current = [root];

            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                var isLast = index == segments.Length - 1;
                var hasWildcard = segment.Contains('*') || segment.Contains('?');
                current = hasWildcard
                    ? current.SelectMany(directory => Directory.Exists(directory)
                        ? Directory.EnumerateFileSystemEntries(directory, segment, SearchOption.TopDirectoryOnly)
                        : [])
                    : current.Select(item => Path.Combine(item, segment));
                if (!isLast) current = current.Where(Directory.Exists);
            }

            return current.Any(item => File.Exists(item) || Directory.Exists(item));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
