using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Services;

public class TweakServiceTests
{
    private readonly CommandRunner _runner;
    private readonly TweakService _service;

    public TweakServiceTests()
    {
        _runner = new CommandRunner();
        _service = new TweakService(_runner);
    }

    [Fact]
    public void GetAllTweaks_ReturnsExpectedTweaks()
    {
        var tweaks = _service.GetAllTweaks();

        Assert.NotNull(tweaks);
        Assert.NotEmpty(tweaks);
        Assert.Contains(tweaks, t => t.Id == "privacy.telemetry");
        Assert.Contains(tweaks, t => t.Category == "Privacy");
    }

    [Fact]
    public async Task CheckTweakStateAsync_InvalidId_ReturnsError()
    {
        var response = await _service.CheckTweakStateAsync("invalid.id");

        Assert.NotNull(response);
        Assert.Equal("invalid.id", response.Id);
        Assert.Equal("Tweak not found.", response.Error);
    }

    [Fact]
    public async Task ApplyTweakAsync_InvalidId_ReturnsError()
    {
        var response = await _service.ApplyTweakAsync("invalid.id", true);

        Assert.NotNull(response);
        Assert.Equal("invalid.id", response.Id);
        Assert.Equal("Tweak not found.", response.Error);
    }
}
