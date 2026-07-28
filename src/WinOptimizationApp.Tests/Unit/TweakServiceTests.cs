using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class TweakServiceTests
{
    [Fact]
    public void GetAllTweaks_ReturnsPredefinedTweaks()
    {
        var service = new TweakService(new CommandRunner());
        var tweaks = service.GetAllTweaks();

        Assert.NotNull(tweaks);
        Assert.NotEmpty(tweaks);
        
        // Verify expected categories
        Assert.Contains(tweaks, t => t.Category == "Privacy");
        Assert.Contains(tweaks, t => t.Category == "Gaming");
        Assert.Contains(tweaks, t => t.Category == "UI/Taskbar");
        Assert.Contains(tweaks, t => t.Category == "System");
    }

    [Fact]
    public void Tweaks_HaveRequiredScripts()
    {
        var service = new TweakService(new CommandRunner());
        var tweaks = service.GetAllTweaks();

        foreach (var tweak in tweaks)
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Id));
            Assert.False(string.IsNullOrWhiteSpace(tweak.CheckScript));
            Assert.False(string.IsNullOrWhiteSpace(tweak.EnableScript));
            Assert.False(string.IsNullOrWhiteSpace(tweak.DisableScript));
        }
    }

    [Fact]
    public void Profiles_ReferenceOnlyKnownTweaks()
    {
        var knownIds = TweakService.Tweaks.Select(tweak => tweak.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in TweakProfileCatalog.All)
        {
            Assert.NotEmpty(profile.Values);
            Assert.All(profile.Values.Keys, id => Assert.Contains(id, knownIds));
        }
    }

    [Fact]
    public void CuratedReferenceTweaks_ArePresentAndReversible()
    {
        var tweaks = new TweakService(new CommandRunner()).GetAllTweaks();
        var expectedIds = new[]
        {
            "privacy.windowsSuggestions",
            "privacy.webSearch",
            "ui.showFileExtensions",
            "ui.endTaskTaskbar",
            "system.utcClock"
        };

        foreach (var id in expectedIds)
        {
            var tweak = Assert.Single(tweaks, candidate => candidate.Id == id);
            Assert.False(string.IsNullOrWhiteSpace(tweak.CheckScript));
            Assert.False(string.IsNullOrWhiteSpace(tweak.EnableScript));
            Assert.False(string.IsNullOrWhiteSpace(tweak.DisableScript));
        }

        Assert.All(
            tweaks.Where(tweak => expectedIds.Take(4).Contains(tweak.Id)),
            tweak =>
            {
                Assert.Equal(RiskLevel.Safe, tweak.RiskLevel);
                Assert.False(tweak.RequiresAdministrator);
            });

        var utcClock = Assert.Single(tweaks, tweak => tweak.Id == "system.utcClock");
        Assert.Equal(RiskLevel.Medium, utcClock.RiskLevel);
        Assert.True(utcClock.RequiresAdministrator);
    }

    [Fact]
    public async Task CheckAllTweakStatesAsync_ReturnsOneStatePerKnownTweak()
    {
        var service = new TweakService(new CommandRunner());

        var states = await service.CheckAllTweakStatesAsync();

        Assert.Equal(service.GetAllTweaks().Count, states.Count);
        Assert.Equal(states.Count, states.Select(state => state.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(service.GetAllTweaks(), tweak => Assert.Contains(states, state => state.Id == tweak.Id));
    }
}
