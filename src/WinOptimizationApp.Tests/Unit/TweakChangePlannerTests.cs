using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class TweakChangePlannerTests
{
    private static readonly SystemTweak[] Catalog =
    [
        new() { Id = "ui.extensions", Title = "Extensions" },
        new() { Id = "ui.taskbar", Title = "Taskbar" }
    ];

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"ui.extensions\":1}")]
    [InlineData("{\"ui.extensions\":\"true\"}")]
    [InlineData("{\"ui.extensions\":true,\"UI.EXTENSIONS\":false}")]
    [InlineData("{\"ui.extensions\":true,\"ui.extensions\":false}")]
    [InlineData("{\" \":true}")]
    public void ParseProfile_RejectsAmbiguousOrInvalidValues(string json)
    {
        Assert.Throws<InvalidDataException>(() => TweakChangePlanner.ParseProfile(json));
    }

    [Fact]
    public void Create_CanonicalizesIdsSkipsUnchangedAndCountsUnknowns()
    {
        var requested = TweakChangePlanner.ParseProfile("{\"UI.EXTENSIONS\":true,\"ui.taskbar\":false,\"future.setting\":true}");
        var plan = TweakChangePlanner.Create(requested, Catalog,
            new Dictionary<string, bool> { ["ui.extensions"] = false, ["ui.taskbar"] = false });
        var change = Assert.Single(plan.Changes);
        Assert.Equal("ui.extensions", change.Id);
        Assert.False(change.Before);
        Assert.True(change.After);
        Assert.Equal(1, plan.UnchangedCount);
        Assert.Equal(1, plan.UnknownCount);
    }

    [Fact]
    public void Create_RejectsUnreadableCurrentStateBeforePlanningAnyChanges()
    {
        var requested = new Dictionary<string, bool> { ["ui.extensions"] = true, ["ui.taskbar"] = true };
        Assert.Throws<InvalidDataException>(() => TweakChangePlanner.Create(requested, Catalog,
            new Dictionary<string, bool> { ["ui.extensions"] = false }));
    }
}
