using System.Diagnostics;

namespace WinOptimizationApp.Services;

internal static class AppProcessLauncher
{
    internal const string RunnerPipeNamePrefix = "WinOptimizationApp_Runner_";
    public const string UiArgument = "--ui";
    public const string RunnerArgument = "--runner";
    public const string ConnectRunnerArgument = "--connect-runner";
    public const string RunnerPipeArgument = "--runner-pipe";
    public const string StandaloneArgument = "--standalone";
    public const string BaseDirectoryArgument = "--base-dir";

    public static Process? StartUi(
        bool elevated,
        bool standalone = false,
        bool connectRunner = false,
        string? runnerPipeName = null)
    {
        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = elevated,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
        };

        if (elevated)
        {
            startInfo.Verb = "runas";
        }

        startInfo.Arguments = BuildArguments(
            standalone,
            connectRunner,
            AppRuntimePaths.OriginalBaseDirectory,
            runnerPipeName);

        return Process.Start(startInfo);
    }

    internal static string? GetRunnerPipeName(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (!args[index].Equals(RunnerPipeArgument, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = args[index + 1];
            return IsValidRunnerPipeName(candidate) ? candidate : null;
        }

        return null;
    }

    private static string BuildArguments(
        bool standalone,
        bool connectRunner,
        string originalBaseDirectory,
        string? runnerPipeName)
    {
        var args = new List<string> { UiArgument };
        if (standalone)
        {
            args.Add(StandaloneArgument);
        }
        if (connectRunner)
        {
            args.Add(ConnectRunnerArgument);
            if (IsValidRunnerPipeName(runnerPipeName))
            {
                args.Add(RunnerPipeArgument);
                args.Add(runnerPipeName!);
            }
        }

        args.Add(BaseDirectoryArgument);
        args.Add(originalBaseDirectory);
        return string.Join(" ", args.Select(QuoteArgument));
    }

    internal static string CreateRunnerPipeName() =>
        RunnerPipeNamePrefix + Guid.NewGuid().ToString("N");

    internal static bool IsValidRunnerPipeName(string? pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName) ||
            !pipeName.StartsWith(RunnerPipeNamePrefix, StringComparison.Ordinal) ||
            pipeName.Length != RunnerPipeNamePrefix.Length + 32)
        {
            return false;
        }

        return pipeName.AsSpan(RunnerPipeNamePrefix.Length).ToString().All(Uri.IsHexDigit);
    }

    private static string? ResolveExecutablePath()
    {
        if (IsExistingExecutable(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        var assemblyPath = typeof(AppProcessLauncher).Assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        if (Path.GetExtension(assemblyPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var appHostPath = Path.ChangeExtension(assemblyPath, ".exe");
            return IsExistingExecutable(appHostPath) ? appHostPath : null;
        }

        return IsExistingExecutable(assemblyPath) ? assemblyPath : null;
    }

    private static bool IsExistingExecutable(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(path);
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = argument.Any(char.IsWhiteSpace) || argument.Contains('"') || argument.EndsWith('\\');
        if (!needsQuotes)
        {
            return argument;
        }

        var quoted = new System.Text.StringBuilder();
        quoted.Append('"');

        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', (backslashCount * 2) + 1);
                quoted.Append('"');
            }
            else
            {
                quoted.Append('\\', backslashCount);
                quoted.Append(character);
            }

            backslashCount = 0;
        }

        quoted.Append('\\', backslashCount * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}
