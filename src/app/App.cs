using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using WinOptimizationApp.Views;

namespace WinOptimizationApp;

public sealed partial class App : Application
{
    private Window? _window;
    private SplashScreenWindow? _splashWindow;

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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var initialSettings = new AppSettingsService().Load();
            var startupLocalization = new LocalizationService(initialSettings.Language);
            var startupTheme = ResolveStartupTheme(initialSettings.Theme);
            _splashWindow = new SplashScreenWindow(startupTheme);
            _splashWindow.Activate();
            await _splashWindow.WaitUntilReadyAsync();

            _splashWindow.SetStatus(startupLocalization.Get("splash.loadingSettings"));

            var mainWindow = new MainWindow(initialSettings);
            await mainWindow.CompleteStartupAsync(status => _splashWindow?.SetStatus(status));

            _window = mainWindow;
            _window.Activate();
            _splashWindow.Close();
            _splashWindow = null;
        }
        catch (Exception ex)
        {
            try
            {
                _splashWindow?.Close();
            }
            catch
            {
            }

            WriteCrashLog(ex);
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        WriteCrashLog(args.Exception);
    }

    private static ElementTheme ResolveStartupTheme(AppTheme? theme)
    {
        return theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light
        };
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var crashLogDirectory = AppRuntimePaths.OriginalBaseDirectory;
            Directory.CreateDirectory(crashLogDirectory);
            var path = Path.Combine(crashLogDirectory, $"app-crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

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
