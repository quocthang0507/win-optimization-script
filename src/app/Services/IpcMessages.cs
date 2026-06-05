namespace WinOptimizationApp.Services;

public sealed class IpcMessage
{
    public string Type { get; set; } = string.Empty;
    public string? Payload { get; set; }

    public IpcMessage() { }

    public IpcMessage(string type, string? payload = null)
    {
        Type = type;
        Payload = payload;
    }
}

public sealed class RunTaskRequestPayload
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskLabel { get; set; } = string.Empty;
    public string TaskGroup { get; set; } = string.Empty;
    public bool CanRollback { get; set; }
}

public sealed class PreviewTaskRequestPayload
{
    public string TaskId { get; set; } = string.Empty;
}

public sealed class SettingsPayload
{
    public string? Language { get; set; }
    public string? Theme { get; set; }
    public string? WinUiStyle { get; set; }
}

public sealed class DeleteReportPayload
{
    public string ReportPath { get; set; } = string.Empty;
}
