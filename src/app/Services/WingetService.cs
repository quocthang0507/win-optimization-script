using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class WingetService
{
    private const string AgreementArguments = "--accept-package-agreements --accept-source-agreements --disable-interactivity";
    private const string SourceAgreementArguments = "--accept-source-agreements --disable-interactivity";
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

        var result = await _commands.RunCaptureAsync("winget.exe", $"upgrade {SourceAgreementArguments}", cancellationToken);
        return result.ExitCode != 0 ? [] : Parse(result.StandardOutput);
    }

    public async Task<WingetPackageUpgradeResult> UpgradePackageAsync(WingetPackage package, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(package);
            var response = await Client.SendRequestAsync("UpgradeWingetPackage", payload);
            return System.Text.Json.JsonSerializer.Deserialize<WingetPackageUpgradeResult>(response)
                ?? new WingetPackageUpgradeResult(package, false, -1, string.Empty, "Failed to deserialize winget upgrade result.");
        }

        if (!_commands.Exists("winget"))
        {
            return new WingetPackageUpgradeResult(package, false, -1, string.Empty, "winget is not available.");
        }

        var result = await _commands.RunCaptureAsync("winget.exe", BuildUpgradeArguments(package), cancellationToken);
        return new WingetPackageUpgradeResult(
            package,
            result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.StandardError),
            result.ExitCode,
            result.StandardOutput.Trim(),
            result.StandardError.Trim());
    }

    public async Task<WingetPackageUpgradeResult> InstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("InstallWingetPackage", packageId);
            return System.Text.Json.JsonSerializer.Deserialize<WingetPackageUpgradeResult>(response)
                ?? new WingetPackageUpgradeResult(new WingetPackage(packageId, packageId, "", "", ""), false, -1, string.Empty, "Failed to deserialize winget install result.");
        }

        if (!_commands.Exists("winget"))
        {
            return new WingetPackageUpgradeResult(new WingetPackage(packageId, packageId, "", "", ""), false, -1, string.Empty, "winget is not available.");
        }

        var result = await _commands.RunCaptureAsync("winget.exe", BuildInstallArguments(packageId), cancellationToken);
        return new WingetPackageUpgradeResult(
            new WingetPackage(packageId, packageId, "", "", ""),
            result.ExitCode == 0,
            result.ExitCode,
            result.StandardOutput.Trim(),
            result.StandardError.Trim());
    }

    public static string BuildUpgradeAllArguments()
    {
        return $"upgrade --all --silent {AgreementArguments}";
    }

    public static string BuildUpgradeArguments(WingetPackage package)
    {
        var source = string.IsNullOrWhiteSpace(package.Source)
            ? string.Empty
            : $" --source {QuoteArgument(package.Source)}";

        return $"upgrade --id {QuoteArgument(package.Id)} --exact --silent{source} {AgreementArguments}";
    }

    public static string BuildInstallArguments(string packageId)
    {
        return $"install --id {QuoteArgument(packageId)} --exact --silent {AgreementArguments}";
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

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
