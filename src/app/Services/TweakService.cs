using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class TweakService
{
    private readonly CommandRunner _commands;
    private readonly IRegistryAccessor _registry;
    public IpcClient? Client { get; set; }

    public static readonly List<SystemTweak> Tweaks = new()
    {
        // Privacy
        new SystemTweak
        {
            Id = "privacy.telemetry",
            Category = "Privacy",
            Title = "Disable Windows Telemetry",
            Description = "Requests the minimum diagnostic data level supported by the installed Windows edition.",
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", "DWord", "0", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue).AllowTelemetry -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "privacy.activityHistory",
            Category = "Privacy",
            Title = "Disable Activity History",
            Description = "Prevents Windows from tracking your activity across apps and devices.",
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", "DWord", "0", null, RequiredForCheck: true),
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", "DWord", "0", null, RequiredForCheck: false),
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", "DWord", "0", null, RequiredForCheck: false)
            ],
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
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "LetAppsGetDiagnosticInfo", "DWord", "2", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -ErrorAction SilentlyContinue).LetAppsGetDiagnosticInfo -eq 2",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -Value 2 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'LetAppsGetDiagnosticInfo' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "privacy.windowsSuggestions",
            Category = "Privacy",
            Title = "Reduce Windows Tips and Suggestions",
            Description = "Disables common welcome, Start menu, Settings, and usage suggestions without removing Windows components.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", "DWord", "0", null),
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", "DWord", "0", null),
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", "DWord", "0", null),
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", "DWord", "0", null),
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", "DWord", "0", null),
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", "DWord", "0", null)
            ],
            CheckScript = @"$cdm = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -ErrorAction SilentlyContinue; $advanced = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -ErrorAction SilentlyContinue; $cdm.'SubscribedContent-310093Enabled' -eq 0 -and $cdm.'SubscribedContent-338388Enabled' -eq 0 -and $cdm.'SubscribedContent-338389Enabled' -eq 0 -and $cdm.SoftLandingEnabled -eq 0 -and $cdm.SystemPaneSuggestionsEnabled -eq 0 -and $advanced.Start_IrisRecommendations -eq 0",
            EnableScript = @"
                $cdm = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
                $advanced = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
                New-Item -Path $cdm -Force | Out-Null
                New-Item -Path $advanced -Force | Out-Null
                @('SubscribedContent-310093Enabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SoftLandingEnabled','SystemPaneSuggestionsEnabled') | ForEach-Object {
                    Set-ItemProperty -Path $cdm -Name $_ -Value 0 -Type DWord -Force
                }
                Set-ItemProperty -Path $advanced -Name 'Start_IrisRecommendations' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                $cdm = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
                @('SubscribedContent-310093Enabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SoftLandingEnabled','SystemPaneSuggestionsEnabled') | ForEach-Object {
                    Remove-ItemProperty -Path $cdm -Name $_ -ErrorAction SilentlyContinue
                }
                Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_IrisRecommendations' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "privacy.webSearch",
            Category = "Privacy",
            Title = "Disable Web Suggestions in Windows Search",
            Description = "Keeps Start and taskbar searches focused on local apps, settings, and files.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            RegistryRules =
            [
                new("HKCU", @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", "DWord", "1", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' -Name 'DisableSearchBoxSuggestions' -ErrorAction SilentlyContinue).DisableSearchBoxSuggestions -eq 1",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' -Name 'DisableSearchBoxSuggestions' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' -Name 'DisableSearchBoxSuggestions' -ErrorAction SilentlyContinue
            "
        },
        // Gaming
        new SystemTweak
        {
            Id = "gaming.gameMode",
            Category = "Gaming",
            Title = "Enable Game Mode",
            Description = "Optimizes Windows for gaming by prioritizing game processes.",
            RiskLevel = RiskLevel.Safe,
            RequiresAdministrator = false,
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", "DWord", "1", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -ErrorAction SilentlyContinue).AllowAutoGameMode -eq 1",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\GameBar' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AllowAutoGameMode' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "gaming.gameBar",
            Category = "Gaming",
            Title = "Disable Xbox Game Bar",
            Description = "Turns off Xbox Game Bar, which can improve performance in some games.",
            RegistryRules =
            [
                new("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", "DWord", "0", "1", RequiredForCheck: true),
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", "DWord", "0", null, RequiredForCheck: false)
            ],
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
            RequiresAdministrator = false,
            SupportedWindows = "Windows 11",
            RestartRequirement = "Explorer restarts",
            UsesCustomApplyScript = true,
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", "DWord", "0", "1")
            ],
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
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", "DWord", "0", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -ErrorAction SilentlyContinue).SearchboxTaskbarMode -eq 0",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "ui.hideTaskView",
            Category = "UI/Taskbar",
            Title = "Hide Task View Button",
            Description = "Hides the Task View button from the taskbar.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", "DWord", "0", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -ErrorAction SilentlyContinue).ShowTaskViewButton -eq 0",
            EnableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "ui.showFileExtensions",
            Category = "UI/Explorer",
            Title = "Show File Name Extensions",
            Description = "Shows extensions for known file types in File Explorer, making executable and script files easier to identify.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            RestartRequirement = "New Explorer window",
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", "DWord", "0", "1")
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideFileExt' -ErrorAction SilentlyContinue).HideFileExt -eq 0",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideFileExt' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideFileExt' -Value 1 -Type DWord -Force
            "
        },
        new SystemTweak
        {
            Id = "ui.endTaskTaskbar",
            Category = "UI/Taskbar",
            Title = "Enable End Task on the Taskbar",
            Description = "Adds End Task to an app's taskbar context menu on supported Windows 11 versions.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            SupportedWindows = "Windows 11",
            RegistryRules =
            [
                new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", "DWord", "1", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -ErrorAction SilentlyContinue).TaskbarEndTask -eq 1",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "ui.classicContextMenu",
            Category = "UI/Explorer",
            Title = "Restore Classic Context Menu (Win 11)",
            Description = "Shows the full Windows 10 right-click context menu directly without clicking Show more options.",
            RequiresAdministrator = false,
            RiskLevel = RiskLevel.Safe,
            SupportedWindows = "Windows 11",
            RestartRequirement = "Explorer restarts",
            RegistryRules =
            [
                new("HKCU", @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "String", "", null)
            ],
            CheckScript = @"Test-Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32'",
            EnableScript = @"
                New-Item -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Force | Out-Null
                Set-ItemProperty -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Name '(Default)' -Value '' -Force
            ",
            DisableScript = @"
                Remove-Item -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}' -Recurse -Force -ErrorAction SilentlyContinue
            "
        },
        // System
        new SystemTweak
        {
            Id = "system.hibernation",
            Category = "System",
            Title = "Disable Hibernation",
            Description = "Frees up disk space by disabling the hibernation feature.",
            UsesCustomApplyScript = true,
            RegistryRules =
            [
                new("HKLM", @"System\CurrentControlSet\Control\Session Manager\Power", "HibernateEnabled", "DWord", "0", "1")
            ],
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
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", "DWord", "0", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -ErrorAction SilentlyContinue).AllowCortana -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "system.preventUpdateAutoReboot",
            Category = "System",
            Title = "Prevent Automatic Restart After Updates",
            Description = "Prevents Windows Update from automatically restarting while a user is signed in.",
            RiskLevel = RiskLevel.Medium,
            RestartRequirement = "Restart required",
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", "DWord", "1", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name 'NoAutoRebootWithLoggedOnUsers' -ErrorAction SilentlyContinue).NoAutoRebootWithLoggedOnUsers -eq 1",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name 'NoAutoRebootWithLoggedOnUsers' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name 'NoAutoRebootWithLoggedOnUsers' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "system.disableDeliveryOptimization",
            Category = "System",
            Title = "Disable Peer-to-Peer Update Sharing",
            Description = "Prevents Windows Update Delivery Optimization from sharing update data with other PCs.",
            RiskLevel = RiskLevel.Medium,
            RegistryRules =
            [
                new("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", "DWord", "0", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' -Name 'DODownloadMode' -ErrorAction SilentlyContinue).DODownloadMode -eq 0",
            EnableScript = @"
                New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' -Force | Out-Null
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' -Name 'DODownloadMode' -Value 0 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' -Name 'DODownloadMode' -ErrorAction SilentlyContinue
            "
        },
        new SystemTweak
        {
            Id = "system.utcClock",
            Category = "System",
            Title = "Use UTC for the Hardware Clock",
            Description = "Useful for Windows and Linux dual-boot systems. Do not enable this on a normal Windows-only setup.",
            RequiresAdministrator = true,
            RiskLevel = RiskLevel.Medium,
            RestartRequirement = "Restart required",
            RegistryRules =
            [
                new("HKLM", @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", "DWord", "1", null)
            ],
            CheckScript = @"(Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation' -Name 'RealTimeIsUniversal' -ErrorAction SilentlyContinue).RealTimeIsUniversal -eq 1",
            EnableScript = @"
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation' -Name 'RealTimeIsUniversal' -Value 1 -Type DWord -Force
            ",
            DisableScript = @"
                Remove-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation' -Name 'RealTimeIsUniversal' -ErrorAction SilentlyContinue
            "
        }
    };

    public TweakService(CommandRunner commands, IRegistryAccessor? registry = null)
    {
        _commands = commands;
        _registry = registry ?? new WindowsRegistryAccessor();
    }

    public IReadOnlyList<SystemTweak> GetAllTweaks() => Tweaks;

    public async Task<IReadOnlyList<TweakStateResponse>> CheckAllTweakStatesAsync(
        CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("CheckAllTweakStates", cancellationToken: cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<List<TweakStateResponse>>(response) ?? [];
        }

        var results = new List<TweakStateResponse>(Tweaks.Count);
        var scriptTweaks = new List<SystemTweak>();

        foreach (var tweak in Tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tweak.RegistryRules.Count > 0)
            {
                results.Add(EvaluateRegistryRules(tweak));
            }
            else
            {
                scriptTweaks.Add(tweak);
            }
        }

        if (scriptTweaks.Count > 0)
        {
            var checks = scriptTweaks.Select(tweak =>
            {
                var id = tweak.Id.Replace("'", "''", StringComparison.Ordinal);
                return $@"try {{
                    $enabled = [bool](& {{ {tweak.CheckScript} }})
                    [pscustomobject]@{{ Id = '{id}'; IsEnabled = $enabled; Error = '' }}
                }} catch {{
                    [pscustomobject]@{{ Id = '{id}'; IsEnabled = $false; Error = $_.Exception.Message }}
                }}";
            });
            var script = $"$results = @({string.Join(Environment.NewLine, checks)}); ConvertTo-Json -InputObject $results -Depth 3 -Compress";
            var result = await _commands.RunCaptureAsync(
                "powershell.exe",
                $"-NoProfile -Command \"{script}\"",
                cancellationToken);
            if (result.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"PowerShell exited with code {result.ExitCode}."
                    : result.StandardError.Trim();
                throw new InvalidOperationException(error);
            }

            var scriptStates = System.Text.Json.JsonSerializer.Deserialize<List<TweakStateResponse>>(result.StandardOutput) ?? [];
            results.AddRange(scriptStates);
        }

        return results;
    }

    public async Task<TweakStateResponse> CheckTweakStateAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("CheckTweakState", id, cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<TweakStateResponse>(response)
                ?? new TweakStateResponse { Id = id, Error = "Deserialization failed." };
        }

        var tweak = Tweaks.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (tweak == null) return new TweakStateResponse { Id = id, Error = "Tweak not found." };

        if (tweak.RegistryRules.Count > 0)
        {
            return EvaluateRegistryRules(tweak);
        }

        var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -Command \"{tweak.CheckScript}\"", cancellationToken);
        var output = result.StandardOutput.Trim();
        
        return new TweakStateResponse
        {
            Id = tweak.Id,
            IsEnabled = output.Equals("True", StringComparison.OrdinalIgnoreCase),
            Error = result.ExitCode != 0 ? result.StandardError : ""
        };
    }

    public async Task<TweakStateResponse> ApplyTweakAsync(string id, bool enable, CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { Id = id, Enable = enable });
            var response = await Client.SendRequestAsync("ApplyTweak", payload, cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<TweakStateResponse>(response)
                ?? new TweakStateResponse { Id = id, Error = "Deserialization failed." };
        }

        var tweak = Tweaks.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (tweak == null) return new TweakStateResponse { Id = id, Error = "Tweak not found." };

        if (tweak.RegistryRules.Count > 0 && !tweak.UsesCustomApplyScript)
        {
            try
            {
                foreach (var rule in tweak.RegistryRules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (enable)
                    {
                        _registry.WriteValue(rule.Hive, rule.SubKey, rule.ValueName, rule.ValueKind, rule.EnabledValue);
                    }
                    else if (rule.DisabledValue is not null)
                    {
                        _registry.WriteValue(rule.Hive, rule.SubKey, rule.ValueName, rule.ValueKind, rule.DisabledValue);
                    }
                    else
                    {
                        _registry.DeleteValue(rule.Hive, rule.SubKey, rule.ValueName);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new TweakStateResponse
                {
                    Id = tweak.Id,
                    IsEnabled = !enable,
                    Error = ex.Message
                };
            }
        }
        else
        {
            var script = enable ? tweak.EnableScript : tweak.DisableScript;
            var result = await _commands.RunCaptureAsync("powershell.exe", $"-NoProfile -Command \"{script}\"", cancellationToken);
            if (result.ExitCode != 0)
            {
                return new TweakStateResponse
                {
                    Id = tweak.Id,
                    IsEnabled = !enable,
                    Error = string.IsNullOrWhiteSpace(result.StandardError) ? $"PowerShell exited with code {result.ExitCode}." : result.StandardError.Trim()
                };
            }
        }

        var verified = await CheckTweakStateAsync(tweak.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(verified.Error) && verified.IsEnabled != enable)
        {
            verified.Error = "The command completed, but Windows did not report the requested state.";
        }

        return verified;
    }

    private TweakStateResponse EvaluateRegistryRules(SystemTweak tweak)
    {
        try
        {
            var checkRules = tweak.RegistryRules.Where(r => r.RequiredForCheck).ToList();
            if (checkRules.Count == 0)
            {
                checkRules = tweak.RegistryRules.ToList();
            }

            var isEnabled = checkRules.All(rule =>
            {
                var snapshot = _registry.ReadValue(rule.Hive, rule.SubKey, rule.ValueName);
                return snapshot.Exists &&
                       string.Equals(snapshot.RawValue, rule.EnabledValue, StringComparison.OrdinalIgnoreCase);
            });

            return new TweakStateResponse
            {
                Id = tweak.Id,
                IsEnabled = isEnabled,
                Error = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new TweakStateResponse
            {
                Id = tweak.Id,
                IsEnabled = false,
                Error = ex.Message
            };
        }
    }
}
