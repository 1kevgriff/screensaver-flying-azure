<#
.SYNOPSIS
    Removes the Flying Azure screensaver and turns it off.

.PARAMETER System
    Also remove the copy from Windows\System32 (prompts for administrator rights).

.PARAMETER KeepSettings
    Leave your saved options (HKCU\Software\FlyingAzure) in place.
#>
[CmdletBinding()]
param(
    [switch]$System,
    [switch]$KeepSettings
)

$ErrorActionPreference = 'Stop'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole(
        [Security.Principal.WindowsBuiltinRole]::Administrator)
}

# Elevate if removing the System32 copy.
if ($System -and -not (Test-Admin)) {
    $hostExe = (Get-Process -Id $PID).Path
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-System')
    if ($KeepSettings) { $argv += '-KeepSettings' }
    Start-Process -FilePath $hostExe -Verb RunAs -ArgumentList $argv
    return
}

$userScr   = Join-Path $env:LOCALAPPDATA 'FlyingAzure\FlyingAzure.scr'
$systemScr = Join-Path $env:WINDIR 'System32\FlyingAzure.scr'
$targets   = @($userScr)
if ($System) { $targets += $systemScr }

# Turn it off if the current screensaver is one of ours.
$desktop = 'HKCU:\Control Panel\Desktop'
$current = (Get-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
if ($current -and ($targets -contains $current -or (Split-Path $current -Leaf) -ieq 'FlyingAzure.scr')) {
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveActive' -Value '0'
    Remove-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue
    if (-not ('FlyingAzureSpi' -as [type])) {
        Add-Type -Namespace '' -Name 'FlyingAzureSpi' -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, System.IntPtr pvParam, uint fWinIni);
'@
    }
    [FlyingAzureSpi]::SystemParametersInfo(0x0011, 0, [IntPtr]::Zero, 0x0003) | Out-Null
    Write-Host 'Turned off the Flying Azure screensaver.' -ForegroundColor Green
}

if ($System -and -not (Test-Admin) -and (Test-Path $systemScr)) {
    throw "Removing the System32 copy needs administrator rights. Re-run as Administrator."
}

foreach ($scr in $targets) {
    if (Test-Path $scr) {
        Remove-Item -Path $scr -Force
        Write-Host "Removed: $scr" -ForegroundColor Green
    }
}

$userDir = Split-Path $userScr -Parent
if ((Test-Path $userDir) -and -not (Get-ChildItem -Path $userDir -Force)) {
    Remove-Item -Path $userDir -Force
}

if (-not $KeepSettings) {
    $settingsKey = 'HKCU:\Software\FlyingAzure'
    if (Test-Path $settingsKey) {
        Remove-Item -Path $settingsKey -Recurse -Force
        Write-Host 'Removed saved options.' -ForegroundColor Green
    }
}

Write-Host ''
Write-Host 'Uninstall complete.' -ForegroundColor Green
