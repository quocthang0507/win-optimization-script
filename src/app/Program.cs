using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using WinOptimizationApp.Services;

namespace WinOptimizationApp;

public static class Program
{
    private static App? _app;

    [STAThread]
    public static void Main(string[] args)
    {
        var runUi = false;
        foreach (var arg in args)
        {
            if (arg.Equals("--ui", StringComparison.OrdinalIgnoreCase))
            {
                runUi = true;
                break;
            }
        }

        if (runUi)
        {
            // Run UI Mode
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackParams =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
            });
        }
        else
        {
            // Run Runner Mode
            var paths = new PathService();
            var settingsService = new AppSettingsService();
            var commands = new CommandRunner();
            var reports = new ReportService(paths);
            var cleanup = new CleanupService(commands);
            var status = new SystemStatusService(commands, reports);
            var winget = new WingetService(commands);
            var startup = new StartupService();
            var execution = new MaintenanceExecutionService(cleanup, commands, paths, reports, new RestorePointService(commands));

            var server = new IpcServer(cleanup, execution, status, settingsService, reports, startup, winget);
            server.Start();

            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = typeof(Program).Assembly.Location;
                    if (Path.GetExtension(exePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        exePath = Path.ChangeExtension(exePath, ".exe");
                    }
                }

                if (File.Exists(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--ui",
                        UseShellExecute = false
                    };

                    using var uiProcess = Process.Start(startInfo);
                    if (uiProcess != null)
                    {
                        uiProcess.WaitForExit();
                    }
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
}
