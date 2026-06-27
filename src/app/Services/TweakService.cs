using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class TweakService
{
    private readonly CommandRunner _commands;
    public IpcClient? Client { get; set; }

    public static readonly List<SystemTweak> Tweaks = new()
    {
        // Privacy
        new SystemTweak
        {
            Id = "privacy.telemetry",
            Category = "Privacy",
            Title = "Disable Windows Telemetry",
            Description = "Stops Windows from sending diagnostic data to Microsoft.",
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue).AllowTelemetry -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                Disable-ScheduledTask -TaskName 'Microsoft\Windows\Customer Experience Improvement Program\Consolidator' -ErrorAction SilentlyContinue
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue
                Enable-ScheduledTask -TaskName 'Microsoft\Windows\Customer Experience Improvement Program\Consolidator' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "privacy.activityHistory",
            Category = "Privacy",
            Title = "Disable Activity History",
            Description = "Prevents Windows from tracking your activity across apps and devices.",
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -ErrorAction SilentlyContinue).EnableActivityFeed -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'UploadUserActivities' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -ErrorAction SilentlyContinue
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -ErrorAction SilentlyContinue
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'UploadUserActivities' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "privacy.appDiagnostics",
            Category = "Privacy",
            Title = "Disable App Diagnostics",
            Description = "Prevents apps from accessing diagnostic information about other apps.",
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -ErrorAction SilentlyContinue).LetAppsGetDiagnosticInfo -eq 2",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -Value 2 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -ErrorAction SilentlyContinue
            "
        },
        // Gaming
        new SystemTweak
        {
            Id = "gaming.gameMode",
            Category = "Gaming",
            Title = "Enable Game Mode",
            Description = "Optimizes Windows for gaming by prioritizing game processes.",
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -ErrorAction SilentlyContinue).AllowAutoGameMode -eq 1",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\GameBar' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -Value 0 -Type DWord -Force
            "
        },
        new SystemTweak
        {
            Id = "gaming.gameBar",
            Category = "Gaming",
            Title = "Disable Xbox Game Bar",
            Description = "Turns off Xbox Game Bar, which can improve performance in some games.",
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\System\GameConfigStore' -Name 'GameDVR_Enabled' -ErrorAction SilentlyContinue).GameDVR_Enabled -eq 0",
            EnableScript = @"
                New-Item -Path 'HKCU:\System\GameConfigStore' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\System\GameConfigStore' -Name 'GameDVR_Enabled' -Value 0 -Type DWord -Force
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\System\GameConfigStore' -Name 'GameDVR_Enabled' -Value 1 -Type DWord -Force
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -ErrorAction SilentlyContinue
            "
        },
        // Taskbar/UI
        new SystemTweak
        {
            Id = "ui.taskbarLeft",
            Category = "UI/Taskbar",
            Title = "Align Taskbar to Left (Win 11)",
            Description = "Moves the Taskbar icons to the left like Windows 10.",
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarAl' -ErrorAction SilentlyContinue).TaskbarAl -eq 0",
            EnableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarAl' -Value 0 -Type DWord -Force
                Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarAl' -Value 1 -Type DWord -Force
                Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "ui.hideSearch",
            Category = "UI/Taskbar",
            Title = "Hide Taskbar Search",
            Description = "Hides the search box on the taskbar to save space.",
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -ErrorAction SilentlyContinue).SearchboxTaskbarMode -eq 0",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 1 -Type DWord -Force
            "
        },
        new SystemTweak
        {
            Id = "ui.hideTaskView",
            Category = "UI/Taskbar",
            Title = "Hide Task View Button",
            Description = "Hides the Task View button from the taskbar.",
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -ErrorAction SilentlyContinue).ShowTaskViewButton -eq 0",
            EnableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -Value 1 -Type DWord -Force
            "
        },
        // System
        new SystemTweak
        {
            Id = "system.hibernation",
            Category = "System",
            Title = "Disable Hibernation",
            Description = "Frees up disk space by disabling the hibernation feature.",
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Session Manager\Power' -Name 'HibernateEnabled' -ErrorAction SilentlyContinue).HibernateEnabled -eq 0",
            EnableScript = @"
                powercfg.exe /hibernate off
            ",
            DisableScript = @"
                powercfg.exe /hibernate on
            "
        },
        new SystemTweak
        {
            Id = "system.cortana",
            Category = "System",
            Title = "Disable Cortana",
            Description = "Turns off the Cortana voice assistant to save resources.",
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -ErrorAction SilentlyContinue).AllowCortana -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -ErrorAction SilentlyContinue
            "
        }
    };

    public TweakService(CommandRunner commands)
    {
        _commands = commands;
    }

    public IReadOnlyList<SystemTweak> GetAllTweaks() => Tweaks;

    public async Task<TweakStateResponse> CheckTweakStateAsync(string id)
    {
        if (Client != null)
        {
            var response = await Client.SendRequestAsync("CheckTweakState", id);
            return System.Text.Json.JsonSerializer.Deserialize<TweakStateResponse>(response)
                ?? new TweakStateResponse { Id = id, Error = "Deserialization failed." };
        }

        var tweak = Tweaks.FirstOrDefault(t => t.Id == id);
        if (tweak == null) return new TweakStateResponse { Id = id, Error = "Tweak not found." };

        var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -Command \"{tweak.CheckScript}\"");
        var output = result.StandardOutput.Trim();
        
        return new TweakStateResponse
        {
            Id = id,
            IsEnabled = output.Equals("True", StringComparison.OrdinalIgnoreCase),
            Error = result.ExitCode != 0 ? result.StandardError : ""
        };
    }

    public async Task<TweakStateResponse> ApplyTweakAsync(string id, bool enable)
    {
        if (Client != null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { Id = id, Enable = enable });
            var response = await Client.SendRequestAsync("ApplyTweak", payload);
            return System.Text.Json.JsonSerializer.Deserialize<TweakStateResponse>(response)
                ?? new TweakStateResponse { Id = id, Error = "Deserialization failed." };
        }

        var tweak = Tweaks.FirstOrDefault(t => t.Id == id);
        if (tweak == null) return new TweakStateResponse { Id = id, Error = "Tweak not found." };

        var script = enable ? tweak.EnableScript : tweak.DisableScript;
        var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -Command \"{script}\"");
        
        return new TweakStateResponse
        {
            Id = id,
            IsEnabled = enable, // Assume it applied if exit code is 0, but ideally we re-check
            Error = result.ExitCode != 0 ? result.StandardError : ""
        };
    }
}
