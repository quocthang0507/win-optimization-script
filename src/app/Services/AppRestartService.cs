namespace WinOptimizationApp.Services;

internal static class AppRestartService
{
    private static int _restartAsAdminRequested;

    public static bool ScheduleRestartAsAdministrator()
    {
        return Interlocked.Exchange(ref _restartAsAdminRequested, 1) == 0;
    }

    public static void RestartIfScheduled()
    {
        if (Interlocked.Exchange(ref _restartAsAdminRequested, 0) == 1)
        {
            AppProcessLauncher.StartUi(elevated: true, standalone: true);
        }
    }
}
