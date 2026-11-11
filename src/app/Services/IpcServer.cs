using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class IpcServer
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly CleanupService _cleanup;
    private readonly MaintenanceExecutionService _execution;
    private readonly SystemStatusService _status;
    private readonly AppSettingsService _settingsService;
    private readonly ReportService _reports;
    private readonly StartupService _startup;
    private readonly WingetService _winget;
    private readonly RegistryCleanerService _registryCleaner;
    private readonly NetworkOptimizationService _networkOptimizer;
    private readonly UninstallerService _uninstaller;
    private readonly string _pipeName;
    private readonly MaintenanceCatalog _catalog = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlightRequests =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TaskCompletionSource<int> _expectedClientProcessId =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _listenTask;

    public Func<string, string?, CancellationToken, Task<string?>>? CustomRequestHandler { get; set; }

    public IpcServer(
        CleanupService cleanup,
        MaintenanceExecutionService execution,
        SystemStatusService status,
        AppSettingsService settingsService,
        ReportService reports,
        StartupService startup,
        WingetService winget,
        RegistryCleanerService registryCleaner,
        NetworkOptimizationService networkOptimizer,
        UninstallerService uninstaller,
        string pipeName)
    {
        if (!AppProcessLauncher.IsValidRunnerPipeName(pipeName))
        {
            throw new ArgumentException("The runner pipe name is invalid.", nameof(pipeName));
        }

        _cleanup = cleanup;
        _execution = execution;
        _status = status;
        _settingsService = settingsService;
        _reports = reports;
        _startup = startup;
        _winget = winget;
        _registryCleaner = registryCleaner;
        _networkOptimizer = networkOptimizer;
        _uninstaller = uninstaller;
        _pipeName = pipeName;
    }

    public void SetExpectedClientProcessId(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        _expectedClientProcessId.TrySetResult(processId);
    }

    public void Start()
    {
        _listenTask = Task.Run(ListenLoopAsync);
    }

    public bool WaitUntilReady(TimeSpan timeout)
    {
        try
        {
            return _ready.Task.Wait(timeout) && _ready.Task.Result;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        foreach (var pair in _inFlightRequests)
        {
            if (_inFlightRequests.TryRemove(pair.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance);
                _ready.TrySetResult(true);

                await pipeServer.WaitForConnectionAsync(_cts.Token);

                var expectedProcessId = await _expectedClientProcessId.Task.WaitAsync(_cts.Token);
                if (!TryGetNamedPipeClientProcessId(pipeServer.SafePipeHandle, out var clientProcessId) ||
                    clientProcessId != expectedProcessId)
                {
                    pipeServer.Disconnect();
                    continue;
                }

                using var reader = new StreamReader(pipeServer, Utf8NoBom, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                using var writer = new StreamWriter(pipeServer, Utf8NoBom, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
                using var writeLock = new SemaphoreSlim(1, 1);
                var activeTasks = new List<Task>();

                while (!_cts.Token.IsCancellationRequested && pipeServer.IsConnected)
                {
                    var line = await reader.ReadLineAsync(_cts.Token);
                    if (line == null)
                    {
                        break;
                    }

                    var request = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (request == null)
                    {
                        continue;
                    }

                    if (request.Type.Equals("CancelRequest", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetRequestId = request.Payload ?? request.RequestId;
                        if (!string.IsNullOrWhiteSpace(targetRequestId) &&
                            _inFlightRequests.TryGetValue(targetRequestId, out var targetCts))
                        {
                            targetCts.Cancel();
                        }

                        continue;
                    }

                    activeTasks.RemoveAll(t => t.IsCompleted);
                    activeTasks.Add(Task.Run(() => ProcessAndRespondAsync(request, writer, writeLock, pipeServer)));
                }

                try
                {
                    await Task.WhenAll(activeTasks);
                }
                catch
                {
                    // Connection ended while in-flight requests were finishing.
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

    private async Task ProcessAndRespondAsync(
        IpcMessage request,
        StreamWriter writer,
        SemaphoreSlim writeLock,
        NamedPipeServerStream pipeServer)
    {
        var requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? Guid.NewGuid().ToString("N")
            : request.RequestId!;
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _inFlightRequests[requestId] = requestCts;

        IpcMessage? response;
        try
        {
            response = await HandleRequestAsync(request, writer, requestCts.Token);
        }
        finally
        {
            _inFlightRequests.TryRemove(requestId, out _);
        }

        if (response != null && pipeServer.IsConnected && !_cts.IsCancellationRequested)
        {
            response.RequestId = request.RequestId;
            var responseLine = JsonSerializer.Serialize(response);
            await writeLock.WaitAsync(_cts.Token);
            try
            {
                if (pipeServer.IsConnected)
                {
                    await writer.WriteLineAsync(responseLine);
                }
            }
            finally
            {
                writeLock.Release();
            }
        }
    }

    private static bool TryGetNamedPipeClientProcessId(SafePipeHandle pipeHandle, out int processId)
    {
        processId = 0;
        if (!GetNamedPipeClientProcessId(pipeHandle, out var nativeProcessId) || nativeProcessId > int.MaxValue)
        {
            return false;
        }

        processId = (int)nativeProcessId;
        return processId > 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    private async Task<IpcMessage?> HandleRequestAsync(
        IpcMessage request,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            if (CustomRequestHandler is not null)
            {
                var customResponse = await CustomRequestHandler(request.Type, request.Payload, cancellationToken);
                if (customResponse is not null)
                {
                    return new IpcMessage("Response", customResponse, request.RequestId);
                }
            }

            switch (request.Type)
            {
                case "Handshake":
                    return new IpcMessage("Response", "Connected", request.RequestId);

                case "GetStatus":
                    {
                        var status = await _status.GetAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(status);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "ScanStartup":
                    {
                        var entries = await _startup.ScanAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(entries);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "EnableStartup":
                    {
                        var entry = JsonSerializer.Deserialize<StartupEntry>(request.Payload ?? "{}");
                        if (entry == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _startup.EnableAsync(entry);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "DisableStartup":
                    {
                        var entry = JsonSerializer.Deserialize<StartupEntry>(request.Payload ?? "{}");
                        if (entry == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _startup.DisableAsync(entry);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "ScanWinget":
                    {
                        var packages = await _winget.ScanAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(packages);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "UpgradeWingetPackage":
                    {
                        var package = JsonSerializer.Deserialize<WingetPackage>(request.Payload ?? "{}");
                        if (package == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }

                        var result = await _winget.UpgradePackageAsync(package, cancellationToken);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "DownloadWingetPackage":
                    {
                        var download = JsonSerializer.Deserialize<WingetPackageDownloadRequest>(request.Payload ?? "{}");
                        if (download == null || string.IsNullOrWhiteSpace(download.DownloadDirectory))
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }

                        var result = await _winget.DownloadPackageAsync(download.Package, download.DownloadDirectory, cancellationToken);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "InstallWingetPackage":
                    {
                        var packageId = request.Payload;
                        if (string.IsNullOrWhiteSpace(packageId))
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }

                        var result = await _winget.InstallPackageAsync(packageId, cancellationToken);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "CheckTweakState":
                    {
                        var tweakId = request.Payload ?? "";
                        var service = new TweakService(new CommandRunner());
                        var result = await service.CheckTweakStateAsync(tweakId, cancellationToken);
                        return new IpcMessage("Response", JsonSerializer.Serialize(result), request.RequestId);
                    }

                case "CheckAllTweakStates":
                    {
                        var service = new TweakService(new CommandRunner());
                        var result = await service.CheckAllTweakStatesAsync(cancellationToken);
                        return new IpcMessage("Response", JsonSerializer.Serialize(result), request.RequestId);
                    }

                case "ApplyTweak":
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(request.Payload ?? "{}");
                        var tweakId = jsonElement.GetProperty("Id").GetString() ?? "";
                        var enable = jsonElement.GetProperty("Enable").GetBoolean();
                        var service = new TweakService(new CommandRunner());
                        var result = await service.ApplyTweakAsync(tweakId, enable, cancellationToken);
                        return new IpcMessage("Response", JsonSerializer.Serialize(result), request.RequestId);
                    }

                case "PreviewTask":
                    {
                        var payload = JsonSerializer.Deserialize<PreviewTaskRequestPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        if (!_catalog.TryGetById(payload.TaskId, out var task))
                        {
                            return new IpcMessage("Error", $"Unknown task: {payload.TaskId}", request.RequestId);
                        }

                        var preview = await _cleanup.PreviewAsync(task, payload.ProtectedPaths, cancellationToken);
                        var json = JsonSerializer.Serialize(preview);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "RunTask":
                    {
                        var payload = JsonSerializer.Deserialize<RunTaskRequestPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        if (!_catalog.TryGetById(payload.TaskId, out var taskObj))
                        {
                            return new IpcMessage("Error", $"Unknown task: {payload.TaskId}", request.RequestId);
                        }

                        var result = await _execution.RunAsync(taskObj, payload.ProtectedPaths, cancellationToken);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "SaveSettings":
                    {
                        var payload = JsonSerializer.Deserialize<SettingsPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
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
                        return new IpcMessage("Response", saved.ToString(), request.RequestId);
                    }

                case "DeleteReport":
                    {
                        var payload = JsonSerializer.Deserialize<DeleteReportPayload>(request.Payload ?? "{}");
                        if (payload == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        try
                        {
                            if (!ReportService.TryResolveReportDeleteTargets(_reports.LogsDirectory, payload.ReportPath, out var targets))
                            {
                                return new IpcMessage("Error", "Report path is outside the logs directory or is not a maintenance report.", request.RequestId);
                            }

                            foreach (var target in targets)
                            {
                                if (File.Exists(target))
                                {
                                    File.Delete(target);
                                }
                            }

                            return new IpcMessage("Response", "Success", request.RequestId);
                        }
                        catch (Exception ex)
                        {
                            return new IpcMessage("Error", ex.Message, request.RequestId);
                        }
                    }

                case "ScanRegistry":
                    {
                        var issues = await _registryCleaner.ScanAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(issues);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "CleanRegistry":
                    {
                        var issues = JsonSerializer.Deserialize<List<RegistryIssue>>(request.Payload ?? "[]");
                        if (issues == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _registryCleaner.CleanAsync(issues, cancellationToken);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "RunNetworkRepair":
                    {
                        var action = request.Payload;
                        bool ok = action switch
                        {
                            "FlushDns" => await _networkOptimizer.FlushDnsAsync(cancellationToken),
                            "ResetWinsock" => await _networkOptimizer.ResetWinsockAsync(cancellationToken),
                            "RenewIp" => await _networkOptimizer.RenewIpAsync(cancellationToken),
                            _ => false
                        };
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "ScanInstalledApps":
                    {
                        var apps = await _uninstaller.ScanInstalledAppsAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(apps);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "UninstallApp":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _uninstaller.UninstallAppAsync(app, cancellationToken);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "ScanLeftovers":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var candidates = await _uninstaller.ScanLeftoversAsync(app, cancellationToken);
                        var json = JsonSerializer.Serialize(candidates);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "CleanLeftovers":
                    {
                        var paths = JsonSerializer.Deserialize<List<string>>(request.Payload ?? "[]");
                        if (paths == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _uninstaller.DeleteLeftoversAsync(paths, cancellationToken);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "ScanAppxPackages":
                    {
                        var apps = await _uninstaller.ScanAppxPackagesAsync(cancellationToken);
                        var json = JsonSerializer.Serialize(apps);
                        return new IpcMessage("Response", json, request.RequestId);
                    }

                case "RemoveAppxPackage":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload", request.RequestId);
                        }
                        var ok = await _uninstaller.RemoveAppxPackageAsync(app, cancellationToken);
                        return new IpcMessage("Response", ok ? "Success" : "Failed", request.RequestId);
                    }

                case "Shutdown":
                    _cts.Cancel();
                    return new IpcMessage("Response", "Shutdown", request.RequestId);

                default:
                    return new IpcMessage("Error", $"Unknown request type: {request.Type}", request.RequestId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new IpcMessage("Cancelled", "Operation was cancelled.", request.RequestId);
        }
        catch (Exception ex)
        {
            return new IpcMessage("Error", ex.ToString(), request.RequestId);
        }
    }
}
