using System.Diagnostics.CodeAnalysis;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class MaintenanceCatalog
{
    public IReadOnlyList<MaintenanceTask> All { get; } = [
        new("cleanup.temp", "Cleanup", "Temporary files", "User and Windows temporary folders.", RiskLevel.Safe, false, false, true, false, "Frees local temporary files."),
        new("cleanup.shaders", "Cleanup", "DirectX shader cache", "Per-user DirectX compiled shader cache.", RiskLevel.Safe, false, false, true, false, "Windows and games rebuild shaders when needed."),
        new("cleanup.crashdumps", "Cleanup", "User crash dumps", "Application crash dumps stored for the current user.", RiskLevel.Medium, false, true, true, false, "Removes local diagnostic dumps; applications are unaffected."),
        new("cleanup.errorreports", "Cleanup", "Windows Error Reporting", "Archived and queued Windows error reports older than 14 days.", RiskLevel.Medium, true, true, true, false, "Keeps recent diagnostics and does not disable Windows Error Reporting."),
        new("cleanup.prefetch", "Cleanup", "Stale Windows Prefetch", "Prefetch entries that have not been updated for at least 30 days.", RiskLevel.Medium, true, true, true, false, "Keeps recent prefetch data so frequently used apps still launch efficiently."),
        new("cleanup.defenderlogs", "Cleanup", "Old Microsoft Defender support logs", "Defender support log and ETL files older than 30 days.", RiskLevel.Medium, true, true, true, false, "Keeps recent security diagnostics and does not remove protection history or quarantine."),
        new("cleanup.systemdumps", "Cleanup", "Old system crash dumps", "Windows memory dump files older than 7 days.", RiskLevel.Medium, true, true, true, false, "Removes old kernel crash diagnostics after the review window."),
        new("cleanup.browser", "Cleanup", "Browser cache", "Edge, Chrome, Firefox, Brave and Opera cache folders.", RiskLevel.Medium, false, true, true, false, "Browsers may reload cached assets."),
        new("cleanup.diskcleanup", "Cleanup", "Disk Cleanup", "Opens Windows Disk Cleanup options, then runs the selected cleanup profile.", RiskLevel.Medium, false, true, true, false, "Uses cleanmgr.exe; review selected categories before running."),
        new("cleanup.dev", "Cleanup", "Developer caches", "NuGet, pip, npm and yarn cache commands.", RiskLevel.Medium, false, true, true, false, "Build tools may download packages again."),
        new("cleanup.windowsupdate", "Cleanup", "Windows Update cache", "SoftwareDistribution and Delivery Optimization caches.", RiskLevel.High, true, true, true, true, "May require services to restart."),
        new("cleanup.recyclebin", "Cleanup", "Recycle Bin", "Empties the current user's recycle bin.", RiskLevel.Safe, false, true, true, false, "Removes deleted files permanently."),
        new("cleanup.windowsold", "Cleanup", "Old Windows installation", "Windows.old from previous upgrades.", RiskLevel.High, true, true, true, true, "Removes rollback files for old Windows installs."),
        new("privacy.clipboard", "Privacy", "Clipboard", "Clears clipboard contents.", RiskLevel.Safe, false, false, false, false, "Removes current clipboard data."),
        new("privacy.recentFiles", "Privacy", "Recent files", "Clears Windows recent documents, jump lists and taskbar recent shortcuts.", RiskLevel.Safe, false, true, true, false, "Removes recent file traces from the current Windows profile."),
        new("privacy.powershell", "Privacy", "PowerShell history", "Clears PSReadLine console history.", RiskLevel.Medium, false, true, true, false, "Command history cannot be recovered from this file."),
        new("privacy.browserHistory", "Privacy", "Browser history", "Clears browsing history databases for supported browsers.", RiskLevel.High, false, true, true, false, "Deletes local browsing history; close browsers first for best results."),
        new("privacy.browserCookies", "Privacy", "Browser cookies and sessions", "Clears browser cookies, session restore files and web session storage.", RiskLevel.High, false, true, true, false, "Signs websites out and removes local browser sessions."),
        new("network.dns", "Repair", "DNS cache", "Runs ipconfig /flushdns.", RiskLevel.Safe, false, false, false, false, "Refreshes cached DNS records."),
        new("repair.dism", "Repair", "DISM RestoreHealth", "Repairs the Windows component store.", RiskLevel.High, true, true, false, true, "Long-running Windows repair command."),
        new("repair.sfc", "Repair", "System File Checker", "Runs sfc /scannow.", RiskLevel.High, true, true, false, true, "Long-running integrity scan."),
        new("repair.explorer", "Repair", "Restart Explorer", "Restarts Windows Explorer.", RiskLevel.Medium, false, true, false, false, "Taskbar and File Explorer windows refresh."),
        new("optimization.hibernate", "Optimization", "Disable hibernation", "Runs powercfg -h off.", RiskLevel.High, true, true, false, true, "Reclaims hiberfil.sys but disables hibernate."),
        new("optimization.drives", "Optimization", "Optimize drives", "Runs Windows drive optimization.", RiskLevel.Medium, true, true, false, false, "TRIM/defrag fixed drives."),
        new("software.winget", "Updates", "WinGet updates", "Scans and upgrades packages through winget.", RiskLevel.Medium, false, true, true, false, "Applications may change versions."),
        new("startup.scan", "Startup", "Startup inventory", "Reads startup registry entries and folders.", RiskLevel.Safe, false, false, true, false, "Read-only inventory."),
        new("settings.storage", "Settings", "Storage Sense", "Opens Windows Storage Sense settings.", RiskLevel.Safe, false, false, false, false, "Uses Windows Settings."),
        new("cli.launch", "Settings", "Launch CLI tool", "Starts src/cli/Utilities.ps1 in PowerShell.", RiskLevel.Medium, true, true, false, false, "Runs the existing console workflow.")
    ];

    public MaintenanceTask GetById(string id)
    {
        return All.First(task => task.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGetById(string id, [NotNullWhen(true)] out MaintenanceTask? task)
    {
        task = All.FirstOrDefault(task => task.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return task is not null;
    }

    public IReadOnlyList<MaintenanceTask> ByGroup(string group)
    {
        return All.Where(task => task.Group.Equals(group, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
