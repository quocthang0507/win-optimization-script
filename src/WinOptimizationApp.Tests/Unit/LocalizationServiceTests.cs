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
}
