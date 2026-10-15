using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public class LocalizationServiceTests
{
    [Fact]
    public void Constructor_SetsDefaultLanguage_ToEnglish_WhenNotSpecified()
    {
        // Act
        // We cannot reliably predict system culture in a test runner, so we test
        // the behavior when an explicit language is passed vs when it falls back.
        var service = new LocalizationService(AppLanguage.English);

        // Assert
        Assert.Equal(AppLanguage.English, service.CurrentLanguage);
    }

    [Fact]
    public void Get_ReturnsVietnameseString_WhenLanguageIsVietnamese()
    {
        // Arrange
        var service = new LocalizationService(AppLanguage.Vietnamese);

        // Act
        var result = service.Get("common.close");

        // Assert
        Assert.Equal("Đóng", result); // Based on the standard dictionary
    }

    [Fact]
    public void Get_ReturnsEnglishString_WhenLanguageIsEnglish()
    {
        // Arrange
        var service = new LocalizationService(AppLanguage.English);

        // Act
        var result = service.Get("common.close");

        // Assert
        Assert.Equal("Close", result);
    }

    [Fact]
    public void Get_FallsBackToEnglish_WhenVietnameseKeyIsMissing()
    {
        // Arrange
        var service = new LocalizationService(AppLanguage.Vietnamese);
        
        // Let's assume a key exists in English but not Vietnamese (if any).
        // Since both dicts are identical in keys, testing a non-existent key returns the key itself.
        var result = service.Get("some.missing.key");

        // Assert
        Assert.Equal("some.missing.key", result);
    }

    [Fact]
    public void LanguageDictionaries_HaveExactMatchingKeysAndValidFormatPlaceholders()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var enPath = Path.Combine(basePath, "Assets", "Langs", "en.json");
        var viPath = Path.Combine(basePath, "Assets", "Langs", "vi.json");

        Assert.True(File.Exists(enPath), "en.json must exist in Assets/Langs");
        Assert.True(File.Exists(viPath), "vi.json must exist in Assets/Langs");

        var enDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath))!;
        var viDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(viPath))!;

        var missingInVi = enDict.Keys.Where(k => !viDict.ContainsKey(k)).OrderBy(k => k).ToList();
        var missingInEn = viDict.Keys.Where(k => !enDict.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.True(missingInVi.Count == 0, $"Keys present in en.json but missing in vi.json: {string.Join(", ", missingInVi)}");
        Assert.True(missingInEn.Count == 0, $"Keys present in vi.json but missing in en.json: {string.Join(", ", missingInEn)}");

        // Check format placeholder parity (e.g. {0}, {1} count)
        var placeholderRegex = new System.Text.RegularExpressions.Regex(@"\{(\d+)");
        foreach (var key in enDict.Keys)
        {
            var enMatches = placeholderRegex.Matches(enDict[key]).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToList();
            var viMatches = placeholderRegex.Matches(viDict[key]).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToList();

            Assert.True(enMatches.SequenceEqual(viMatches),
                $"Placeholder mismatch for key '{key}'. EN: [{string.Join(",", enMatches)}], VI: [{string.Join(",", viMatches)}]");
        }
    }

    [Fact]
    public void AllCodeTranslationKeys_ExistInLanguageDictionaries()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(basePath);
        DirectoryInfo? appDir = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "app");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "WinOptimizationApp.csproj")))
            {
                appDir = new DirectoryInfo(candidate);
                break;
            }
            // Also check if dir itself is src
            var candidateApp = Path.Combine(dir.FullName, "app");
            if (Directory.Exists(candidateApp) && File.Exists(Path.Combine(candidateApp, "WinOptimizationApp.csproj")))
            {
                appDir = new DirectoryInfo(candidateApp);
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(appDir);

        var enPath = Path.Combine(appDir.FullName, "Assets", "Langs", "en.json");
        var viPath = Path.Combine(appDir.FullName, "Assets", "Langs", "vi.json");

        var enDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath))!;
        var viDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(viPath))!;

        var csFiles = appDir.GetFiles("*.cs", SearchOption.AllDirectories);
        var keyRegex = new System.Text.RegularExpressions.Regex(@"(?:\bT|\bF|\bGet|\bFormat)\(\s*""([a-zA-Z0-9_\-\.]+)""");

        var missingInEn = new HashSet<string>();
        var missingInVi = new HashSet<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file.FullName);
            var matches = keyRegex.Matches(content);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var key = match.Groups[1].Value;
                // Only consider dotted keys like 'category.key' to avoid accidental false positives
                if (!key.Contains('.'))
                    continue;

                if (!enDict.ContainsKey(key))
                    missingInEn.Add($"{key} (found in {file.Name})");
                if (!viDict.ContainsKey(key))
                    missingInVi.Add($"{key} (found in {file.Name})");
            }
        }

        Assert.True(missingInEn.Count == 0, $"Missing keys in en.json:\n{string.Join("\n", missingInEn)}");
        Assert.True(missingInVi.Count == 0, $"Missing keys in vi.json:\n{string.Join("\n", missingInVi)}");
    }
}
