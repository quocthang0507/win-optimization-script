using System.Threading.Tasks;
using Xunit;
using WinOptimizationApp.Services;
using System;

namespace WinOptimizationApp.Tests.Unit;

public class CommandRunnerTests
{
    [Fact]
    public async Task RunCaptureAsync_ReturnsStandardOutput_ForValidCommand()
    {
        // Arrange
        var runner = new CommandRunner();
        var isWindows = OperatingSystem.IsWindows();
        var cmd = isWindows ? "cmd.exe" : "echo";
        var args = isWindows ? "/c echo Hello World" : "Hello World";

        // Act
        var result = await runner.RunCaptureAsync(cmd, args);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello World", result.StandardOutput);
        Assert.Equal(1, runner.ExecutionCount);
    }

    [Fact]
    public async Task RunCaptureAsync_ReturnsError_ForInvalidCommand()
    {
        // Arrange
        var runner = new CommandRunner();

        // Act
        var result = await runner.RunCaptureAsync("thiscommanddoesnotexist123", "");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Error starting process", result.StandardError);
    }

    [Fact]
    public void Exists_ReturnsTrue_ForExistingCommand()
    {
        // Arrange
        var runner = new CommandRunner();
        var cmd = OperatingSystem.IsWindows() ? "cmd.exe" : "ls";

        // Act
        var exists = runner.Exists(cmd);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void Exists_ReturnsFalse_ForMissingCommand()
    {
        // Arrange
        var runner = new CommandRunner();

        // Act
        var exists = runner.Exists("thiscommanddoesnotexist123");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task RunCaptureAsync_ExecutesFullyQualifiedBatchPathContainingSpaces()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "Win Optimization App Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "sample script.cmd");
        await File.WriteAllTextAsync(script, "@echo Batch Path Works");

        try
        {
            var result = await new CommandRunner().RunCaptureAsync(script, string.Empty);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Batch Path Works", result.StandardOutput);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
