using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class OperationService : IDisposable
{
    private const int MaxHistoryCount = 50;
    private readonly object _syncLock = new();
    private readonly List<OperationHandle> _activeOperations = [];
    private readonly List<OperationHandle> _history = [];
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private bool _isDisposed;

    public event Action<OperationHandle>? OperationStarted;
    public event Action<OperationHandle>? OperationProgressChanged;
    public event Action<OperationHandle>? OperationCompleted;
    public event Action? ActiveOperationsChanged;

    public IReadOnlyList<OperationHandle> ActiveOperations
    {
        get
        {
            lock (_syncLock)
            {
                return _activeOperations.ToArray();
            }
        }
    }

    public IReadOnlyList<OperationHandle> RecentOperations
    {
        get
        {
            lock (_syncLock)
            {
                return _history.ToArray();
            }
        }
    }

    public bool HasActiveOperations
    {
        get
        {
            lock (_syncLock)
            {
                return _activeOperations.Count > 0;
            }
        }
    }

    public int ActiveCount
    {
        get
        {
            lock (_syncLock)
            {
                return _activeOperations.Count;
            }
        }
    }

    public OperationHandle? GetOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        lock (_syncLock)
        {
            return _activeOperations.FirstOrDefault(o => o.Id == operationId)
                   ?? _history.FirstOrDefault(o => o.Id == operationId);
        }
    }

    public async Task<TResult> RunAsync<TResult>(
        string title,
        OperationCategory category,
        Func<OperationProgressReporter, CancellationToken, Task<TResult>> work,
        bool canCancel = true,
        bool isExclusive = false,
        CancellationToken callerToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var handle = new OperationHandle(title, category, canCancel, isExclusive, callerToken);

        lock (_syncLock)
        {
            _activeOperations.Add(handle);
        }

        NotifyActiveChanged();

        var reporter = new OperationProgressReporter(update =>
        {
            handle.UpdateProgress(update);
            try
            {
                OperationProgressChanged?.Invoke(handle);
            }
            catch
            {
                // Progress listeners should not throw into running operations.
            }
        });

        var acquiredExclusive = false;
        try
        {
            if (isExclusive)
            {
                await _exclusiveLock.WaitAsync(handle.CancellationToken);
                acquiredExclusive = true;
            }

            handle.MarkStarted();
            try
            {
                OperationStarted?.Invoke(handle);
            }
            catch
            {
                // Event handlers should not interrupt execution.
            }

            var result = await work(reporter, handle.CancellationToken);
            handle.MarkCompleted(OperationState.Succeeded, result: result);
            return result;
        }
        catch (OperationCanceledException) when (handle.CancellationToken.IsCancellationRequested)
        {
            handle.MarkCompleted(OperationState.Canceled);
            throw;
        }
        catch (Exception ex)
        {
            handle.MarkCompleted(OperationState.Failed, error: ex.Message);
            throw;
        }
        finally
        {
            if (acquiredExclusive)
            {
                _exclusiveLock.Release();
            }

            lock (_syncLock)
            {
                _activeOperations.Remove(handle);
                _history.Insert(0, handle);
                if (_history.Count > MaxHistoryCount)
                {
                    var excess = _history.Count - MaxHistoryCount;
                    for (var i = 0; i < excess; i++)
                    {
                        var oldest = _history[^1];
                        _history.RemoveAt(_history.Count - 1);
                        oldest.Dispose();
                    }
                }
            }

            NotifyActiveChanged();

            try
            {
                OperationCompleted?.Invoke(handle);
            }
            catch
            {
                // Event listeners should not throw in finally.
            }
        }
    }

    public Task RunAsync(
        string title,
        OperationCategory category,
        Func<OperationProgressReporter, CancellationToken, Task> work,
        bool canCancel = true,
        bool isExclusive = false,
        CancellationToken callerToken = default)
    {
        return RunAsync<bool>(
            title,
            category,
            async (reporter, token) =>
            {
                await work(reporter, token);
                return true;
            },
            canCancel,
            isExclusive,
            callerToken);
    }

    public bool CancelOperation(string operationId)
    {
        OperationHandle? target;
        lock (_syncLock)
        {
            target = _activeOperations.FirstOrDefault(o => o.Id == operationId);
        }

        if (target is { CanCancel: true })
        {
            target.Cancel();
            return true;
        }

        return false;
    }

    public void CancelAllActive()
    {
        List<OperationHandle> snapshot;
        lock (_syncLock)
        {
            snapshot = _activeOperations.Where(o => o.CanCancel).ToList();
        }

        foreach (var op in snapshot)
        {
            op.Cancel();
        }
    }

    private void NotifyActiveChanged()
    {
        try
        {
            ActiveOperationsChanged?.Invoke();
        }
        catch
        {
            // Event listeners should not crash operation dispatch.
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelAllActive();

        lock (_syncLock)
        {
            foreach (var op in _activeOperations)
            {
                op.Dispose();
            }
            _activeOperations.Clear();

            foreach (var op in _history)
            {
                op.Dispose();
            }
            _history.Clear();
        }

        _exclusiveLock.Dispose();
    }
}
