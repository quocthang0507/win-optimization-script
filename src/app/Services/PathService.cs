namespace WinOptimizationApp.Services;

public sealed class PathService
{
    public string RepositoryRoot { get; }
    public string CliScriptPath => Path.Combine(RepositoryRoot, "src", "cli", "Utilities.ps1");
    public string LogsDirectory => AppRuntimePaths.OriginalBaseDirectory;
    public string BackupsDirectory => Path.Combine(AppRuntimePaths.OriginalBaseDirectory, "backups");

    public PathService()
    {
        RepositoryRoot = FindRepositoryRoot(AppRuntimePaths.OriginalBaseDirectory);
    }

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            var cliScript = Path.Combine(directory.FullName, "src", "cli", "Utilities.ps1");
            var plan = Path.Combine(directory.FullName, "docs", "implementation_plan.md");
            if (File.Exists(cliScript) || File.Exists(plan))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppRuntimePaths.OriginalBaseDirectory;
    }
}
