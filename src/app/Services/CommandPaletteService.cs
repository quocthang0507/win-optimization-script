using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class CommandPaletteService
{
    public static IReadOnlyList<CommandItem> Filter(IEnumerable<CommandItem> commands, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return commands.OrderBy(c => c.Category).ThenBy(c => c.Title).ToList();
        }

        var trimmed = query.Trim();

        return commands
            .Select(c => new { Command = c, Score = CalculateScore(c, trimmed) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Command.Title)
            .Select(x => x.Command)
            .ToList();
    }

    public static int CalculateScore(CommandItem command, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var score = 0;

        if (string.Equals(command.Title, query, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }
        else if (command.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
        }
        else if (command.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 35;
        }

        if (string.Equals(command.Id, query, StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
        }
        else if (command.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        foreach (var keyword in command.Keywords)
        {
            if (string.Equals(keyword, query, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
                break;
            }

            if (keyword.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
                break;
            }

            if (keyword.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
                break;
            }
        }

        if (command.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }
}
