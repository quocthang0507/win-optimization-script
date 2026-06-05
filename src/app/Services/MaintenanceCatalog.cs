using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class MaintenanceCatalog
{
    public IReadOnlyList<MaintenanceTask> All { get; } = [
        new("cleanup.temp", "Cleanup", "Temporary files", "User and Windows temporary folders.", RiskLevel.Safe, false, false, true, false, "Frees local temporary files."),
        new("cleanup.browser", "Cleanup", "Browser cache", "Edge, Chrome, Firefox, Brave and Opera cache folders.", RiskLevel.Medium, false, true, true, false, "Browsers may reload cached assets."),
        new("cleanup.dev", "Cleanup", "Developer caches", "NuGet, pip, npm and yarn cache commands.", RiskLevel.Medium, false, true, true, false, "Build tools may download packages again."),
        new("cleanup.windowsupdate", "Cleanup", "Windows Update cache", "SoftwareDistribution and Delivery Optimization caches.", RiskLevel.High, true, true, true, true, "May require services to restart."),
        new("cleanup.recyclebin", "Cleanup", "Recycle Bin", "Empties the current user's recycle bin.", RiskLevel.Safe, false, true, true, false, "Removes deleted files permanently."),
        new("cleanup.windowsold", "Cleanup", "Old Windows installation", "Windows.old from previous upgrades.", RiskLevel.High, true, true, true, true, "Removes rollback files for old Windows installs."),
        new("privacy.clipboard", "Privacy", "Clipboard", "Clears clipboard contents.", RiskLevel.Safe, false, false, false, false, "Removes current clipboard data."),
        new("privacy.powershell", "Privacy", "PowerShell history", "Clears PSReadLine console history.", RiskLevel.Medium, false, true, true, false, "Command history cannot be recovered from this file."),
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

    public IReadOnlyList<MaintenanceTask> ByGroup(string group)
    {
        return All.Where(task => task.Group.Equals(group, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
