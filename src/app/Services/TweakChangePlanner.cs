using System.Text.Json;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed record PlannedTweakChange(string Id, string Title, bool Before, bool After);
public sealed record TweakChangePlan(IReadOnlyList<PlannedTweakChange> Changes, int UnchangedCount, int UnknownCount);

public static class TweakChangePlanner
{
    public static Dictionary<string, bool> ParseProfile(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ParseValues(document.RootElement);
    }

    public static Dictionary<string, bool> ParseValues(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Settings must be a JSON object of IDs and boolean values.");

        var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) ||
                property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !values.TryAdd(property.Name, property.Value.GetBoolean()))
                throw new InvalidDataException("Settings contain an empty/duplicate ID or a non-boolean value.");
        }
        return values;
    }

    public static TweakChangePlan Create(
        IReadOnlyDictionary<string, bool> requested,
        IEnumerable<SystemTweak> catalog,
        IReadOnlyDictionary<string, bool> current)
    {
        var known = catalog.ToDictionary(tweak => tweak.Id, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, bool>(current, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<PlannedTweakChange>();
        var unchanged = 0;
        var unknown = 0;
        foreach (var (id, value) in requested)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                throw new InvalidDataException("Settings contain an empty or duplicate ID.");
            if (!known.TryGetValue(id, out var tweak))
            {
                unknown++;
                continue;
            }
            // Never infer 'off' when detection failed: every change needs a restorable state.
            if (!states.TryGetValue(tweak.Id, out var before))
                throw new InvalidDataException($"Cannot read the current state of {tweak.Title}. Refresh and try again.");
            if (before == value) unchanged++;
            else changes.Add(new PlannedTweakChange(tweak.Id, tweak.Title, before, value));
        }
        return new TweakChangePlan(changes, unchanged, unknown);
    }
}
