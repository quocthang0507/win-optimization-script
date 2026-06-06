using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class PrivacyCleanerCatalogTests
{
    [Fact]
    public void BuildDefault_SelectsOnlySafeNonSensitiveItemsByDefault()
    {
        var items = PrivacyCleanerCatalog.BuildDefault();

        Assert.Contains(items, item => item.Id == "privacy.clipboard" && item.IsSelectedByDefault);
        Assert.All(items.Where(item => item.IsSelectedByDefault), item =>
        {
            Assert.False(item.IsSensitive);
            Assert.NotEqual(RiskLevel.High, item.RiskLevel);
        });
    }

    [Fact]
    public void BuildDefault_KeepsBrowserHistoryCookiesAndPasswordsOptIn()
    {
        var items = PrivacyCleanerCatalog.BuildDefault();

        Assert.Contains(items, item => item.Id == "privacy.browserHistory" && item.IsSensitive && !item.IsSelectedByDefault);
        Assert.Contains(items, item => item.Id == "privacy.browserCookies" && item.IsSensitive && !item.IsSelectedByDefault);
        Assert.Contains(items, item => item.Id == "privacy.browserPasswords" && item.IsSensitive && !item.IsSelectedByDefault && !item.CanCleanAutomatically);
    }

    [Fact]
    public void BuildDefault_AllSensitiveItemsHaveExplicitRecommendation()
    {
        var items = PrivacyCleanerCatalog.BuildDefault();

        Assert.All(items.Where(item => item.IsSensitive), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Recommendation));
        });
    }
}
