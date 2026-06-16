<#
.SYNOPSIS
    Installs the Flying Azure screensaver from this folder. No build tools needed —
    it uses the FlyingAzure.scr sitting next to this script.

.DESCRIPTION
    Default (no switches): installs for the current user (no admin) and turns it on
    with a 10-minute idle timeout.

    -System : also copies it into Windows\System32 so it shows up in the Windows
              "Screen saver" drop-down list. Prompts for administrator rights.

.EXAMPLE
    Right-click install.ps1  ->  Run with PowerShell      (per-user, easiest)

.EXAMPLE
    ./install.ps1 -System                                 (add to the Windows list; UAC)
#>
[CmdletBinding()]
param(
    [switch]$System,
    [int]$TimeoutMinutes = 10,
    [switch]$NoActivate
)

$ErrorActionPreference = 'Stop'
$scr = Join-Path $PSScriptRoot 'FlyingAzure.scr'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole(
        [Security.Principal.WindowsBuiltinRole]::Administrator)
}

# Elevate for a System install.
if ($System -and -not (Test-Admin)) {
    Write-Host 'Adding to the Windows screensaver list needs administrator rights — approve the prompt.' -ForegroundColor Yellow
    $hostExe = (Get-Process -Id $PID).Path
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-System', '-TimeoutMinutes', $TimeoutMinutes)
    if ($NoActivate) { $argv += '-NoActivate' }
    Start-Process -FilePath $hostExe -Verb RunAs -ArgumentList $argv
    return
}

if (-not (Test-Path $scr)) {
    throw "FlyingAzure.scr was not found next to this script. Keep install.ps1 and FlyingAzure.scr in the same folder."
}

$destDir = if ($System) { Join-Path $env:WINDIR 'System32' } else { Join-Path $env:LOCALAPPDATA 'FlyingAzure' }
$dest = Join-Path $destDir 'FlyingAzure.scr'

New-Item -ItemType Directory -Force -Path $destDir | Out-Null
# Clear any "downloaded from the internet" mark so Windows doesn't block it, then copy.
try { Unblock-File -Path $scr } catch { }
Copy-Item -Path $scr -Destination $dest -Force
Write-Host "Installed: $dest" -ForegroundColor Green

if (-not $NoActivate) {
    $timeoutSeconds = [Math]::Max(60, $TimeoutMinutes * 60)
    $desktop = 'HKCU:\Control Panel\Desktop'
    Set-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE'      -Value $dest
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveActive'  -Value '1'
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveTimeOut' -Value "$timeoutSeconds"

    if (-not ('FlyingAzureSpi' -as [type])) {
        Add-Type -Namespace '' -Name 'FlyingAzureSpi' -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, System.IntPtr pvParam, uint fWinIni);
'@
    }
    # Apply immediately without a sign-out.
    [FlyingAzureSpi]::SystemParametersInfo(0x000F, [uint32]$timeoutSeconds, [IntPtr]::Zero, 0x0003) | Out-Null # SPI_SETSCREENSAVETIMEOUT
    [FlyingAzureSpi]::SystemParametersInfo(0x0011, 1, [IntPtr]::Zero, 0x0003) | Out-Null                      # SPI_SETSCREENSAVEACTIVE
    Write-Host "Turned on as your screensaver ($TimeoutMinutes-minute idle)." -ForegroundColor Green
}

Write-Host ''
Write-Host 'Done! Preview it now with:' -ForegroundColor Green
Write-Host "  `"$dest`" /s"
Write-Host 'Configure (count, speed, colors, clock corner):'
Write-Host "  `"$dest`" /c"
if ($System) {
    Write-Host 'It also appears in Settings > Personalization > Lock screen > Screen saver.'
}
Write-Host 'To remove it later: run uninstall.ps1'
