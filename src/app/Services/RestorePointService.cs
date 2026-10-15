using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class RestorePointService
{
    private readonly CommandRunner _commands;
    private readonly Func<bool> _isAdministrator;

    public RestorePointService(CommandRunner commands, Func<bool>? isAdministrator = null)
    {
        _commands = commands;
        _isAdministrator = isAdministrator ?? SystemStatusService.IsAdministrator;
    }

    public async Task<string> TryCreateAsync(MaintenanceTask task, CancellationToken cancellationToken = default)
    {
        if (!task.CanRollback)
        {
            return "Restore point not required for this task.";
        }

        if (!_isAdministrator())
        {
            return "Restore point skipped: app is not running as Administrator.";
        }

        var description = $"WinOptimizationApp-{task.Id}-{DateTime.Now:yyyyMMddHHmmss}";
        var command = $"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS";
        var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", cancellationToken);

        return result.ExitCode == 0 ? $"Restore point requested: {description}." : $"Restore point failed: {result.StandardError.Trim()}";
    }

    public async Task<(bool Success, string Message)> CreateManualRestorePointAsync(
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isAdministrator())
        {
            return (false, "Administrator privileges required to create a System Restore Point.");
        }

        var desc = SanitizeDescription(description);
        if (desc.Length == 0)
        {
            desc = $"WinOptimizationApp-Manual-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        var command = $"Checkpoint-Computer -Description '{desc}' -RestorePointType MODIFY_SETTINGS";
        var result = await _commands.RunCaptureAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            cancellationToken);

        if (result.ExitCode == 0)
        {
            return (true, desc);
        }

        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return (false, string.IsNullOrWhiteSpace(error) ? $"Failed with exit code {result.ExitCode}" : error);
    }

    /// <summary>
    /// Keeps only characters that are safe inside a single-quoted PowerShell string passed on a
    /// command line (letters, digits, space and <c>-_.,()</c>), collapses whitespace and caps length.
    /// </summary>
    public static string SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(description.Length);
        var lastWasSpace = false;
        foreach (var ch in description.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ',' or '(' or ')')
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch) && !lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        var sanitized = builder.ToString().Trim();
        return sanitized.Length > MaxDescriptionLength ? sanitized[..MaxDescriptionLength].TrimEnd() : sanitized;
    }

    private const int MaxDescriptionLength = 64;
}
