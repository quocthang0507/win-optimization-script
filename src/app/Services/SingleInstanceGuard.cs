using System.Security.Cryptography;
using System.Text;

namespace WinOptimizationApp.Services;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquireForCurrentProcess()
    {
        var mutexName = BuildMutexName();

        try
        {
            var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstanceGuard(mutex);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex may already be abandoned during shutdown.
        }

        _mutex.Dispose();
    }

    private static string BuildMutexName()
    {
        var baseDirectoryHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(AppRuntimePaths.OriginalBaseDirectory.ToUpperInvariant())))
            .Substring(0, 16);

        return $@"Local\WinOptimizationApp_{baseDirectoryHash}";
    }
}
