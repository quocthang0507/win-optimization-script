using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class PathExpanderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Exists_RecognizesWildcardSegments()
    {
        var detected = Path.Combine(_root, "Chrome Stable", "Cache");
        Directory.CreateDirectory(detected);

        Assert.True(PathExpander.Exists(Path.Combine(_root, "Chrome*", "Cache")));
        Assert.False(PathExpander.Exists(Path.Combine(_root, "Firefox*", "Cache")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
