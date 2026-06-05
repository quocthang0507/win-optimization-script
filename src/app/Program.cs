using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinOptimizationApp;

public static class Program
{
    private static App? _app;

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _app = new App();
        });
    }
}
