using System;
using System.Collections.Generic;

namespace WinOptimizationApp.Models;

public class CleanerEntry
{
    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = "Applications";
    public int LangSecRef { get; set; }
    public string? SpecialDetect { get; set; }
    public string? Warning { get; set; }
    public bool Default { get; set; } = true;

    // Detect keys and files are used to check if the app is installed
    public List<string> DetectKeys { get; } = new();
    public List<string> DetectFiles { get; } = new();

    // Actual cleaning rules
    public List<FileKeyEntry> FileKeys { get; } = new();
    public List<RegKeyEntry> RegKeys { get; } = new();
    public List<ExcludeKeyEntry> ExcludeKeys { get; } = new();
}

public class FileKeyEntry
{
    public string Path { get; set; } = string.Empty;
    public string Extension { get; set; } = "*.*";
    public bool Recurse { get; set; }

    public static FileKeyEntry Parse(string value)
    {
        var parts = value.Split('|');
        return new FileKeyEntry
        {
            Path = parts[0],
            Extension = parts.Length > 1 ? parts[1] : "*.*",
            Recurse = parts.Length > 2 && parts[2].Equals("RECURSE", StringComparison.OrdinalIgnoreCase)
        };
    }
}

public class RegKeyEntry
{
    public string Root { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }

    public static RegKeyEntry Parse(string value)
    {
        var parts = value.Split('|');
        return new RegKeyEntry
        {
            Root = parts[0],
            Key = parts.Length > 1 ? parts[1] : string.Empty,
            Value = parts.Length > 2 ? parts[2] : null
        };
    }
}

public class ExcludeKeyEntry
{
    public string Path { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;

    public static ExcludeKeyEntry Parse(string value)
    {
        var parts = value.Split('|');
        return new ExcludeKeyEntry
        {
            Path = parts[0],
            Expression = parts.Length > 1 ? parts[1] : string.Empty
        };
    }
}
