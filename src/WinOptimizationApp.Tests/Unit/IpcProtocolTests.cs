using System.Diagnostics;
using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class IpcProtocolTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "WinOptimizationApp.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private IpcServer CreateTestServer(string pipeName)
    {
        var commands = new CommandRunner();
        var paths = new PathService(_tempRoot);
        var reports = new ReportService(paths);
        var cleanup = new CleanupService(commands);
        var restore = new RestorePointService(commands);
        var execution = new MaintenanceExecutionService(cleanup, commands, paths, reports, restore);
        var status = new SystemStatusService(commands, reports);
        var settings = new AppSettingsService(Path.Combine(_tempRoot, "settings.json"));
        var startup = new StartupService();
        var winget = new WingetService(commands);
        var registryCleaner = new RegistryCleanerService(commands);
        var network = new NetworkOptimizationService(commands);
        var uninstaller = new UninstallerService(commands);

        var server = new IpcServer(
            cleanup,
            execution,
            status,
            settings,
            reports,
            startup,
            winget,
            registryCleaner,
            network,
            uninstaller,
            pipeName);
        server.SetExpectedClientProcessId(Environment.ProcessId);
        return server;
    }

    [Fact]
    public async Task CancelRequest_CancelsInFlightOperationWithoutClosingPipeAndAllowsSubsequentRequests()
    {
        var pipeName = AppProcessLauncher.CreateRunnerPipeName();
        var server = CreateTestServer(pipeName);
        var serverCancelledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.CustomRequestHandler = async (type, payload, token) =>
        {
            if (type == "SlowOperation")
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    return "Completed";
                }
                catch (OperationCanceledException)
                {
                    serverCancelledTcs.TrySetResult(true);
                    throw;
                }
            }

            if (type == "Echo")
            {
                return $"Echo:{payload}";
            }

            return null;
        };

        server.Start();
        Assert.True(server.WaitUntilReady(TimeSpan.FromSeconds(3)));

        var client = new IpcClient();
        try
        {
            Assert.True(await client.ConnectAsync(pipeName, 2000).WaitAsync(TimeSpan.FromSeconds(3)));

            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await client.SendRequestAsync("SlowOperation", "1", cts.Token).WaitAsync(TimeSpan.FromSeconds(2)));
            }

            var cancelledOnServer = await serverCancelledTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(cancelledOnServer);
            Assert.True(client.IsConnected);

            // Subsequent request on the exact same pipe must still succeed and match RequestId!
            var followUp = await client.SendRequestAsync("Echo", "AfterCancel").WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("Echo:AfterCancel", followUp);
        }
        finally
        {
            client.Disconnect();
            server.Stop();
        }
    }

    [Fact]
    public async Task ConcurrentRequests_AreCorrelatedByRequestIdCorrectly()
    {
        var pipeName = AppProcessLauncher.CreateRunnerPipeName();
        var server = CreateTestServer(pipeName);

        server.CustomRequestHandler = async (type, payload, token) =>
        {
            if (type == "DelayedEcho")
            {
                var parts = (payload ?? "").Split(':');
                var delayMs = int.Parse(parts[0]);
                await Task.Delay(delayMs, token);
                return parts[1];
            }

            return null;
        };

        server.Start();
        Assert.True(server.WaitUntilReady(TimeSpan.FromSeconds(3)));

        var client = new IpcClient();
        try
        {
            Assert.True(await client.ConnectAsync(pipeName, 2000).WaitAsync(TimeSpan.FromSeconds(3)));

            var taskSlow = client.SendRequestAsync("DelayedEcho", "180:FirstRequest").WaitAsync(TimeSpan.FromSeconds(2));
            var taskFast = client.SendRequestAsync("DelayedEcho", "20:SecondRequest").WaitAsync(TimeSpan.FromSeconds(2));

            await Task.WhenAll(taskSlow, taskFast);

            Assert.Equal("FirstRequest", await taskSlow);
            Assert.Equal("SecondRequest", await taskFast);
        }
        finally
        {
            client.Disconnect();
            server.Stop();
        }
    }
}
