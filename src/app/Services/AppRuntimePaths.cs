namespace WinOptimizationApp.Services;

internal static class AppRuntimePaths
{
    public static string OriginalBaseDirectory { get; } = ResolveOriginalBaseDirectory();

    private static string ResolveOriginalBaseDirectory()
    {
        var args = Environment.GetCommandLineArgs();
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(AppProcessLauncher.BaseDirectoryArgument, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = args[index + 1];
                if (Directory.Exists(candidate))
                {
                    return EnsureTrailingSeparator(Path.GetFullPath(candidate));
                }
            }
        }

        return EnsureTrailingSeparator(AppContext.BaseDirectory);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
}
