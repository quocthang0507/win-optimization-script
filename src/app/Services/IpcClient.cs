using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace WinOptimizationApp.Services;

public sealed class IpcClient
{
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private Task? _readTask;
    private readonly CancellationTokenSource _cts = new();

    private TaskCompletionSource<string>? _activeResponseTcs;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

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
            _writer = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };

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

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeResponseTcs = tcs;

            var message = new IpcMessage(type, payload);
            var json = JsonSerializer.Serialize(message);

            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);

            try
            {
                return await tcs.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The protocol is sequential and has no request IDs. Closing the pipe prevents
                // a late response from being mistaken for the next request after cancellation.
                Disconnect();
                throw;
            }
        }
        finally
        {
            _activeResponseTcs = null;
            _sendLock.Release();
        }
    }

    public void Disconnect()
    {
        _cts.Cancel();
        _activeResponseTcs?.TrySetException(new IOException("IPC connection was closed."));
        _activeResponseTcs = null;
        _writer?.Dispose();
        _pipeClient?.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        if (_pipeClient == null)
        {
            return;
        }

        using var reader = new StreamReader(_pipeClient, Encoding.UTF8);

        while (!_cts.Token.IsCancellationRequested && _pipeClient.IsConnected)
        {
            try
            {
                var line = await reader.ReadLineAsync();
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
                    var tcs = _activeResponseTcs;
                    if (tcs != null)
                    {
                        _activeResponseTcs = null;
                        tcs.TrySetResult(message.Payload ?? string.Empty);
                    }
                }
                else if (message.Type == "Error")
                {
                    var tcs = _activeResponseTcs;
                    if (tcs != null)
                    {
                        _activeResponseTcs = null;
                        tcs.TrySetException(new Exception(message.Payload ?? "Unknown server error."));
                    }
                }
            }
            catch
            {
                break;
            }
        }

        _activeResponseTcs?.TrySetException(new IOException("IPC runner disconnected before responding."));
        _activeResponseTcs = null;
        OnDisconnected?.Invoke();
    }
}
