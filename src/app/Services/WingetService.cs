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
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("ScanWinget", cancellationToken: cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<WingetPackage>>(response) ?? [];
        }
        if (!_commands.Exists("winget"))
        {
            throw new InvalidOperationException("WinGet is not installed or is not available on PATH.");
        }

        var result = await _commands.RunCaptureAsync("winget.exe", $"upgrade {SourceAgreementArguments}", cancellationToken);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? $"WinGet scan failed with exit code {result.ExitCode}."
                : detail);
        }

        return Parse(result.StandardOutput);
    }

    public async Task<WingetPackageUpgradeResult> UpgradePackageAsync(WingetPackage package, CancellationToken cancellationToken = default)
    {
        if (!IsValidPackageId(package.Id))
        {
            return new WingetPackageUpgradeResult(package, false, -1, string.Empty, "Invalid WinGet package ID.");
        }
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(package);
            var response = await Client.SendRequestAsync("UpgradeWingetPackage", payload, cancellationToken);
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
            result.ExitCode == 0,
            result.ExitCode,
            result.StandardOutput.Trim(),
            result.StandardError.Trim());
    }

    public async Task<WingetPackageUpgradeResult> DownloadPackageAsync(
        WingetPackage package,
        string downloadDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidPackageId(package.Id))
        {
            return new WingetPackageUpgradeResult(package, false, -1, string.Empty, "Invalid WinGet package ID.");
        }
        if (!IsSafeDownloadDirectory(downloadDirectory))
        {
            return new WingetPackageUpgradeResult(package, false, -1, string.Empty, "The selected download directory is protected or invalid.");
        }

        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new WingetPackageDownloadRequest(package, downloadDirectory));
            var response = await Client.SendRequestAsync("DownloadWingetPackage", payload, cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<WingetPackageUpgradeResult>(response)
                ?? new WingetPackageUpgradeResult(package, false, -1, string.Empty, "Failed to deserialize winget download result.");
        }

        if (!_commands.Exists("winget"))
        {
            return new WingetPackageUpgradeResult(package, false, -1, string.Empty, "winget is not available.");
        }

        Directory.CreateDirectory(downloadDirectory);
        var result = await _commands.RunCaptureAsync("winget.exe", BuildDownloadArguments(package, downloadDirectory), cancellationToken);
        return new WingetPackageUpgradeResult(
            package,
            result.ExitCode == 0,
            result.ExitCode,
            result.StandardOutput.Trim(),
            result.StandardError.Trim());
    }

    public async Task<WingetPackageUpgradeResult> InstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (!IsValidPackageId(packageId))
        {
            return new WingetPackageUpgradeResult(
                new WingetPackage(packageId, packageId, "", "", ""),
                false,
                -1,
                string.Empty,
                "Invalid WinGet package ID.");
        }

        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("InstallWingetPackage", packageId, cancellationToken);
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

    public static string BuildDownloadArguments(WingetPackage package, string downloadDirectory)
    {
        var source = string.IsNullOrWhiteSpace(package.Source)
            ? string.Empty
            : $" --source {QuoteArgument(package.Source)}";

        return $"download --id {QuoteArgument(package.Id)} --exact{source} --download-directory {QuoteArgument(downloadDirectory)} {AgreementArguments}";
    }

    public static string BuildInstallArguments(string packageId)
    {
        return $"install --id {QuoteArgument(packageId)} --exact --silent {AgreementArguments}";
    }

    public static bool IsValidPackageId(string packageId)
    {
        return !string.IsNullOrWhiteSpace(packageId) &&
               packageId.Length <= 255 &&
               packageId.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');
    }

    public static bool IsSafeDownloadDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        try
        {
            var fullPath = Path.GetFullPath(directory).TrimEnd('\\', '/');
            var root = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(fullPath) || fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)) return false;

            string[] protectedTrees =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            ];
            return !protectedTrees.Where(path => !string.IsNullOrWhiteSpace(path))
                .Any(path => PathSafetyService.IsPathWithinOrEqual(fullPath, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
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

    public async Task<WingetPackageDetails?> ShowPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (!_commands.Exists("winget"))
        {
            return null;
        }

        var result = await _commands.RunCaptureAsync("winget.exe", $"show --id {QuoteArgument(packageId)} --exact", cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        return ParseShowOutput(packageId, result.StandardOutput);
    }

    private static WingetPackageDetails ParseShowOutput(string packageId, string output)
    {
        var details = new WingetPackageDetails { Id = packageId };
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string currentMultiLineKey = "";
        var multiLineValues = new List<string>();

        void FlushMultiLine()
        {
            if (!string.IsNullOrEmpty(currentMultiLineKey))
            {
                var combined = string.Join("\n", multiLineValues).Trim();
                if (combined.StartsWith("'") && combined.EndsWith("'") && combined.Length >= 2)
                    combined = combined.Substring(1, combined.Length - 2).Trim();
                if (combined.StartsWith("\"") && combined.EndsWith("\"") && combined.Length >= 2)
                    combined = combined.Substring(1, combined.Length - 2).Trim();

                switch (currentMultiLineKey.ToLowerInvariant())
                {
                    case "description":
                        details.Description = combined;
                        break;
                    case "release notes":
                    case "releasenotes":
                        details.ReleaseNotes = combined;
                        break;
                    case "tags":
                        details.Tags = multiLineValues.Select(t => t.Trim())
                                                      .Select(t => t.StartsWith("- ") ? t.Substring(2).Trim() : t)
                                                      .Where(t => !string.IsNullOrEmpty(t))
                                                      .ToList();
                        break;
                }
                currentMultiLineKey = "";
                multiLineValues.Clear();
            }
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("  ") && !string.IsNullOrEmpty(currentMultiLineKey))
            {
                multiLineValues.Add(line.Trim());
                continue;
            }

            if (line.TrimStart().StartsWith("- ") && !string.IsNullOrEmpty(currentMultiLineKey))
            {
                multiLineValues.Add(line.TrimStart().Substring(2).Trim());
                continue;
            }

            FlushMultiLine();

            int colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                string key = line.Substring(0, colonIndex).Trim();
                string val = line.Substring(colonIndex + 1).Trim();

                bool isMultiLineKey = key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                                      key.Equals("releasenotes", StringComparison.OrdinalIgnoreCase) ||
                                      key.Equals("release notes", StringComparison.OrdinalIgnoreCase) ||
                                      key.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
                                      key.Equals("documentations", StringComparison.OrdinalIgnoreCase) ||
                                      key.Equals("documentation", StringComparison.OrdinalIgnoreCase);

                if (isMultiLineKey)
                {
                    currentMultiLineKey = key;
                    if (!string.IsNullOrEmpty(val))
                    {
                        multiLineValues.Add(val);
                    }
                }
                else if (string.IsNullOrEmpty(val))
                {
                    currentMultiLineKey = key;
                }
                else
                {
                    // If val starts with quote and ends with quote, strip them
                    if (val.StartsWith("'") && val.EndsWith("'") && val.Length >= 2) val = val.Substring(1, val.Length - 2).Trim();
                    if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2) val = val.Substring(1, val.Length - 2).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "version":
                        case "packageversion":
                            details.Version = val;
                            break;
                        case "publisher":
                            details.Publisher = val;
                            break;
                        case "publisher url":
                        case "publisherurl":
                            details.PublisherUrl = val;
                            break;
                        case "homepage":
                        case "packageurl":
                            details.Homepage = val;
                            break;
                        case "license":
                            details.License = val;
                            break;
                        case "license url":
                        case "licenseurl":
                            details.LicenseUrl = val;
                            break;
                        case "release notes url":
                        case "releasenotesurl":
                            details.ReleaseNotesUrl = val;
                            break;
                        case "installer url":
                        case "installerurl":
                            details.InstallerUrl = val;
                            break;
                        case "installer type":
                        case "installertype":
                            details.InstallerType = val;
                            break;
                        case "packagename":
                            details.Name = val;
                            break;
                    }
                }
            }
            else if (line.StartsWith("Found ") && line.Contains("[") && line.Contains("]"))
            {
                int nameStart = 6;
                int nameEnd = line.IndexOf('[');
                if (nameEnd > nameStart)
                {
                    details.Name = line.Substring(nameStart, nameEnd - nameStart).Trim();
                }
            }
        }

        FlushMultiLine();
        return details;
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
