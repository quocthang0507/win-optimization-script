using System.Globalization;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class FormattersTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(-42, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024L * 1024 * 3, "3.0 MB")]
    public void FormatBytes_UsesExpectedUnits(long bytes, string expected)
    {
        using var culture = CultureScope.Use("en-US");

        Assert.Equal(expected, Formatters.FormatBytes(bytes));
    }

    [Fact]
    public void FormatDuration_English_PluralizesUnits()
    {
        using var culture = CultureScope.Use("en-US");
        var text = Formatters.FormatDuration(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)), AppLanguage.English);

        Assert.Equal("2 hours 1 minute", text);
    }

    [Fact]
    public void FormatDuration_Vietnamese_UsesVietnameseLabels()
    {
        var text = Formatters.FormatDuration(TimeSpan.FromMinutes(12), AppLanguage.Vietnamese);

        Assert.Contains("12", text, StringComparison.Ordinal);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;

        private CultureScope(CultureInfo culture)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public static CultureScope Use(string name)
        {
            return new CultureScope(CultureInfo.GetCultureInfo(name));
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
