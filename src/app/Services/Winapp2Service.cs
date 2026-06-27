using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public class Winapp2Service
{
    private readonly Winapp2Parser _parser;
    private List<CleanerEntry>? _cachedEntries;

    public Winapp2Service()
    {
        _parser = new Winapp2Parser();
    }

    public async Task<List<CleanerEntry>> GetDetectedEntriesAsync()
    {
        if (_cachedEntries != null)
        {
            return _cachedEntries;
        }

        var dbPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Database", "Winapp2.ini");
        var allEntries = await _parser.ParseFileAsync(dbPath);

        var detected = new List<CleanerEntry>();

        await Task.Run(() =>
        {
            foreach (var entry in allEntries)
            {
                if (IsDetected(entry))
                {
                    detected.Add(entry);
                }
            }
        });

        _cachedEntries = detected.OrderBy(e => e.Name).ToList();
        return _cachedEntries;
    }

    private static bool IsDetected(CleanerEntry entry)
    {
        // Simple detection: if any of the target folders exist, we consider it detected
        foreach (var fileKey in entry.FileKeys)
        {
            var expandedPath = PathExpander.Expand(fileKey.Path);
            if (!string.IsNullOrWhiteSpace(expandedPath) && Directory.Exists(expandedPath))
            {
                return true;
            }
        }
        return false;
    }
}
