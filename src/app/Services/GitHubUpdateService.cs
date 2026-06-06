using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class GitHubUpdateService
{
    public const string Owner = "quocthang0507";
    public const string Repository = "win-optimization-script";
    public static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");

    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (httpClient is null)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    public async Task<AppUpdateInfo?> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.UserAgent.ParseAdd("WinOptimizationApp");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);
        if (release is null ||
            string.IsNullOrWhiteSpace(release.TagName) ||
            string.IsNullOrWhiteSpace(release.HtmlUrl) ||
            !TryParseVersion(release.TagName, out var latestVersion))
        {
            return null;
        }

        var currentVersion = GetCurrentVersion();
        var asset = release.Assets?
            .Where(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ??
            release.Assets?.FirstOrDefault(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl));

        return new AppUpdateInfo(
            currentVersion,
            latestVersion,
            release.TagName,
            release.HtmlUrl,
            asset?.Name,
            asset?.BrowserDownloadUrl,
            latestVersion.CompareTo(currentVersion) > 0);
    }

    public static Version GetCurrentVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return TryParseVersion(informationalVersion, out var informational)
            ? informational
            : Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    public static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("assets")]
        public IReadOnlyList<GitHubReleaseAsset>? Assets { get; init; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;
    }
}
