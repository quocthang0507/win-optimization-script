using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public class Winapp2ParserTests
{
    [Fact]
    public void Parse_ReturnsCorrectEntries_ForValidIniContent()
    {
        // Arrange
        var content = @"
[Adobe Flash Player]
LangSecRef=3021
Detect=HKCU\Software\Macromedia\FlashPlayer
Default=True
FileKey1=%AppData%\Macromedia\Flash Player|*.*|RECURSE
FileKey2=%AppData%\Adobe\Flash Player|*.*|RECURSE

[Mozilla Firefox - Cache]
LangSecRef=3024
DetectFile=%LocalAppData%\Mozilla\Firefox\Profiles
Default=True
FileKey1=%LocalAppData%\Mozilla\Firefox\Profiles|*.*|RECURSE
";
        var parser = new Winapp2Parser();

        // Act
        var entries = parser.Parse(content);

        // Assert
        Assert.Equal(2, entries.Count);

        var flash = entries[0];
        Assert.Equal("Adobe Flash Player", flash.Name);
        Assert.True(flash.Default);
        Assert.Single(flash.DetectKeys);
        Assert.Equal(2, flash.FileKeys.Count);
        Assert.True(flash.FileKeys[0].Recurse);

        var firefox = entries[1];
        Assert.Equal("Mozilla Firefox - Cache", firefox.Name);
        Assert.Single(firefox.DetectFiles);
        Assert.Single(firefox.FileKeys);
    }
}
