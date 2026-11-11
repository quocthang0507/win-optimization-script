using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class StorageSenseService
{
    public const string StoragePolicyHive = "HKCU";
    public const string StoragePolicySubKey = @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy";

    private readonly CommandRunner _commands;
    private readonly IRegistryAccessor _registry;

    public StorageSenseService(CommandRunner commands, IRegistryAccessor? registry = null)
    {
        _commands = commands;
        _registry = registry ?? new WindowsRegistryAccessor();
    }

    public StorageSenseState GetStorageSenseState()
    {
        var enabled = ReadDword("01", 0) != 0;
        var tempFiles = ReadDword("04", 1) != 0;
        var recycleBin = ReadDword("08", 1) != 0;
        var recycleThreshold = ReadDword("256", 30);
        var frequencyDays = ReadDword("2048", 0);

        return new StorageSenseState(
            enabled,
            tempFiles,
            recycleBin,
            recycleThreshold,
            frequencyDays);
    }

    public StorageSenseState ConfigureStorageSense(
        bool enabled,
        bool cleanTemporaryFiles = true,
        bool cleanRecycleBin = true,
        int recycleBinThresholdDays = 30,
        int runFrequencyDays = 30)
    {
        var normalizedFrequency = runFrequencyDays switch
        {
            0 or 1 or 7 or 30 => runFrequencyDays,
            _ => 30
        };
        var normalizedRecycleDays = recycleBinThresholdDays switch
        {
            0 or 1 or 14 or 30 or 60 => recycleBinThresholdDays,
            _ => 30
        };

        _registry.WriteValue(StoragePolicyHive, StoragePolicySubKey, "01", "DWord", enabled ? "1" : "0");
        _registry.WriteValue(StoragePolicyHive, StoragePolicySubKey, "04", "DWord", cleanTemporaryFiles ? "1" : "0");
        _registry.WriteValue(StoragePolicyHive, StoragePolicySubKey, "08", "DWord", cleanRecycleBin ? "1" : "0");
        _registry.WriteValue(StoragePolicyHive, StoragePolicySubKey, "256", "DWord", normalizedRecycleDays.ToString());
        _registry.WriteValue(StoragePolicyHive, StoragePolicySubKey, "2048", "DWord", normalizedFrequency.ToString());

        return GetStorageSenseState();
    }

    public async Task<CompactOsState> QueryCompactOsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunCaptureAsync(
            "compact.exe",
            "/CompactOS:query",
            cancellationToken);

        return ParseCompactOsQueryOutput(result.StandardOutput, result.StandardError, result.ExitCode);
    }

    public async Task<CommandResult> SetCompactOsAsync(bool enableCompression, CancellationToken cancellationToken = default)
    {
        var arg = enableCompression ? "/CompactOS:always" : "/CompactOS:never";
        return await _commands.RunCaptureAsync(
            "compact.exe",
            arg,
            cancellationToken);
    }

    public static CompactOsState ParseCompactOsQueryOutput(string stdOut, string stdErr, int exitCode)
    {
        var combined = string.Join(
            Environment.NewLine,
            new[] { stdOut, stdErr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

        if (string.IsNullOrWhiteSpace(combined))
        {
            return new CompactOsState(exitCode == 0, false, string.Empty);
        }

        var normalized = combined.ToLowerInvariant();
        var notCompact = normalized.Contains("not in the compact state", StringComparison.Ordinal) ||
                         normalized.Contains("is not compressed", StringComparison.Ordinal) ||
                         normalized.Contains("không ở trạng thái nén", StringComparison.Ordinal);
        var isCompact = !notCompact &&
                        (normalized.Contains("in the compact state", StringComparison.Ordinal) ||
                         normalized.Contains("compact state", StringComparison.Ordinal));

        var firstLine = combined
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? combined;

        return new CompactOsState(exitCode == 0 || isCompact || notCompact, isCompact, firstLine);
    }

    private int ReadDword(string valueName, int defaultValue)
    {
        var snapshot = _registry.ReadValue(StoragePolicyHive, StoragePolicySubKey, valueName);
        if (!snapshot.Exists || string.IsNullOrWhiteSpace(snapshot.RawValue))
        {
            return defaultValue;
        }

        return int.TryParse(snapshot.RawValue, out var parsed) ? parsed : defaultValue;
    }
}
