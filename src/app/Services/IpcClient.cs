using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace WinOptimizationApp.Services;

public sealed class IpcClient
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private Task? _readTask;
    private readonly CancellationTokenSource _cts = new();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event Action<string>? OnProgressReceived;
    public event Action? OnDisconnected;

    public bool IsConnected => _pipeClient?.IsConnected == true;

    public async Task<bool> ConnectAsync(string pipeName, int timeoutMs = 2000)
    {
        if (!AppProcessLauncher.IsValidRunnerPipeName(pipeName))
        {
            return false;
        }

        try
        {
            _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipeClient.ConnectAsync(timeoutMs, _cts.Token);
            _writer = new StreamWriter(_pipeClient, Utf8NoBom, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

            _readTask = Task.Run(ReadLoopAsync);
            return true;
        }
        catch
        {
            _pipeClient?.Dispose();
            _pipeClient = null;
            return false;
        }
    }

    public async Task<string> SendRequestAsync(
        string type,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected || _writer == null)
        {
            throw new InvalidOperationException("IPC Client is not connected to Runner.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            var message = new IpcMessage(type, payload, requestId);
            await WriteMessageAsync(message, cancellationToken);

            try
            {
                return await tcs.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _pendingRequests.TryRemove(requestId, out _);
                await TrySendCancelAsync(requestId);
                throw;
            }
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task WriteMessageAsync(IpcMessage message, CancellationToken cancellationToken)
    {
        var writer = _writer ?? throw new InvalidOperationException("IPC Client writer is not initialized.");
        var json = JsonSerializer.Serialize(message);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task TrySendCancelAsync(string requestId)
    {
        if (_pipeClient?.IsConnected != true || _writer == null || _cts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var cancelMessage = new IpcMessage("CancelRequest", requestId, Guid.NewGuid().ToString("N"));
            var json = JsonSerializer.Serialize(cancelMessage);
            if (!await _writeLock.WaitAsync(TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            try
            {
                if (_pipeClient?.IsConnected == true && _writer != null)
                {
                    await _writer.WriteLineAsync(json);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
            // Best-effort remote cancellation; late response is already ignored via RequestId removal.
        }
    }

    public void Disconnect()
    {
        _cts.Cancel();
        FailAllPendingRequests(new IOException("IPC connection was closed."));
        try { _pipeClient?.Dispose(); } catch { }
    }

    private async Task ReadLoopAsync()
    {
        if (_pipeClient == null)
        {
            return;
        }

        using var reader = new StreamReader(_pipeClient, Utf8NoBom, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        while (!_cts.Token.IsCancellationRequested && _pipeClient.IsConnected)
        {
            try
            {
                var line = await reader.ReadLineAsync(_cts.Token);
                if (line == null)
                {
                    break;
                }

                var message = JsonSerializer.Deserialize<IpcMessage>(line);
                if (message == null)
                {
                    continue;
                }

                if (message.Type == "Progress")
                {
                    OnProgressReceived?.Invoke(message.Payload ?? string.Empty);
                }
                else if (message.Type == "Response")
                {
                    if (TryTakePendingRequest(message.RequestId, out var tcs))
                    {
                        tcs.TrySetResult(message.Payload ?? string.Empty);
                    }
                }
                else if (message.Type == "Cancelled")
                {
                    if (TryTakePendingRequest(message.RequestId, out var tcs))
                    {
                        tcs.TrySetCanceled();
                    }
                }
                else if (message.Type == "Error")
                {
                    if (TryTakePendingRequest(message.RequestId, out var tcs))
                    {
                        tcs.TrySetException(new Exception(message.Payload ?? "Unknown server error."));
                    }
                }
            }
            catch
            {
                break;
            }
        }

        FailAllPendingRequests(new IOException("IPC runner disconnected before responding."));
        OnDisconnected?.Invoke();
    }

    private bool TryTakePendingRequest(string? requestId, out TaskCompletionSource<string> tcs)
    {
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            return _pendingRequests.TryRemove(requestId, out tcs!);
        }

        foreach (var pair in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(pair.Key, out tcs!))
            {
                return true;
            }
        }

        tcs = null!;
        return false;
    }

    private void FailAllPendingRequests(Exception error)
    {
        foreach (var key in _pendingRequests.Keys)
        {
            if (_pendingRequests.TryRemove(key, out var tcs))
            {
                tcs.TrySetException(error);
            }
        }
    }
}
