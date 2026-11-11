namespace WinOptimizationApp.Models;

public sealed record StorageSenseState(
    bool IsEnabled,
    bool CleanTemporaryFiles,
    bool CleanRecycleBin,
    int RecycleBinThresholdDays,
    int RunFrequencyDays);

public sealed record CompactOsState(
    bool IsSupported,
    bool IsCompressed,
    string RawSummary);
