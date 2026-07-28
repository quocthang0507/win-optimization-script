namespace WinOptimizationApp.Services;

public static class ProtectedPathService
{
    public static IReadOnlyList<string> NormalizePaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return [];
        }

        var normalized = new List<string>();
        foreach (var path in paths)
        {
            if (!TryNormalize(path, out var fullPath) ||
                normalized.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized.Add(fullPath);
        }

        return normalized;
    }

    public static bool IsProtectedPath(string candidatePath, IEnumerable<string>? protectedPaths)
    {
        if (!TryNormalize(candidatePath, out var candidate))
        {
            return false;
        }

        return NormalizePaths(protectedPaths)
            .Any(protectedPath => PathSafetyService.IsPathWithinOrEqual(candidate, protectedPath));
    }

    public static bool IntersectsProtectedTree(string candidatePath, IEnumerable<string>? protectedPaths)
    {
        if (!TryNormalize(candidatePath, out var candidate))
        {
            return false;
        }

        return NormalizePaths(protectedPaths).Any(protectedPath =>
            PathSafetyService.IsPathWithinOrEqual(candidate, protectedPath) ||
            PathSafetyService.IsPathWithinOrEqual(protectedPath, candidate));
    }

    private static bool TryNormalize(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            var root = Path.GetPathRoot(fullPath);
            normalized = !string.IsNullOrWhiteSpace(root) && fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                ? root
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
