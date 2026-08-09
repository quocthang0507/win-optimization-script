using System.Diagnostics;
using System.Collections.Concurrent;

namespace WinOptimizationApp.Services;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    private static readonly ConcurrentDictionary<string, CommandPathCacheEntry> CommandPathCache =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private int _executionCount;

    public int ExecutionCount => Volatile.Read(ref _executionCount);

    public async Task<CommandResult> RunCaptureAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _executionCount);
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
                    finalArgs = $"/d /s /c \"\"{resolvedPath}\" {arguments}\"";
                }
                else
                {
                    finalFile = resolvedPath;
                }
            }
            else if (resolvedPath != null)
            {
                finalFile = resolvedPath;
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
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathExtensions = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT"
            : string.Empty;
        var environmentSignature = pathVariable + "\0" + pathExtensions;
        if (CommandPathCache.TryGetValue(command, out var cached) &&
            cached.EnvironmentSignature.Equals(environmentSignature, StringComparison.Ordinal) &&
            ((cached.Path != null && File.Exists(cached.Path)) ||
             (cached.Path == null && DateTimeOffset.UtcNow - cached.CachedAt < TimeSpan.FromSeconds(5))))
        {
            return cached.Path;
        }

        try
        {
            if (Path.IsPathFullyQualified(command) && File.Exists(command))
            {
                var fullPath = Path.GetFullPath(command);
                CommandPathCache[command] = new CommandPathCacheEntry(environmentSignature, fullPath, DateTimeOffset.UtcNow);
                return fullPath;
            }

            var paths = pathVariable
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            var extensions = OperatingSystem.IsWindows()
                ? pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries)
                : [string.Empty];

            foreach (var path in paths)
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(path, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension);
                    if (File.Exists(candidate))
                    {
                        var fullPath = Path.GetFullPath(candidate);
                        CommandPathCache[command] = new CommandPathCacheEntry(environmentSignature, fullPath, DateTimeOffset.UtcNow);
                        return fullPath;
                    }
                }
            }
        }
        catch
        {
            // Cache a short-lived miss below.
        }

        CommandPathCache[command] = new CommandPathCacheEntry(environmentSignature, null, DateTimeOffset.UtcNow);
        return null;
    }

    private sealed record CommandPathCacheEntry(
        string EnvironmentSignature,
        string? Path,
        DateTimeOffset CachedAt);

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
