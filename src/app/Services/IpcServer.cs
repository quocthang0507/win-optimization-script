using System.IO.Pipes;
using System.Text;
using System.Text.Json;
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
    private readonly MaintenanceCatalog _catalog = new();
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
                    2,
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
                        if (!_catalog.TryGetById(payload.TaskId, out var task))
                        {
                            return new IpcMessage("Error", $"Unknown task: {payload.TaskId}");
                        }

                        var preview = await _cleanup.PreviewAsync(task);
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
                        if (!_catalog.TryGetById(payload.TaskId, out var taskObj))
                        {
                            return new IpcMessage("Error", $"Unknown task: {payload.TaskId}");
                        }

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
                            if (!ReportService.TryResolveReportDeleteTargets(_reports.LogsDirectory, payload.ReportPath, out var targets))
                            {
                                return new IpcMessage("Error", "Report path is outside the logs directory or is not a maintenance report.");
                            }

                            foreach (var target in targets)
                            {
                                if (File.Exists(target))
                                {
                                    File.Delete(target);
                                }
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
