using WinOptimizationApp.Models;
using WinOptimizationApp.Services;
using Xunit;

namespace WinOptimizationApp.Tests.Unit;

public sealed class OperationServiceTests
{
    [Fact]
    public async Task RunAsync_CompletesSuccessfully_AndRecordsHistory()
    {
        using var service = new OperationService();
        OperationHandle? startedHandle = null;
        OperationHandle? completedHandle = null;

        service.OperationStarted += h => startedHandle = h;
        service.OperationCompleted += h => completedHandle = h;

        var result = await service.RunAsync(
            "Test Operation",
            OperationCategory.Maintenance,
            async (reporter, token) =>
            {
                await Task.Delay(10, token);
                reporter.Report(50.0, "Working...");
                return 42;
            });

        Assert.Equal(42, result);
        Assert.NotNull(startedHandle);
        Assert.NotNull(completedHandle);
        Assert.Equal("Test Operation", completedHandle.Title);
        Assert.Equal(OperationState.Succeeded, completedHandle.State);
        Assert.False(service.HasActiveOperations);
        Assert.Single(service.RecentOperations);
        Assert.Equal(completedHandle.Id, service.RecentOperations[0].Id);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressAccurately()
    {
        using var service = new OperationService();
        var progressUpdates = new List<double>();
        var stepMessages = new List<string>();

        service.OperationProgressChanged += handle =>
        {
            if (handle.ProgressPercent.HasValue)
            {
                progressUpdates.Add(handle.ProgressPercent.Value);
            }
            if (handle.CurrentStep != null)
            {
                stepMessages.Add(handle.CurrentStep);
            }
        };

        await service.RunAsync(
            "Progress Test",
            OperationCategory.Cleanup,
            async (reporter, token) =>
            {
                reporter.Report(25.0, "Step 1");
                await Task.Delay(5, token);
                reporter.Report(75.0, "Step 2");
                await Task.Delay(5, token);
                return true;
            });

        Assert.Contains(25.0, progressUpdates);
        Assert.Contains(75.0, progressUpdates);
        Assert.Contains("Step 1", stepMessages);
        Assert.Contains("Step 2", stepMessages);
    }

    [Fact]
    public async Task RunAsync_HandlesCancellation_AndMarksHandleCanceled()
    {
        using var service = new OperationService();
        var tcs = new TaskCompletionSource();

        var runTask = service.RunAsync(
            "Cancellable Op",
            OperationCategory.Updates,
            async (reporter, token) =>
            {
                tcs.SetResult();
                await Task.Delay(5000, token);
                return "never";
            },
            canCancel: true);

        await tcs.Task;
        var active = Assert.Single(service.ActiveOperations);
        Assert.True(service.CancelOperation(active.Id));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.False(service.HasActiveOperations);
        Assert.Single(service.RecentOperations);
        Assert.Equal(OperationState.Canceled, service.RecentOperations[0].State);
    }

    [Fact]
    public async Task RunAsync_CapturesFailure_AndErrorMessage()
    {
        using var service = new OperationService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync<string>(
                "Failing Op",
                OperationCategory.Registry,
                (reporter, token) => throw new InvalidOperationException("Simulated error")));

        Assert.Equal("Simulated error", ex.Message);
        Assert.False(service.HasActiveOperations);
        Assert.Single(service.RecentOperations);
        Assert.Equal(OperationState.Failed, service.RecentOperations[0].State);
        Assert.Equal("Simulated error", service.RecentOperations[0].ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_ExclusiveLocking_PreventsSimultaneousExecution()
    {
        using var service = new OperationService();
        var concurrentRunningCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        async Task DoWorkAsync(OperationProgressReporter reporter, CancellationToken token)
        {
            lock (lockObj)
            {
                concurrentRunningCount++;
                if (concurrentRunningCount > maxConcurrent)
                {
                    maxConcurrent = concurrentRunningCount;
                }
            }

            await Task.Delay(20, token);

            lock (lockObj)
            {
                concurrentRunningCount--;
            }
        }

        var task1 = service.RunAsync("Op 1", OperationCategory.SystemTool, DoWorkAsync, isExclusive: true);
        var task2 = service.RunAsync("Op 2", OperationCategory.SystemTool, DoWorkAsync, isExclusive: true);

        await Task.WhenAll(task1, task2);

        Assert.Equal(1, maxConcurrent);
        Assert.Equal(2, service.RecentOperations.Count);
    }

    [Fact]
    public async Task CancelAllActive_CancelsAllCancellableOperations()
    {
        using var service = new OperationService();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        var task1 = service.RunAsync("Op 1", OperationCategory.Maintenance, async (_, token) =>
        {
            tcs1.SetResult();
            await Task.Delay(5000, token);
        }, canCancel: true);

        var task2 = service.RunAsync("Op 2", OperationCategory.Maintenance, async (_, token) =>
        {
            tcs2.SetResult();
            await Task.Delay(5000, token);
        }, canCancel: true);

        await Task.WhenAll(tcs1.Task, tcs2.Task);
        Assert.Equal(2, service.ActiveCount);

        service.CancelAllActive();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(task1, task2));
        Assert.Equal(0, service.ActiveCount);
    }

    [Fact]
    public async Task GetOperation_FindsActiveAndHistoricalOperations()
    {
        using var service = new OperationService();
        var tcs = new TaskCompletionSource();

        var task = service.RunAsync("Findable Op", OperationCategory.General, async (_, token) =>
        {
            tcs.SetResult();
            await Task.Delay(20, token);
            return "done";
        });

        await tcs.Task;
        var active = Assert.Single(service.ActiveOperations);
        Assert.NotNull(service.GetOperation(active.Id));

        await task;
        Assert.NotNull(service.GetOperation(active.Id));
        Assert.Null(service.GetOperation("non_existent_id"));
    }
}
