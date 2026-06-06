using WinOptimizationApp.Services;

namespace WinOptimizationApp;

public sealed partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteCrashLog(exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        WriteCrashLog(args.Exception);
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var root = FindRepositoryRoot(AppRuntimePaths.OriginalBaseDirectory);
            var logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            var path = Path.Combine(logs, $"app-crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            var builder = new StringBuilder();
            builder.AppendLine(DateTimeOffset.Now.ToString("O"));
            builder.AppendLine(exception.ToString());
            File.WriteAllText(path, builder.ToString());
        }
        catch
        {
            // Crash logging must never create another startup failure.
        }
    }

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "implementation_plan.md")) ||
                File.Exists(Path.Combine(directory.FullName, "src", "cli", "Utilities.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppRuntimePaths.OriginalBaseDirectory;
    }
}
