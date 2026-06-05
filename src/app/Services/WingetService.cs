using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class WingetService
{
    private readonly CommandRunner _commands;

    public IpcClient? Client { get; set; }

    public WingetService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<IReadOnlyList<WingetPackage>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("ScanWinget");
            return System.Text.Json.JsonSerializer.Deserialize<List<WingetPackage>>(response) ?? [];
        }
        if (!_commands.Exists("winget"))
        {
            return [];
        }

        var result = await _commands.RunCaptureAsync("winget.exe", "upgrade", cancellationToken);
        return result.ExitCode != 0 ? [] : Parse(result.StandardOutput);
    }

    private static IReadOnlyList<WingetPackage> Parse(string output)
    {
        var packages = new List<WingetPackage>();
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var separatorIndex = Array.FindIndex(lines, line => line.TrimStart().StartsWith("---", StringComparison.Ordinal));
        if (separatorIndex < 0)
        {
            return packages;
        }

        foreach (var line in lines.Skip(separatorIndex + 1))
        {
            if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("No installed package", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var source = parts[^1];
            var available = parts[^2];
            var installed = parts[^3];
            var id = parts[^4];
            var name = string.Join(' ', parts[..^4]);
            packages.Add(new WingetPackage(name, id, installed, available, source));
        }

        return packages;
    }
}
