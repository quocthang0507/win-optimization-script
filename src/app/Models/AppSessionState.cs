namespace WinOptimizationApp.Models;

/// <summary>
/// Holds machine state collected for the current app session. Pages consume this
/// snapshot instead of starting a new scan every time the user navigates.
/// </summary>
public sealed class AppSessionState
{
    public DashboardStatus? SystemOverview { get; internal set; }
    public string? SystemOverviewError { get; internal set; }

    public HealthCheckScanMetrics? HealthMetrics { get; internal set; }
    public string? HealthMetricsError { get; internal set; }
    public int DashboardRevision { get; internal set; }

    public IReadOnlyList<StartupEntry> StartupEntries { get; internal set; } = [];
    public string? StartupError { get; internal set; }
    public bool StartupLoaded { get; internal set; }
    public int StartupRevision { get; internal set; }

    public IReadOnlyList<WingetPackage> UpdatePackages { get; internal set; } = [];
    public string? UpdatesError { get; internal set; }
    public bool UpdatesLoaded { get; internal set; }
    public int UpdatesRevision { get; internal set; }

    public IReadOnlyList<InstalledApp> AppxPackages { get; internal set; } = [];
    public string? AppxError { get; internal set; }
    public bool AppxLoaded { get; internal set; }
    public int AppxRevision { get; internal set; }

    public IReadOnlyDictionary<string, TweakStateResponse> TweakStates { get; internal set; }
        = new Dictionary<string, TweakStateResponse>(StringComparer.OrdinalIgnoreCase);
    public bool TweakStatesLoaded { get; internal set; }
    public int TweakStatesRevision { get; internal set; }

    public IReadOnlyList<NetworkAdapterInfo> NetworkAdapters { get; internal set; } = [];
    public string? NetworkAdaptersError { get; internal set; }
    public bool NetworkAdaptersLoaded { get; internal set; }
    public int NetworkAdaptersRevision { get; internal set; }

    public IReadOnlyList<CleanerEntry> Winapp2Entries { get; internal set; } = [];
    public string? Winapp2Error { get; internal set; }
    public bool Winapp2Loaded { get; internal set; }
    public int Winapp2Revision { get; internal set; }
}
