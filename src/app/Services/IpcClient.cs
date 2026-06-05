using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinOptimizationApp.Services;

public sealed class IpcClient
{
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private Task? _readTask;
    private readonly CancellationTokenSource _cts = new();
    
    private TaskCompletionSource<string>? _activeResponseTcs;
    
    public event Action<string>? OnProgressReceived;
    public event Action? OnDisconnected;

    public bool IsConnected => _pipeClient?.IsConnected == true;

    public async Task<bool> ConnectAsync(int timeoutMs = 2000)
    {
        try
        {
            _pipeClient = new NamedPipeClientStream(".", "WinOptimizationApp_Runner", PipeDirection.InOut, PipeOptions.Asynchronous);
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

    public async Task<string> SendRequestAsync(string type, string? payload = null)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected || _writer == null)
        {
            throw new InvalidOperationException("IPC Client is not connected to Runner.");
        }

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _activeResponseTcs = tcs;

        var message = new IpcMessage(type, payload);
        var json = JsonSerializer.Serialize(message);
        
        await _writer.WriteLineAsync(json);

        return await tcs.Task;
    }

    public void Disconnect()
    {
        _cts.Cancel();
        _writer?.Dispose();
        _pipeClient?.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        if (_pipeClient == null) return;

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
                        tcs.SetResult(message.Payload ?? string.Empty);
                    }
                }
                else if (message.Type == "Error")
                {
                    var tcs = _activeResponseTcs;
                    if (tcs != null)
                    {
                        _activeResponseTcs = null;
                        tcs.SetException(new Exception(message.Payload ?? "Unknown server error."));
                    }
                }
            }
            catch
            {
                break;
            }
        }

        OnDisconnected?.Invoke();
    }
}
