using System.Threading.Tasks;
using Xunit;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public class NetworkOptimizationServiceTests
{
    private readonly NetworkOptimizationService _service;

    public NetworkOptimizationServiceTests()
    {
        // For unit tests, we don't necessarily want to actually run flushdns if it disrupts
        // the host's network during CI, but GetAdaptersAsync is safe.
        var runner = new CommandRunner();
        _service = new NetworkOptimizationService(runner);
    }

    [Fact]
    public async Task GetAdaptersAsync_ReturnsAdaptersList()
    {
        // Act
        var adapters = await _service.GetAdaptersAsync();

        // Assert
        Assert.NotNull(adapters);
        // Any real system should have at least one network adapter, even if disconnected.
        // It might be empty on some isolated CI runners without virtual NICs, so we just
        // check that it doesn't throw and returns a list.
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<WinOptimizationApp.Models.NetworkAdapterInfo>>(adapters);
    }

    // Example testing of private formatting logic through reflection or exposed behavior 
    // is normally avoided in TDD. We test the public output.
    [Fact]
    public async Task GetAdaptersAsync_AdaptersHaveExpectedPropertiesPopulated()
    {
        var adapters = await _service.GetAdaptersAsync();

        foreach (var adapter in adapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
            Assert.NotNull(adapter.Status);
            Assert.NotNull(adapter.IpAddress);
            Assert.NotNull(adapter.Speed);
            // MAC addresses can be empty for some virtual adapters, but shouldn't be null
            Assert.NotNull(adapter.MacAddress);
        }
    }
}
