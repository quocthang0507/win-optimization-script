using System.Diagnostics;
using System.IO;
using System;

namespace WinOptimizationApp.Services;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    public int ExecutionCount { get; private set; }

    public async Task<CommandResult> RunCaptureAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        try
        {
            var resolvedPath = FindCommandPath(fileName);
            var finalFile = fileName;
            var finalArgs = arguments;

            if (OperatingSystem.IsWindows() && resolvedPath != null)
            {
                var ext = Path.GetExtension(resolvedPath).ToUpperInvariant();
                if (ext == ".CMD" || ext == ".BAT")
                {
                    finalFile = "cmd.exe";
                    finalArgs = $"/c {fileName} {arguments}";
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = finalFile,
                Arguments = finalArgs,
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
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, $"Error starting process {fileName}: {ex.Message}");
        }
    }

    public static Task StartShellAsync(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch
        {
            // Best-effort shell start
        }

        return Task.CompletedTask;
    }

    public bool Exists(string command)
    {
        _ = ExecutionCount;
        return FindCommandPath(command) != null;
    }

    private static string? FindCommandPath(string command)
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
                        return candidate;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
