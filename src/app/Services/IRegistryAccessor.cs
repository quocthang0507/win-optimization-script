using Microsoft.Win32;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public interface IRegistryAccessor
{
    RegistryValueSnapshot ReadValue(string hive, string subKey, string valueName);
    void WriteValue(string hive, string subKey, string valueName, string valueKind, string? rawValue);
    void DeleteValue(string hive, string subKey, string valueName);
    bool KeyExists(string hive, string subKey);
    void CreateKey(string hive, string subKey);
    void DeleteSubKeyTree(string hive, string subKey);
    IReadOnlyList<string> GetSubKeyNames(string hive, string subKey);
    IReadOnlyList<string> GetValueNames(string hive, string subKey);
}

public sealed class WindowsRegistryAccessor : IRegistryAccessor
{
    public RegistryValueSnapshot ReadValue(string hive, string subKey, string valueName)
    {
        using var root = OpenHive(hive);
        using var key = root.OpenSubKey(NormalizeSubKey(subKey), writable: false);
        if (key is null)
        {
            return new RegistryValueSnapshot(NormalizeHive(hive), NormalizeSubKey(subKey), valueName, false, "None", null);
        }

        var raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is null)
        {
            return new RegistryValueSnapshot(NormalizeHive(hive), NormalizeSubKey(subKey), valueName, false, "None", null);
        }

        var kind = key.GetValueKind(valueName);
        return new RegistryValueSnapshot(
            NormalizeHive(hive),
            NormalizeSubKey(subKey),
            valueName,
            true,
            kind.ToString(),
            FormatRawValue(raw, kind));
    }

    public void WriteValue(string hive, string subKey, string valueName, string valueKind, string? rawValue)
    {
        using var root = OpenHive(hive);
        using var key = root.CreateSubKey(NormalizeSubKey(subKey), writable: true)
            ?? throw new InvalidOperationException($"Cannot create or open registry key {hive}\\{subKey}.");

        var parsedKind = ParseKind(valueKind);
        var value = ConvertRawValue(rawValue, parsedKind);
        key.SetValue(valueName, value, parsedKind);
    }

    public void DeleteValue(string hive, string subKey, string valueName)
    {
        using var root = OpenHive(hive);
        using var key = root.OpenSubKey(NormalizeSubKey(subKey), writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public bool KeyExists(string hive, string subKey)
    {
        using var root = OpenHive(hive);
        using var key = root.OpenSubKey(NormalizeSubKey(subKey), writable: false);
        return key is not null;
    }

    public void CreateKey(string hive, string subKey)
    {
        using var root = OpenHive(hive);
        using var key = root.CreateSubKey(NormalizeSubKey(subKey), writable: true);
    }

    public void DeleteSubKeyTree(string hive, string subKey)
    {
        using var root = OpenHive(hive);
        root.DeleteSubKeyTree(NormalizeSubKey(subKey), throwOnMissingSubKey: false);
    }

    public IReadOnlyList<string> GetSubKeyNames(string hive, string subKey)
    {
        using var root = OpenHive(hive);
        using var key = root.OpenSubKey(NormalizeSubKey(subKey), writable: false);
        return key?.GetSubKeyNames() ?? [];
    }

    public IReadOnlyList<string> GetValueNames(string hive, string subKey)
    {
        using var root = OpenHive(hive);
        using var key = root.OpenSubKey(NormalizeSubKey(subKey), writable: false);
        return key?.GetValueNames() ?? [];
    }

    internal static string NormalizeHive(string hive)
    {
        var trimmed = hive.Trim().TrimEnd(':').ToUpperInvariant();
        return trimmed switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => "HKLM",
            "HKCU" or "HKEY_CURRENT_USER" => "HKCU",
            "HKCR" or "HKEY_CLASSES_ROOT" => "HKCR",
            "HKU" or "HKEY_USERS" => "HKU",
            _ => throw new ArgumentException($"Unsupported registry hive: {hive}", nameof(hive))
        };
    }

    internal static string NormalizeSubKey(string subKey)
    {
        return subKey.Trim().TrimStart('\\', '/');
    }

    private static RegistryKey OpenHive(string hive)
    {
        return NormalizeHive(hive) switch
        {
            "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default),
            "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
            "HKCR" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default),
            "HKU" => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default),
            _ => throw new ArgumentException($"Unsupported registry hive: {hive}", nameof(hive))
        };
    }

    private static RegistryValueKind ParseKind(string valueKind)
    {
        return valueKind.Trim().ToUpperInvariant() switch
        {
            "DWORD" => RegistryValueKind.DWord,
            "QWORD" => RegistryValueKind.QWord,
            "STRING" => RegistryValueKind.String,
            "EXPANDSTRING" => RegistryValueKind.ExpandString,
            "MULTISTRING" => RegistryValueKind.MultiString,
            "BINARY" => RegistryValueKind.Binary,
            _ => RegistryValueKind.String
        };
    }

    private static string? FormatRawValue(object raw, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord => Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.Binary when raw is byte[] bytes => Convert.ToBase64String(bytes),
            RegistryValueKind.MultiString when raw is string[] lines => System.Text.Json.JsonSerializer.Serialize(lines),
            _ => raw.ToString()
        };
    }

    private static object ConvertRawValue(string? rawValue, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord => int.Parse(rawValue ?? "0", System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(rawValue ?? "0", System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => string.IsNullOrEmpty(rawValue) ? Array.Empty<byte>() : Convert.FromBase64String(rawValue),
            RegistryValueKind.MultiString => string.IsNullOrEmpty(rawValue)
                ? Array.Empty<string>()
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(rawValue) ?? Array.Empty<string>(),
            _ => rawValue ?? string.Empty
        };
    }
}

public sealed class InMemoryRegistryAccessor : IRegistryAccessor
{
    private readonly Dictionary<string, Dictionary<string, (string Kind, string? Value)>> _keys =
        new(StringComparer.OrdinalIgnoreCase);

    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }

    public RegistryValueSnapshot ReadValue(string hive, string subKey, string valueName)
    {
        ReadCount++;
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        var path = $"{normHive}\\{normSubKey}";
        if (_keys.TryGetValue(path, out var values) && values.TryGetValue(valueName, out var entry))
        {
            return new RegistryValueSnapshot(normHive, normSubKey, valueName, true, entry.Kind, entry.Value);
        }

        return new RegistryValueSnapshot(normHive, normSubKey, valueName, false, "None", null);
    }

    public void WriteValue(string hive, string subKey, string valueName, string valueKind, string? rawValue)
    {
        WriteCount++;
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        EnsureKeyHierarchy(normHive, normSubKey);
        var path = $"{normHive}\\{normSubKey}";
        _keys[path][valueName] = (valueKind, rawValue);
    }

    public void DeleteValue(string hive, string subKey, string valueName)
    {
        WriteCount++;
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        var path = $"{normHive}\\{normSubKey}";
        if (_keys.TryGetValue(path, out var values))
        {
            values.Remove(valueName);
        }
    }

    public bool KeyExists(string hive, string subKey)
    {
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        return _keys.ContainsKey($"{normHive}\\{normSubKey}");
    }

    public void CreateKey(string hive, string subKey)
    {
        WriteCount++;
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        EnsureKeyHierarchy(normHive, normSubKey);
    }

    public void DeleteSubKeyTree(string hive, string subKey)
    {
        WriteCount++;
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        var prefix = $"{normHive}\\{normSubKey}";
        var toRemove = _keys.Keys
            .Where(k => k.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in toRemove)
        {
            _keys.Remove(k);
        }
    }

    public IReadOnlyList<string> GetSubKeyNames(string hive, string subKey)
    {
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        var prefix = string.IsNullOrEmpty(normSubKey) ? $"{normHive}\\" : $"{normHive}\\{normSubKey}\\";
        var children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = key[prefix.Length..];
                var slash = remainder.IndexOf('\\');
                var childName = slash >= 0 ? remainder[..slash] : remainder;
                if (!string.IsNullOrEmpty(childName))
                {
                    children.Add(childName);
                }
            }
        }

        return children.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> GetValueNames(string hive, string subKey)
    {
        var normHive = WindowsRegistryAccessor.NormalizeHive(hive);
        var normSubKey = WindowsRegistryAccessor.NormalizeSubKey(subKey);
        var path = $"{normHive}\\{normSubKey}";
        return _keys.TryGetValue(path, out var values)
            ? values.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
    }

    private void EnsureKeyHierarchy(string normHive, string normSubKey)
    {
        var parts = normSubKey.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var current = normHive;
        foreach (var part in parts)
        {
            current = $"{current}\\{part}";
            if (!_keys.ContainsKey(current))
            {
                _keys[current] = new Dictionary<string, (string Kind, string? Value)>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
