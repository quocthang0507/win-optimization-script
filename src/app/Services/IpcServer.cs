using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class IpcServer
{
    private readonly CleanupService _cleanup;
    private readonly MaintenanceExecutionService _execution;
    private readonly SystemStatusService _status;
    private readonly AppSettingsService _settingsService;
    private readonly ReportService _reports;
    private readonly StartupService _startup;
    private readonly WingetService _winget;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;

    public IpcServer(
        CleanupService cleanup,
        MaintenanceExecutionService execution,
        SystemStatusService status,
        AppSettingsService settingsService,
        ReportService reports,
        StartupService startup,
        WingetService winget)
    {
        _cleanup = cleanup;
        _execution = execution;
        _status = status;
        _settingsService = settingsService;
        _reports = reports;
        _startup = startup;
        _winget = winget;
    }

    public void Start()
    {
        _listenTask = Task.Run(ListenLoopAsync);
    }

    public void Stop()
    {
        _cts.Cancel();
        try
        {
            using var client = new NamedPipeClientStream(".", "WinOptimizationApp_Runner", PipeDirection.InOut);
            client.Connect(100);
        }
        catch
        {
            // Ignore
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    "WinOptimizationApp_Runner",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(_cts.Token);

                using var reader = new StreamReader(pipeServer, Encoding.UTF8);
                using var writer = new StreamWriter(pipeServer, Encoding.UTF8) { AutoFlush = true };

                while (!_cts.Token.IsCancellationRequested && pipeServer.IsConnected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    var request = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (request == null)
                    {
                        continue;
                    }

                    var response = await HandleRequestAsync(request, writer);
                    if (response != null)
                    {
                        var responseLine = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseLine);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC Server connection error: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }

    private async Task<IpcMessage?> HandleRequestAsync(IpcMessage request, StreamWriter writer)
    {
        try
        {
            switch (request.Type)
            {
                case "Handshake":
                    return new IpcMessage("Response", "Connected");

                case "GetStatus":
                    {
                        var status = await _status.GetAsync();
                        var json = JsonSerializer.Serialize(status);
                        return new IpcMessage("Response", json);
                    }

                case "ScanStartup":
                    {
                        var entries = await _startup.ScanAsync();
                        var json = JsonSerializer.Serialize(entries);
                        return new IpcMessage("Response", json);
                    }

                case "ScanWinget":
                    {
                        var packages = await _winget.ScanAsync();
                        var json = JsonSerializer.Serialize(packages);
                        return new IpcMessage("Response", json);
                    }

                case "PreviewTask":
                    {
                        var payload = JsonSerializer.Deserialize<PreviewTaskRequestPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var dummyTask = new MaintenanceTask(
                            payload.TaskId,
                            "Cleanup",
                            "",
                            "",
                            RiskLevel.Safe,
                            false,
                            false,
                            true,
                            false,
                            "");
                        var preview = await _cleanup.PreviewAsync(dummyTask);
                        var json = JsonSerializer.Serialize(preview);
                        return new IpcMessage("Response", json);
                    }

                case "RunTask":
                    {
                        var payload = JsonSerializer.Deserialize<RunTaskRequestPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var taskObj = new MaintenanceTask(
                            payload.TaskId,
                            payload.TaskGroup,
                            payload.TaskLabel,
                            "",
                            RiskLevel.Safe,
                            false,
                            false,
                            false,
                            payload.CanRollback,
                            "");

                        var result = await _execution.RunAsync(taskObj);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json);
                    }

                case "SaveSettings":
                    {
                        var payload = JsonSerializer.Deserialize<SettingsPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var settings = _settingsService.Load();
                        if (payload.Language != null && Enum.TryParse<AppLanguage>(payload.Language, out var lang))
                        {
                            settings.Language = lang;
                        }
                        if (payload.Theme != null && Enum.TryParse<AppTheme>(payload.Theme, out var theme))
                        {
                            settings.Theme = theme;
                        }
                        if (payload.WinUiStyle != null && Enum.TryParse<AppWinUiStyle>(payload.WinUiStyle, out var style))
                        {
                            settings.WinUiStyle = style;
                        }
                        var saved = _settingsService.Save(settings);
                        return new IpcMessage("Response", saved.ToString());
                    }

                case "DeleteReport":
                    {
                        var payload = JsonSerializer.Deserialize<DeleteReportPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        try
                        {
                            var logFile = Path.ChangeExtension(payload.ReportPath, ".log");
                            if (File.Exists(payload.ReportPath))
                            {
                                File.Delete(payload.ReportPath);
                            }
                            if (File.Exists(logFile))
                            {
                                File.Delete(logFile);
                            }
                            return new IpcMessage("Response", "Success");
                        }
                        catch (Exception ex)
                        {
                            return new IpcMessage("Error", ex.Message);
                        }
                    }

                case "Shutdown":
                    _cts.Cancel();
                    return new IpcMessage("Response", "Shutdown");

                default:
                    return new IpcMessage("Error", $"Unknown request type: {request.Type}");
            }
        }
        catch (Exception ex)
        {
            return new IpcMessage("Error", ex.ToString());
        }
    }
}
