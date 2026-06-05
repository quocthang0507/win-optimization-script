using Microsoft.UI.Xaml;

namespace WinOptimizationApp;

public sealed partial class App : Application
{
    private Window? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
