using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class ContextMenuServiceTests
{
    [Fact]
    public void ClassicContextMenu_CanBeEnabledAndDisabledViaRegistry()
    {
        var registry = new InMemoryRegistryAccessor();
        var service = new ContextMenuService(registry);

        Assert.False(service.IsClassicContextMenuEnabled());

        service.SetClassicContextMenu(true);
        Assert.True(service.IsClassicContextMenuEnabled());

        service.SetClassicContextMenu(false);
        Assert.False(service.IsClassicContextMenuEnabled());
    }

    [Fact]
    public void ShellExtensions_CanBeDiscoveredAndReversiblyBlockedWithoutDeletingHandlerKeys()
    {
        var registry = new InMemoryRegistryAccessor();
        const string sampleClsid = "{23170F69-40C1-278A-1000-000100020000}";
        registry.WriteValue("HKLM", @"Software\Classes\*\shellex\ContextMenuHandlers\7-Zip", "", "String", sampleClsid);
        registry.WriteValue("HKLM", $@"Software\Classes\CLSID\{sampleClsid}\InprocServer32", "", "String", @"C:\Program Files\7-Zip\7-zip.dll");

        var service = new ContextMenuService(registry);

        var items = service.GetShellExtensions();
        var entry = Assert.Single(items);
        Assert.Equal("7-Zip", entry.Name);
        Assert.Equal(sampleClsid, entry.Clsid);
        Assert.True(entry.IsEnabled);

        // Disable via Shell Extensions\Blocked
        service.SetShellExtensionEnabled(sampleClsid, entry.Name, enabled: false);

        var afterDisable = Assert.Single(service.GetShellExtensions());
        Assert.False(afterDisable.IsEnabled);
        // Underlying handler key must still exist!
        Assert.True(registry.KeyExists("HKLM", @"Software\Classes\*\shellex\ContextMenuHandlers\7-Zip"));

        // Re-enable
        service.SetShellExtensionEnabled(sampleClsid, entry.Name, enabled: true);
        var afterEnable = Assert.Single(service.GetShellExtensions());
        Assert.True(afterEnable.IsEnabled);
    }
}
