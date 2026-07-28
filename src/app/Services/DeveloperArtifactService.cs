namespace WinOptimizationApp.Services;

public static class DeveloperArtifactService
{
    public static bool IsArtifactDirectory(DirectoryInfo directory)
    {
        var parent = directory.Parent;
        if (parent is null)
        {
            return false;
        }

        var name = directory.Name.ToLowerInvariant();
        return name switch
        {
            "node_modules" or ".next" or "dist" => HasAnyFile(parent, "package.json", "pnpm-lock.yaml", "yarn.lock"),
            "target" => HasAnyFile(parent, "Cargo.toml"),
            "bin" or "obj" => HasDotNetProjectMarker(parent),
            "build" or ".gradle" => HasAnyFile(parent, "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts"),
            ".venv" => HasAnyFile(parent, "pyproject.toml", "requirements.txt", "Pipfile"),
            _ => false
        };
    }

    private static bool HasAnyFile(DirectoryInfo directory, params string[] names)
    {
        return names.Any(name => File.Exists(Path.Combine(directory.FullName, name)));
    }

    private static bool HasDotNetProjectMarker(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                .Any(file => file.Extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                             file.Extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                             file.Extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
