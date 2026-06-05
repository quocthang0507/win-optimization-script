param(
    [switch]$Compile
)

# Check for Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script must be run as Administrator." -ForegroundColor Red
    Write-Host "Right-click the file and choose 'Run with PowerShell (Admin)'." -ForegroundColor Yellow
    Pause
    exit
}

# Enable UTF-8 output for proper character display
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Pause-ForUser {
    param(
        [string]$Message = "Press Enter to return to the menu"
    )
    [void](Read-Host $Message)
}

function Invoke-Spinner {
    param(
        [string]$Message,
        [ScriptBlock]$ScriptBlock,
        [object[]]$Arguments = @(),
        [ScriptBlock]$InitializationScript = $null
    )

    Write-Host -NoNewline "$Message ["
    $spinner = @('-', '\', '|', '/')
    $i = 0
    
    if ($null -ne $InitializationScript) {
        $job = Start-Job -InitializationScript $InitializationScript -ScriptBlock $ScriptBlock -ArgumentList $Arguments
    }
    else {
        $job = Start-Job -ScriptBlock $ScriptBlock -ArgumentList $Arguments
    }
    
    while ($job.State -eq 'Running') {
        Write-Host -NoNewline ("`b" + $spinner[$i])
        $i = ($i + 1) % $spinner.Count
        Start-Sleep -Milliseconds 100
    }
    
    Write-Host -NoNewline "`b] Done." -ForegroundColor Green
    Write-Host ""
    
    Receive-Job -Job $job | ForEach-Object { Write-Host $_ }
    Remove-Job -Job $job
}

function Write-Rule {
    param(
        [int]$Width = 45,
        [ConsoleColor]$Color = [ConsoleColor]::DarkGray
    )
    Write-Host ("=" * $Width) -ForegroundColor $Color
}

function Write-Title {
    param(
        [string]$Text,
        [string]$Subtitle
    )

    $Host.UI.RawUI.WindowTitle = $Text
    Clear-Host
    Write-Rule
    Write-Host (" {0} " -f $Text.ToUpperInvariant()) -ForegroundColor Cyan
    if ($Subtitle) {
        Write-Host (" {0} " -f $Subtitle) -ForegroundColor DarkGray
    }
    Write-Rule
}

function Write-Section {
    param(
        [Parameter(Mandatory)]
        [string]$Title
    )
    Write-Host "[$Title]" -ForegroundColor Magenta
}

function Get-OrderedMenuItems {
    $orderedItems = foreach ($group in $script:MenuGroupOrder) {
        if ($group -eq 'Exit') { continue }

        $script:MenuItemsRaw | Where-Object { $_.Group -eq $group }
    }

    @($orderedItems)
}

function Get-DirectoryCleanupInitializationScript {
    {
        function Clear-DirectoryContents {
            param(
                [Parameter(Mandatory)]
                [string]$Path
            )

            if (-not (Test-Path -LiteralPath $Path)) {
                return [pscustomobject]@{
                    Exists = $false
                    FileCount = 0
                    FreedBytes = 0
                }
            }

            $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue)
            $freedBytes = ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
            if ($null -eq $freedBytes) {
                $freedBytes = 0
            }

            Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

            return [pscustomobject]@{
                Exists = $true
                FileCount = $files.Count
                FreedBytes = [int64]$freedBytes
            }
        }
    }
}



$script:MenuTitle = "System Maintenance Tool"
$script:MenuSubtitle = "Run tasks by number (or 'q' to quit)"
$script:MenuGroupOrder = @('Quick Cleanup', 'Deep Cleanup', 'Optimization', 'Repair', 'Software', 'Network', 'Privacy', 'Info', 'Exit')
$script:MenuItemsRaw = @(
    [pscustomobject]@{ Group = 'Quick Cleanup'; Label = 'Clean temporary files'; Action = { Clean-TempFiles } }
    [pscustomobject]@{ Group = 'Quick Cleanup'; Label = 'Clear browser cache (Edge, Chrome)'; Action = { Clear-BrowserCache } }
    [pscustomobject]@{ Group = 'Quick Cleanup'; Label = 'Empty Recycle Bin'; Action = { Empty-RecycleBin } }
    [pscustomobject]@{ Group = 'Quick Cleanup'; Label = 'Clear clipboard contents'; Action = { Clear-Clipboard } }

    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Run Disk Cleanup (automatic)'; Action = { Run-DiskCleanupAuto } }
    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Run Disk Cleanup (GUI)'; Action = { Run-DiskCleanupGUI } }
    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Clean developer caches (NuGet, pip, npm, yarn)'; Action = { Clean-DeveloperCaches } }
    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Clear Windows Update cache'; Action = { Clear-WindowsUpdateCache } }
    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Clear Windows Event Logs'; Action = { Clear-EventLogs } }
    [pscustomobject]@{ Group = 'Deep Cleanup'; Label = 'Remove old Windows installation files'; Action = { Remove-OldWindowsFiles } }

    [pscustomobject]@{ Group = 'Optimization'; Label = 'Optimize drives (Trim/Defrag)'; Action = { Optimize-Drives } }
    [pscustomobject]@{ Group = 'Optimization'; Label = 'Disable Hibernation (reclaim GBs of space)'; Action = { Disable-Hibernation } }

    [pscustomobject]@{ Group = 'Repair'; Label = 'Repair system issues using DISM'; Action = { Run-DISMRepair } }
    [pscustomobject]@{ Group = 'Repair'; Label = 'Run System File Checker (sfc /scannow)'; Action = { Run-SFCScan } }
    [pscustomobject]@{ Group = 'Repair'; Label = 'Restart Windows Explorer'; Action = { Restart-Explorer } }

    [pscustomobject]@{ Group = 'Software'; Label = "Update software (winget upgrade --all)"; Action = { Run-WingetUpgrade } }
    [pscustomobject]@{ Group = 'Software'; Label = 'Quick compile this tool to EXE'; Action = { Compile-UtilitiesToExe } }

    [pscustomobject]@{ Group = 'Network'; Label = 'Flush DNS cache'; Action = { Flush-DNS } }

    [pscustomobject]@{ Group = 'Info'; Label = 'Check system uptime'; Action = { Show-Uptime } }

    [pscustomobject]@{ Group = 'Privacy'; Label = 'Clear PowerShell command history'; Action = { Clear-History } }
    [pscustomobject]@{ Group = 'Privacy'; Label = 'Clear Start/Taskbar recent list (Windows 11)'; Action = { Clear-StartTaskbarRecentList } }

    [pscustomobject]@{ Group = 'Exit'; Label = 'Exit'; Action = { } }
)


function Show-FullMenu {
    Write-Title -Text $script:MenuTitle -Subtitle $script:MenuSubtitle
    
    $flatItems = Get-OrderedMenuItems
    $i = 1
    foreach ($group in $script:MenuGroupOrder) {
        if ($group -eq 'Exit') { continue }
        
        $items = @($flatItems | Where-Object { $_.Group -eq $group })
        if ($items.Count -gt 0) {
            Write-Host " [$group]" -ForegroundColor Magenta
            foreach ($item in $items) {
                Write-Host (" {0}  {1}" -f $i.ToString().PadLeft(2), $item.Label)
                $i++
            }
            Write-Host ""
        }
    }
    Write-Host "  0  Exit" -ForegroundColor DarkGray
    Write-Rule
}

function Optimize-Drives {
    Invoke-Spinner -Message "Optimizing drives (Trim/Defrag)" -ScriptBlock {
        $drives = Get-Volume | Where-Object { $_.DriveLetter -ne $null -and $_.DriveType -eq 'Fixed' }
        foreach ($drive in $drives) {
            Write-Output "Optimizing $($drive.DriveLetter): ($($drive.FileSystemLabel))..."
            # -ReTrim for SSDs, -Defrag for HDDs (Optimize-Volume handles this automatically)
            Optimize-Volume -DriveLetter $drive.DriveLetter -ReTrim -Defrag -Verbose
        }
    }
    Pause-ForUser
}

function Disable-Hibernation {
    Invoke-Spinner -Message "Disabling Hibernation" -ScriptBlock {
        powercfg -h off
        Write-Output "Hibernation disabled and hiberfil.sys removed."
    }
    Pause-ForUser
}

function Clear-History {
    Invoke-Spinner -Message "Clearing PowerShell history" -ScriptBlock {
        $historyPath = "$env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt"
        if (Test-Path $historyPath) {
            Remove-Item $historyPath -Force
        }
    }
    Pause-ForUser
}

function Compile-UtilitiesToExe {
    Write-Host "Compiling this script to an EXE..." -ForegroundColor Cyan

    # When running as a compiled EXE, the original .ps1 path is not available.
    if ([string]::IsNullOrWhiteSpace($PSCommandPath) -or -not (Test-Path -LiteralPath $PSCommandPath)) {
        Write-Host "Source .ps1 path not found. Run this from the .ps1 file to compile." -ForegroundColor Yellow
        Pause-ForUser
        return
    }

    $defaultOut = [System.IO.Path]::ChangeExtension($PSCommandPath, '.exe')
    $out = (Read-Host "Output EXE path (Enter for default: $defaultOut)").Trim()
    if ([string]::IsNullOrWhiteSpace($out)) {
        $out = $defaultOut
    }

    $outDir = Split-Path -Path $out -Parent
    if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
        Write-Host "Output folder does not exist: $outDir" -ForegroundColor Yellow
        Pause-ForUser
        return
    }

    try {
        if (-not (Get-Module -ListAvailable -Name PS2EXE)) {
            Write-Host "Installing PS2EXE (CurrentUser)..." -ForegroundColor DarkGray
            Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
            Install-Module -Name PS2EXE -Scope CurrentUser -Force -AllowClobber
        }

        Import-Module PS2EXE -ErrorAction Stop
        Invoke-PS2EXE -InputFile $PSCommandPath -OutputFile $out -requireAdmin -ErrorAction Stop

        if (Test-Path -LiteralPath $out) {
            Write-Host "Built: $out" -ForegroundColor Green
        }
        else {
            Write-Host "Build completed but EXE was not found at: $out" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "Failed to compile to EXE." -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor DarkRed
    }

    Pause-ForUser
}

function Clear-StartTaskbarRecentList {
    Invoke-Spinner -Message "Clearing Start/Taskbar recent items" -ScriptBlock {
        $recentRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Recent'
        $targets = @(
            $recentRoot,
            (Join-Path $recentRoot 'AutomaticDestinations'),
            (Join-Path $recentRoot 'CustomDestinations')
        )

        foreach ($target in $targets) {
            if (Test-Path -LiteralPath $target) {
                try {
                    Remove-Item -LiteralPath (Join-Path $target '*') -Force -Recurse -ErrorAction SilentlyContinue
                }
                catch { }
            }
        }
    }
    
    # Optional Explorer restart
    while ($true) {
        $answer = (Read-Host "Restart Windows Explorer now to refresh UI? (Y/N)").Trim()
        if ($answer -match '^(y|yes)$') {
            Invoke-Spinner -Message "Restarting Windows Explorer" -ScriptBlock {
                Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
                Start-Process explorer.exe
            }
            break
        }
        if ($answer -match '^(n|no)$') {
            Write-Host "Tip: You can refresh later by signing out/in or restarting Explorer." -ForegroundColor DarkGray
            break
        }
        Write-Host "Please enter Y or N." -ForegroundColor Yellow
    }

    Pause-ForUser
}

function Run-WingetUpgrade {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-Host "winget is not available on this system." -ForegroundColor Yellow
        Write-Host "Install 'App Installer' from Microsoft Store, then try again." -ForegroundColor DarkGray
        Pause-ForUser
        return
    }

    Write-Host "Running 'winget upgrade --all'..." -ForegroundColor Cyan
    # Using Start-Process to prevent the progress bar from bugging out PowerShell's output
    Start-Process -FilePath "winget" -ArgumentList "upgrade --all" -Wait -NoNewWindow
    Pause-ForUser
}

function Run-DISMRepair {

    Invoke-Spinner -Message "Running DISM health check and repair" -ScriptBlock {
        dism /online /cleanup-image /scanhealth *>&1
        dism /online /cleanup-image /restorehealth *>&1
    }
    Pause-ForUser
}

function Run-DiskCleanupGUI {

    Invoke-Spinner -Message "Launching Disk Cleanup (GUI)" -ScriptBlock {
        Start-Process -FilePath "cleanmgr.exe" -ArgumentList "/d C:" -NoNewWindow -Wait
    }
    Pause-ForUser
}

function Run-DiskCleanupAuto {

    Invoke-Spinner -Message "Running automatic Disk Cleanup" -ScriptBlock {
        cleanmgr /sageset:1 | Out-Null
        cleanmgr /sagerun:1 | Out-Null
    }
    Pause-ForUser
}

function Clean-TempFiles {

    Invoke-Spinner -Message "Cleaning temporary files" -ScriptBlock {
        $freedBytes = 0
        $clearedPaths = 0

        $tempPaths = @(
            $env:TEMP,
            "$env:WINDIR\Temp"
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

        foreach ($path in $tempPaths) {
            $result = Clear-DirectoryContents -Path $path
            if ($result.Exists) {
                $clearedPaths++
                $freedBytes += $result.FreedBytes
            }
        }
        
        if ($freedBytes -gt 0) {
            Write-Output ("Cleared {0} temp locations, approx. freed {1:N2} MB." -f $clearedPaths, ($freedBytes / 1MB))
        }
        elseif ($clearedPaths -gt 0) {
            Write-Output "Temporary folders were checked and cleared where possible."
        }
        else {
            Write-Output "No temporary folders were found."
        }
    } -InitializationScript (Get-DirectoryCleanupInitializationScript)
    Pause-ForUser
}

function Clean-DeveloperCaches {
    Invoke-Spinner -Message "Cleaning developer caches (NuGet, pip, npm, yarn)" -ScriptBlock {
        $freedBytes = 0
        $clearedPaths = 0

        $targets = @(
            (Join-Path $env:USERPROFILE ".nuget\packages"),
            (Join-Path $env:LOCALAPPDATA "NuGet\v3-cache"),
            (Join-Path $env:LOCALAPPDATA "pip\cache"),
            (Join-Path $env:LOCALAPPDATA "npm-cache"),
            (Join-Path $env:LOCALAPPDATA "Yarn\Cache\v6")
        )

        foreach ($path in $targets) {
            $result = Clear-DirectoryContents -Path $path
            if ($result.Exists) {
                $clearedPaths++
                $freedBytes += $result.FreedBytes
            }
        }

        if ($freedBytes -gt 0) {
            Write-Output ("Cleared {0} developer cache locations, approx. freed {1:N2} MB." -f $clearedPaths, ($freedBytes / 1MB))
        }
        elseif ($clearedPaths -gt 0) {
            Write-Output "Developer cache locations were checked and cleared where possible."
        }
        else {
            Write-Output "No developer cache folders were found."
        }
    } -InitializationScript (Get-DirectoryCleanupInitializationScript)
    Pause-ForUser
}

function Clear-EventLogs {
    Invoke-Spinner -Message "Clearing Windows Event Logs" -ScriptBlock {
        try {
            # Clear all event logs
            Get-WinEvent -ListLog * -ErrorAction SilentlyContinue | Where-Object { $_.RecordCount -gt 0 } | ForEach-Object {
                [System.Diagnostics.Eventing.Reader.EventLogSession]::GlobalSession.ClearLog($_.LogName)
            }
        }
        catch {
            Write-Output "Make sure you are running as Administrator to clear all event logs."
        }
    }
    Pause-ForUser
}

function Run-SFCScan {

    Invoke-Spinner -Message "Running System File Checker" -ScriptBlock {
        sfc /scannow *>&1
    }
    Pause-ForUser
}

function Restart-Explorer {

    Invoke-Spinner -Message "Restarting Windows Explorer" -ScriptBlock {
        Stop-Process -Name explorer -Force
        Start-Process explorer.exe
    }
    Pause-ForUser
}

function Flush-DNS {

    Invoke-Spinner -Message "Flushing DNS cache" -ScriptBlock {
        ipconfig /flushdns *>&1
    }
    Pause-ForUser
}

function Show-Uptime {
    Invoke-Spinner -Message "Checking system uptime" -ScriptBlock {
        $uptime = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
        "`nSystem last booted at: $uptime"
    }
    Pause-ForUser
}

function Clear-WindowsUpdateCache {

    Invoke-Spinner -Message "Clearing Windows Update cache" -ScriptBlock {
        $servicesToStop = @('wuauserv', 'bits', 'dosvc')
        $stoppedServices = @()
        foreach ($svc in $servicesToStop) {
            try {
                $service = Get-Service -Name $svc -ErrorAction SilentlyContinue
                if ($service -and $service.Status -ne 'Stopped') {
                    Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
                    $stoppedServices += $svc
                }
            }
            catch {}
        }

        $freedBytes = 0
        $clearedPaths = 0

        $targets = @(
            "$env:WINDIR\SoftwareDistribution\Download",
            "$env:WINDIR\SoftwareDistribution\DeliveryOptimization",
            "$env:ProgramData\Microsoft\Windows\DeliveryOptimization\Cache"
        )

        foreach ($target in $targets) {
            $result = Clear-DirectoryContents -Path $target
            if ($result.Exists) {
                $clearedPaths++
                $freedBytes += $result.FreedBytes
            }
        }

        if ($freedBytes -gt 0) {
            Write-Output ("Cleared {0} Windows Update cache locations, approx. freed {1:N2} MB." -f $clearedPaths, ($freedBytes / 1MB))
        }
        elseif ($clearedPaths -gt 0) {
            Write-Output "Windows Update cache locations were checked and cleared where possible."
        }
        else {
            Write-Output "No Windows Update cache folders were found."
        }

        foreach ($svc in ($stoppedServices | Select-Object -Unique)) {
            try {
                Start-Service -Name $svc -ErrorAction SilentlyContinue
            }
            catch { }
        }
    } -InitializationScript (Get-DirectoryCleanupInitializationScript)
    Pause-ForUser
}

function Clear-BrowserCache {

    Invoke-Spinner -Message "Clearing browser cache" -ScriptBlock {
        $freedBytes = 0
        $clearedPaths = 0

        $browserRoots = @(
            @{ Name = 'Edge'; Root = "$env:LOCALAPPDATA\Microsoft\Edge\User Data" },
            @{ Name = 'Chrome'; Root = "$env:LOCALAPPDATA\Google\Chrome\User Data" }
        )

        $cacheRelativePaths = @(
            'Cache',
            'Code Cache',
            'GPUCache',
            'Service Worker\CacheStorage',
            'Service Worker\ScriptCache'
        )

        foreach ($browser in $browserRoots) {
            $root = $browser.Root
            if (-not (Test-Path -LiteralPath $root)) { continue }

            $profiles = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^(Default|Profile \d+)$' }
            
            if (-not $profiles) { continue }

            foreach ($profile in $profiles) {
                foreach ($rel in $cacheRelativePaths) {
                    $cachePath = Join-Path $profile.FullName $rel
                    $result = Clear-DirectoryContents -Path $cachePath
                    if ($result.Exists) {
                        $clearedPaths++
                        $freedBytes += $result.FreedBytes
                    }
                }
            }
        }
        
        if ($freedBytes -gt 0) {
            Write-Output ("Cleared {0} browser cache locations, approx. freed {1:N2} MB." -f $clearedPaths, ($freedBytes / 1MB))
        }
        elseif ($clearedPaths -gt 0) {
            Write-Output "Browser cache locations were checked and cleared where possible."
        }
        else {
            Write-Output "No browser cache folders were found."
        }
    } -InitializationScript (Get-DirectoryCleanupInitializationScript)
    Pause-ForUser
}

function Remove-OldWindowsFiles {

    Invoke-Spinner -Message "Removing old Windows files" -ScriptBlock {
        $windowsOld = "$env:SystemDrive\Windows.old"
        if (Test-Path -LiteralPath $windowsOld) {
            try {
                Remove-Item -LiteralPath $windowsOld -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Host "Note: Access denied or file in use; use Disk Cleanup for complete removal."
            }
        }
        else {
            Write-Host "No Windows.old found."
        }
    }
    Pause-ForUser
}

function Empty-RecycleBin {

    Invoke-Spinner -Message "Emptying Recycle Bin" -ScriptBlock {
        Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    }
    Pause-ForUser
}

function Clear-Clipboard {

    Invoke-Spinner -Message "Clearing clipboard" -ScriptBlock {
        try {
            cmd /c "echo off | clip" | Out-Null
        }
        catch {
            if (Get-Command Set-Clipboard -ErrorAction SilentlyContinue) {
                Set-Clipboard -Value "" -ErrorAction Stop
            }
        }
    }
    Pause-ForUser
}

# Main Loop
if ($Compile) {
    Compile-UtilitiesToExe
}
else {
    do {
        Show-FullMenu
        $choice = (Read-Host "Select tool").Trim()
        
        if ($choice -eq '0' -or $choice -match '^[qQ]$') {
            Write-Host "`nGoodbye!" -ForegroundColor DarkGray
            break
        }
        
        if ($choice -match '^\d+$') {
            $toolIndex = [int]$choice - 1
            $flatItems = Get-OrderedMenuItems
            
            if ($toolIndex -ge 0 -and $toolIndex -lt $flatItems.Count) {
                $action = $flatItems[$toolIndex].Action
                & $action
                continue
            }
        }
        
        Write-Host "Invalid selection." -ForegroundColor Yellow
        Start-Sleep -Seconds 1
        
    } while ($true)
}