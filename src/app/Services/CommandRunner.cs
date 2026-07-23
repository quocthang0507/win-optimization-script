using System.Diagnostics;

namespace WinOptimizationApp.Services;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    public int ExecutionCount { get; private set; }

    public async Task<CommandResult> RunCaptureAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        Process? process = null;
        try
        {
            var resolvedPath = FindCommandPath(fileName);
            var finalFile = fileName;
            var finalArgs = arguments;

            if (OperatingSystem.IsWindows() && resolvedPath != null)
            {
                var ext = Path.GetExtension(resolvedPath).ToUpperInvariant();

                if (ext is ".CMD" or ".BAT")
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
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            process = Process.Start(startInfo);
            if (process is null)
            {
                return new CommandResult(-1, string.Empty, $"Failed to start {fileName}.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, $"Error starting process {fileName}: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    public static Task<bool> StartShellAsync(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
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

    private static void TryKillProcess(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Cancellation should still propagate if the process exits first.
        }
    }
}
