namespace WinOptimizationApp.Models;

public enum OperationCategory
{
    General,
    Cleanup,
    Maintenance,
    Updates,
    Software,
    Registry,
    Network,
    SystemTool,
    Storage
}

public enum OperationState
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public sealed record OperationProgressUpdate(
    double? Percent = null,
    string? Message = null,
    string? CurrentItem = null,
    long? ProcessedBytes = null);

public sealed class OperationHandle
{
    private readonly CancellationTokenSource? _internalCts;
    private readonly object _stateLock = new();

    internal OperationHandle(
        string title,
        OperationCategory category,
        bool canCancel,
        bool isExclusive,
        CancellationToken callerToken)
    {
        Id = Guid.NewGuid().ToString("N");
        Title = title;
        Category = category;
        CanCancel = canCancel;
        IsExclusive = isExclusive;
        CreatedAt = DateTimeOffset.Now;
        State = OperationState.Pending;

        if (canCancel)
        {
            _internalCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            CancellationToken = _internalCts.Token;
        }
        else
        {
            CancellationToken = callerToken;
        }
    }

    public string Id { get; }
    public string Title { get; }
    public OperationCategory Category { get; }
    public bool CanCancel { get; }
    public bool IsExclusive { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public OperationState State { get; private set; }
    public double? ProgressPercent { get; private set; }
    public string? CurrentStep { get; private set; }
    public string? ErrorMessage { get; private set; }
    public object? Result { get; internal set; }
    public CancellationToken CancellationToken { get; }

    public bool IsActive => State is OperationState.Pending or OperationState.Running;

    public TimeSpan Elapsed
    {
        get
        {
            var start = StartedAt ?? CreatedAt;
            var end = CompletedAt ?? DateTimeOffset.Now;
            return end - start;
        }
    }

    public void Cancel()
    {
        if (!CanCancel || _internalCts == null)
        {
            return;
        }

        try
        {
            _internalCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed.
        }
    }

    internal void MarkStarted()
    {
        lock (_stateLock)
        {
            State = OperationState.Running;
            StartedAt = DateTimeOffset.Now;
        }
    }

    internal void UpdateProgress(OperationProgressUpdate update)
    {
        lock (_stateLock)
        {
            if (update.Percent.HasValue)
            {
                ProgressPercent = Math.Clamp(update.Percent.Value, 0.0, 100.0);
            }

            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                CurrentStep = update.Message;
            }
        }
    }

    internal void MarkCompleted(OperationState finalState, string? error = null, object? result = null)
    {
        lock (_stateLock)
        {
            State = finalState;
            CompletedAt = DateTimeOffset.Now;
            ErrorMessage = error;
            Result = result;
        }
    }

    internal void Dispose()
    {
        _internalCts?.Dispose();
    }
}

public sealed class OperationProgressReporter : IProgress<OperationProgressUpdate>
{
    private readonly Action<OperationProgressUpdate> _onReport;

    public OperationProgressReporter(Action<OperationProgressUpdate> onReport)
    {
        _onReport = onReport ?? throw new ArgumentNullException(nameof(onReport));
    }

    public void Report(OperationProgressUpdate value)
    {
        _onReport(value);
    }

    public void Report(string message)
    {
        _onReport(new OperationProgressUpdate(Message: message));
    }

    public void Report(double percent, string? message = null)
    {
        _onReport(new OperationProgressUpdate(Percent: percent, Message: message));
    }

    public void ReportBytes(long processedBytes, double? percent = null, string? message = null)
    {
        _onReport(new OperationProgressUpdate(
            Percent: percent,
            Message: message,
            ProcessedBytes: processedBytes));
    }
}
