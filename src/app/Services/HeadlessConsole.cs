namespace WinOptimizationApp.Services;

/// <summary>
/// The app is a GUI-subsystem executable, so it has no console of its own. Headless runs write to a
/// redirected stdout when one is provided, otherwise to the console of the launching terminal.
/// </summary>
internal static class HeadlessConsole
{
    private const int AttachParentProcess = -1;
    private const int StandardOutputHandle = -11;

    public static TextWriter Open()
    {
        var handle = GetStdHandle(StandardOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            AttachConsole(AttachParentProcess);
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // No console is attached (for example under Task Scheduler); reports are still written to logs.
        }

        return new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
}
