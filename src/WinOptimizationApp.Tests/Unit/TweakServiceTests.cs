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
}
