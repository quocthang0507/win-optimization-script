using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class CommandPaletteServiceTests
{
    private static CommandItem CreateCmd(
        string id,
        string title,
        string description = "Test description",
        CommandCategory category = CommandCategory.Navigation,
        params string[] keywords)
    {
        return new CommandItem
        {
            Id = id,
            Title = title,
            Description = description,
            Category = category,
            Keywords = keywords,
            ExecuteAsync = () => Task.CompletedTask
        };
    }

    [Fact]
    public void Filter_ReturnsAllItems_WhenQueryIsEmptyOrWhitespace()
    {
        var commands = new[]
        {
            CreateCmd("cmd1", "Beta", category: CommandCategory.Settings),
            CreateCmd("cmd2", "Alpha", category: CommandCategory.Navigation)
        };

        var result = CommandPaletteService.Filter(commands, "");
        Assert.Equal(2, result.Count);
        // Ordered by Category (Navigation first), then Title
        Assert.Equal("Alpha", result[0].Title);
        Assert.Equal("Beta", result[1].Title);
    }

    [Fact]
    public void Filter_ExactMatch_RanksFirst()
    {
        var commands = new[]
        {
            CreateCmd("dns.custom", "Custom DNS Configuration"),
            CreateCmd("dns.flush", "DNS", "Flush domain cache"),
            CreateCmd("dns.other", "Other DNS Options")
        };

        var result = CommandPaletteService.Filter(commands, "DNS");
        Assert.Equal("DNS", result[0].Title);
    }

    [Fact]
    public void Filter_PrefixMatch_RanksAboveSubstring()
    {
        var commands = new[]
        {
            CreateCmd("sys.restart", "Restart Explorer with Clean Cache"),
            CreateCmd("sys.clean", "Clean System Junk")
        };

        var result = CommandPaletteService.Filter(commands, "Clean");
        Assert.Equal("Clean System Junk", result[0].Title);
    }

    [Fact]
    public void Filter_KeywordMatch_FindsItemByAliases()
    {
        var commands = new[]
        {
            CreateCmd("nav.cleanup", "Cleanup", "Remove temporary files", CommandCategory.Navigation, "dọn dẹp", "rác", "temp"),
            CreateCmd("tool.wsreset", "Reset Windows Store", "Fix Store download issues", CommandCategory.SystemTool, "wsreset", "store")
        };

        var vietnameseResult = CommandPaletteService.Filter(commands, "dọn dẹp");
        Assert.Single(vietnameseResult);
        Assert.Equal("Cleanup", vietnameseResult[0].Title);

        var toolResult = CommandPaletteService.Filter(commands, "wsreset");
        Assert.Single(toolResult);
        Assert.Equal("Reset Windows Store", toolResult[0].Title);
    }

    [Fact]
    public void Filter_ReturnsEmpty_WhenNoMatchesFound()
    {
        var commands = new[]
        {
            CreateCmd("cmd1", "Disk Space"),
            CreateCmd("cmd2", "Startup Manager")
        };

        var result = CommandPaletteService.Filter(commands, "nonexistentquery123");
        Assert.Empty(result);
    }
}
