using System.Text.RegularExpressions;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class ContextMenuService
{
    public const string ClassicMenuClsidSubKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    public const string ClassicMenuInprocSubKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    public const string BlockedExtensionsSubKey = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    private static readonly Regex ClsidRegex = new(
        @"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$",
        RegexOptions.Compiled);

    private static readonly (string Scope, string SubKeySuffix)[] HandlerLocations =
    [
        ("All Files (*)", @"*\shellex\ContextMenuHandlers"),
        ("Folders (Directory)", @"Directory\shellex\ContextMenuHandlers"),
        ("Desktop / Folder Background", @"Directory\Background\shellex\ContextMenuHandlers")
    ];

    private readonly IRegistryAccessor _registry;

    public ContextMenuService(IRegistryAccessor? registry = null)
    {
        _registry = registry ?? new WindowsRegistryAccessor();
    }

    public bool IsClassicContextMenuEnabled()
    {
        var snapshot = _registry.ReadValue("HKCU", ClassicMenuInprocSubKey, "");
        return snapshot.Exists && string.Equals(snapshot.RawValue ?? string.Empty, string.Empty, StringComparison.Ordinal);
    }

    public void SetClassicContextMenu(bool enabled)
    {
        if (enabled)
        {
            _registry.WriteValue("HKCU", ClassicMenuInprocSubKey, "", "String", string.Empty);
        }
        else
        {
            _registry.DeleteSubKeyTree("HKCU", ClassicMenuClsidSubKey);
        }
    }

    public IReadOnlyList<ContextMenuEntry> GetShellExtensions()
    {
        var blockedClsids = GetBlockedClsids();
        var entries = new List<ContextMenuEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in new[] { "HKCU", "HKLM" })
        {
            var classesPrefix = @"Software\Classes\";
            foreach (var (scope, suffix) in HandlerLocations)
            {
                var handlersSubKey = classesPrefix + suffix;
                var handlerNames = _registry.GetSubKeyNames(hive, handlersSubKey);
                foreach (var handlerName in handlerNames)
                {
                    var handlerPath = $"{handlersSubKey}\\{handlerName}";
                    var clsid = ResolveHandlerClsid(hive, handlerPath, handlerName);
                    if (string.IsNullOrWhiteSpace(clsid))
                    {
                        continue;
                    }

                    var dedupKey = $"{scope}|{clsid}";
                    if (!seen.Add(dedupKey))
                    {
                        continue;
                    }

                    var displayName = ClsidRegex.IsMatch(handlerName)
                        ? ResolveClsidFriendlyName(clsid) ?? handlerName
                        : handlerName;
                    var binaryPath = ResolveClsidBinaryPath(clsid);
                    var isEnabled = !blockedClsids.Contains(clsid);

                    entries.Add(new ContextMenuEntry(
                        displayName,
                        clsid,
                        scope,
                        hive,
                        handlerPath,
                        binaryPath,
                        isEnabled));
                }
            }
        }

        return entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Scope, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SetShellExtensionEnabled(string clsid, string displayName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(clsid) || !ClsidRegex.IsMatch(clsid.Trim()))
        {
            throw new ArgumentException("A valid CLSID in {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} format is required.", nameof(clsid));
        }

        var normalizedClsid = clsid.Trim().ToUpperInvariant();
        if (enabled)
        {
            _registry.DeleteValue("HKCU", BlockedExtensionsSubKey, normalizedClsid);
            // Also try unblocking HKCU lowercase variant if present
            _registry.DeleteValue("HKCU", BlockedExtensionsSubKey, clsid.Trim());
        }
        else
        {
            _registry.WriteValue(
                "HKCU",
                BlockedExtensionsSubKey,
                normalizedClsid,
                "String",
                string.IsNullOrWhiteSpace(displayName) ? "Blocked by WinOptimizationApp" : displayName);
        }
    }

    private HashSet<string> GetBlockedClsids()
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { "HKCU", "HKLM" })
        {
            foreach (var valueName in _registry.GetValueNames(hive, BlockedExtensionsSubKey))
            {
                if (ClsidRegex.IsMatch(valueName))
                {
                    blocked.Add(valueName);
                }
            }
        }

        return blocked;
    }

    private string? ResolveHandlerClsid(string hive, string handlerSubKey, string handlerName)
    {
        if (ClsidRegex.IsMatch(handlerName))
        {
            return handlerName.ToUpperInvariant();
        }

        var defaultVal = _registry.ReadValue(hive, handlerSubKey, "");
        if (defaultVal.Exists && !string.IsNullOrWhiteSpace(defaultVal.RawValue))
        {
            var candidate = defaultVal.RawValue.Trim();
            if (ClsidRegex.IsMatch(candidate))
            {
                return candidate.ToUpperInvariant();
            }
        }

        return null;
    }

    private string? ResolveClsidFriendlyName(string clsid)
    {
        foreach (var hive in new[] { "HKCU", "HKLM" })
        {
            var snap = _registry.ReadValue(hive, $@"Software\Classes\CLSID\{clsid}", "");
            if (snap.Exists && !string.IsNullOrWhiteSpace(snap.RawValue))
            {
                return snap.RawValue.Trim();
            }
        }

        return null;
    }

    private string ResolveClsidBinaryPath(string clsid)
    {
        foreach (var hive in new[] { "HKCU", "HKLM" })
        {
            var snap = _registry.ReadValue(hive, $@"Software\Classes\CLSID\{clsid}\InprocServer32", "");
            if (snap.Exists && !string.IsNullOrWhiteSpace(snap.RawValue))
            {
                return snap.RawValue.Trim();
            }
        }

        return string.Empty;
    }
}
