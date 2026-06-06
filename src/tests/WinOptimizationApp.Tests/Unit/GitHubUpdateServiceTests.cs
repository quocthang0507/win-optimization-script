using System.Net;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3+release", "1.2.3")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    public void TryParseVersion_NormalizesReleaseTags(string input, string expected)
    {
        var parsed = GitHubUpdateService.TryParseVersion(input, out var version);

        Assert.True(parsed);
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsUpdateWhenLatestReleaseIsNewer()
    {
        var json = """
        {
          "tag_name": "v99.0.0",
          "html_url": "https://github.com/quocthang0507/win-optimization-script/releases/tag/v99.0.0",
          "assets": [
            {
              "name": "WinOptimizationApp-v99.0.0-win-x64.zip",
              "browser_download_url": "https://github.com/download/app.zip"
            }
          ]
        }
        """;
        var service = new GitHubUpdateService(new HttpClient(new StaticResponseHandler(HttpStatusCode.OK, json)));

        var update = await service.CheckLatestReleaseAsync();

        Assert.NotNull(update);
        Assert.True(update.IsUpdateAvailable);
        Assert.Equal("v99.0.0", update.TagName);
        Assert.Equal("WinOptimizationApp-v99.0.0-win-x64.zip", update.AssetName);
        Assert.Equal("https://github.com/download/app.zip", update.AssetUrl);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsNoUpdateWhenLatestReleaseMatchesCurrentVersion()
    {
        var current = GitHubUpdateService.GetCurrentVersion();
        var json = $$"""
        {
          "tag_name": "v{{current}}",
          "html_url": "https://github.com/quocthang0507/win-optimization-script/releases/tag/v{{current}}",
          "assets": []
        }
        """;
        var service = new GitHubUpdateService(new HttpClient(new StaticResponseHandler(HttpStatusCode.OK, json)));

        var update = await service.CheckLatestReleaseAsync();

        Assert.NotNull(update);
        Assert.False(update.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsNullWhenGitHubRequestFails()
    {
        var service = new GitHubUpdateService(new HttpClient(new StaticResponseHandler(HttpStatusCode.NotFound, "{}")));

        var update = await service.CheckLatestReleaseAsync();

        Assert.Null(update);
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            };

            return Task.FromResult(response);
        }
    }
}
