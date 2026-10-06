using WinOptimizationApp.Services;

namespace WinOptimizationApp;

public static class Program
{
    private static App? _app;

    [STAThread]
    public static void Main(string[] args)
    {
        ProcessEfficiencyService.EnableForCurrentProcess();

        var headless = HeadlessCommandLine.TryParse(args);
        if (headless is not null)
        {
            Environment.ExitCode = RunHeadless(headless);
            return;
        }

        var runRunner = args.Any(arg => arg.Equals(AppProcessLauncher.RunnerArgument, StringComparison.OrdinalIgnoreCase));

        if (runRunner)
        {
            RunRunner();
        }
        else
        {
            RunUi();
        }
    }

    private static int RunHeadless(HeadlessOptions options)
    {
        var output = HeadlessConsole.Open();
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var runner = new HeadlessMaintenanceRunner(output, new AppSettingsService().Load());
            return runner.RunAsync(options, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return HeadlessMaintenanceRunner.ExitTaskFailed;
        }
        finally
        {
            output.Flush();
        }
    }

    private static void RunUi()
    {
        var singleInstance = SingleInstanceGuard.TryAcquireForCurrentProcess();
        if (singleInstance is null)
        {
            return;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackParams =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
            });
        }
        finally
        {
            singleInstance.Dispose();
        }

        AppRestartService.RestartIfScheduled();
    }

    private static void RunRunner()
    {
        var paths = new PathService();
        var settingsService = new AppSettingsService();
        var commands = new CommandRunner();
        var reports = new ReportService(paths);
        var cleanup = new CleanupService(commands);
        var status = new SystemStatusService(commands, reports);
        var winget = new WingetService(commands);
        var startup = new StartupService();
        var execution = new MaintenanceExecutionService(cleanup, commands, paths, reports, new RestorePointService(commands));
        var registryCleaner = new RegistryCleanerService(commands);
        var networkOptimizer = new NetworkOptimizationService(commands);
        var uninstaller = new UninstallerService(commands);

        var pipeName = AppProcessLauncher.CreateRunnerPipeName();
        var server = new IpcServer(
            cleanup,
            execution,
            status,
            settingsService,
            reports,
            startup,
            winget,
            registryCleaner,
            networkOptimizer,
            uninstaller,
            pipeName);
        server.Start();
        if (!server.WaitUntilReady(TimeSpan.FromSeconds(3)))
        {
            server.Stop();
            Console.WriteLine("Runner error: unable to initialize the secure IPC channel.");
            return;
        }

        try
        {
            using var uiProcess = AppProcessLauncher.StartUi(
                elevated: false,
                connectRunner: true,
                runnerPipeName: pipeName);
            if (uiProcess != null)
            {
                server.SetExpectedClientProcessId(uiProcess.Id);
                uiProcess.WaitForExit();
            }
            else
            {
                Console.WriteLine("Runner error: unable to resolve application executable.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Runner error: {ex.Message}");
        }
        finally
        {
            server.Stop();
        }
    }
}
