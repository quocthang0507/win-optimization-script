using System;

namespace WinOptimizationApp.Models;

public sealed class SystemTweak
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // PowerShell script that returns "True" if enabled/applied, otherwise "False"
    public string CheckScript { get; set; } = string.Empty;
    
    // PowerShell script to apply the tweak
    public string EnableScript { get; set; } = string.Empty;
    
    // PowerShell script to revert the tweak
    public string DisableScript { get; set; } = string.Empty;
}

public sealed class TweakStateResponse
{
    public string Id { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Error { get; set; } = string.Empty;
}
