using System.Diagnostics;

namespace WinOptimizationApp.Services;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    public async Task<CommandResult> RunCaptureAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new CommandResult(-1, string.Empty, $"Failed to start {fileName}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, await outputTask, await errorTask);
    }

    public Task StartShellAsync(string fileName, string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    public bool Exists(string command)
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            var extensions = OperatingSystem.IsWindows()
                ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                : [string.Empty];

            foreach (var path in paths)
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(path, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension);
                    if (File.Exists(candidate))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
