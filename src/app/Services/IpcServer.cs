using WinOptimizationApp.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

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
    private readonly RegistryCleanerService _registryCleaner;
    private readonly NetworkOptimizationService _networkOptimizer;
    private readonly UninstallerService _uninstaller;
    private readonly string _pipeName;
    private readonly MaintenanceCatalog _catalog = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<int> _expectedClientProcessId =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _listenTask;

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

                case "EnableStartup":
                    {
                        var entry = JsonSerializer.Deserialize<StartupEntry>(request.Payload ?? "{}");
                        if (entry == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _startup.EnableAsync(entry);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "DisableStartup":
                    {
                        var entry = JsonSerializer.Deserialize<StartupEntry>(request.Payload ?? "{}");
                        if (entry == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _startup.DisableAsync(entry);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "ScanWinget":
                    {
                        var packages = await _winget.ScanAsync();
                        var json = JsonSerializer.Serialize(packages);
                        return new IpcMessage("Response", json);
                    }

                case "UpgradeWingetPackage":
                    {
                        var package = JsonSerializer.Deserialize<WingetPackage>(request.Payload ?? "{}");
                        if (package == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }

                        var result = await _winget.UpgradePackageAsync(package);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json);
                    }

                case "DownloadWingetPackage":
                    {
                        var download = JsonSerializer.Deserialize<WingetPackageDownloadRequest>(request.Payload ?? "{}");
                        if (download == null || string.IsNullOrWhiteSpace(download.DownloadDirectory))
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }

                        var result = await _winget.DownloadPackageAsync(download.Package, download.DownloadDirectory);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json);
                    }

                case "InstallWingetPackage":
                    {
                        var packageId = request.Payload;
                        if (string.IsNullOrWhiteSpace(packageId))
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }

                        var result = await _winget.InstallPackageAsync(packageId);
                        var json = JsonSerializer.Serialize(result);
                        return new IpcMessage("Response", json);
                    }

                case "CheckTweakState":
                    {
                        var tweakId = request.Payload ?? "";
                        var service = new TweakService(new CommandRunner());
                        var result = await service.CheckTweakStateAsync(tweakId);
                        return new IpcMessage("Response", JsonSerializer.Serialize(result));
                    }

                case "CheckAllTweakStates":
                    {
                        var service = new TweakService(new CommandRunner());
                        var result = await service.CheckAllTweakStatesAsync();
                        return new IpcMessage("Response", JsonSerializer.Serialize(result));
                    }

                case "ApplyTweak":
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(request.Payload ?? "{}");
                        var tweakId = jsonElement.GetProperty("Id").GetString() ?? "";
                        var enable = jsonElement.GetProperty("Enable").GetBoolean();
                        var service = new TweakService(new CommandRunner());
                        var result = await service.ApplyTweakAsync(tweakId, enable);
                        return new IpcMessage("Response", JsonSerializer.Serialize(result));
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

                        var preview = await _cleanup.PreviewAsync(task, payload.ProtectedPaths);
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

                        var result = await _execution.RunAsync(taskObj, payload.ProtectedPaths);
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

                case "ScanRegistry":
                    {
                        var issues = await _registryCleaner.ScanAsync();
                        var json = JsonSerializer.Serialize(issues);
                        return new IpcMessage("Response", json);
                    }

                case "CleanRegistry":
                    {
                        var issues = JsonSerializer.Deserialize<List<RegistryIssue>>(request.Payload ?? "[]");
                        if (issues == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _registryCleaner.CleanAsync(issues);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "RunNetworkRepair":
                    {
                        var action = request.Payload;
                        bool ok = action switch
                        {
                            "FlushDns" => await _networkOptimizer.FlushDnsAsync(),
                            "ResetWinsock" => await _networkOptimizer.ResetWinsockAsync(),
                            "RenewIp" => await _networkOptimizer.RenewIpAsync(),
                            _ => false
                        };
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "ScanInstalledApps":
                    {
                        var apps = await _uninstaller.ScanInstalledAppsAsync();
                        var json = JsonSerializer.Serialize(apps);
                        return new IpcMessage("Response", json);
                    }

                case "UninstallApp":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _uninstaller.UninstallAppAsync(app);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "ScanLeftovers":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var paths = await _uninstaller.ScanLeftoversAsync(app);
                        var json = JsonSerializer.Serialize(paths);
                        return new IpcMessage("Response", json);
                    }

                case "CleanLeftovers":
                    {
                        var paths = JsonSerializer.Deserialize<List<string>>(request.Payload ?? "[]");
                        if (paths == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _uninstaller.DeleteLeftoversAsync(paths);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
                    }

                case "ScanAppxPackages":
                    {
                        var apps = await _uninstaller.ScanAppxPackagesAsync();
                        var json = JsonSerializer.Serialize(apps);
                        return new IpcMessage("Response", json);
                    }

                case "RemoveAppxPackage":
                    {
                        var app = JsonSerializer.Deserialize<InstalledApp>(request.Payload ?? "{}");
                        if (app == null)
                        {
                            return new IpcMessage("Error", "Invalid payload");
                        }
                        var ok = await _uninstaller.RemoveAppxPackageAsync(app);
                        return new IpcMessage("Response", ok ? "Success" : "Failed");
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
