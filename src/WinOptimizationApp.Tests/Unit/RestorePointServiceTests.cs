using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class RestorePointServiceTests
{
    [Theory]
    [InlineData("Before Optimization", "Before Optimization")]
    [InlineData("Before; Drop Table; `rm -rf /` \"quoted\"", "Before Drop Table rm -rf quoted")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    [InlineData("Point (1) - Clean.Up, System_Test", "Point (1) - Clean.Up, System_Test")]
    [InlineData("Test $env:PATH & echo 'hacked'", "Test envPATH echo hacked")]
    public void SanitizeDescription_CleansDangerousCharacters(string? input, string expected)
    {
        var sanitized = RestorePointService.SanitizeDescription(input);
        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void SanitizeDescription_TruncatesLongInputTo64Chars()
    {
        var longInput = new string('A', 100);
        var sanitized = RestorePointService.SanitizeDescription(longInput);
        Assert.Equal(64, sanitized.Length);
        Assert.Equal(new string('A', 64), sanitized);
    }

    [Fact]
    public async Task CreateManualRestorePoint_ReturnsFalse_WhenNotAdministrator()
    {
        var service = new RestorePointService(new CommandRunner(), isAdministrator: () => false);
        var (success, message) = await service.CreateManualRestorePointAsync("Test Point");
        Assert.False(success);
        Assert.Contains("Administrator", message, StringComparison.OrdinalIgnoreCase);
    }
}
