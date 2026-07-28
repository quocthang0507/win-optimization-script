using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ProtectedPathServiceTests
{
    [Fact]
    public void NormalizePaths_RemovesInvalidAndDuplicateEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "protected-root");

        var result = ProtectedPathService.NormalizePaths([root, root + Path.DirectorySeparatorChar, "\0invalid"]);

        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), result[0]);
    }

    [Fact]
    public void IntersectsProtectedTree_BlocksProtectedChildAndItsParent()
    {
        var parent = Path.Combine(Path.GetTempPath(), "workspace");
        var protectedChild = Path.Combine(parent, "important");

        Assert.True(ProtectedPathService.IntersectsProtectedTree(protectedChild, [protectedChild]));
        Assert.True(ProtectedPathService.IntersectsProtectedTree(parent, [protectedChild]));
        Assert.True(ProtectedPathService.IntersectsProtectedTree(Path.Combine(protectedChild, "cache"), [protectedChild]));
        Assert.False(ProtectedPathService.IntersectsProtectedTree(Path.Combine(Path.GetTempPath(), "other"), [protectedChild]));
    }
}
