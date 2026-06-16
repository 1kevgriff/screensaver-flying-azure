<#
.SYNOPSIS
    Removes the Flying Azure screensaver and clears its activation.

.DESCRIPTION
    Removes FlyingAzure.scr from the per-user location (%LOCALAPPDATA%\FlyingAzure)
    and, with -System, from %WINDIR%\System32 (requires elevation). If the current
    user's screensaver points at a Flying Azure .scr, it is deactivated. Saved
    settings under HKCU\Software\FlyingAzure are removed unless -KeepSettings.

.PARAMETER System
    Also remove the copy from %WINDIR%\System32 (requires an elevated session).

.PARAMETER KeepSettings
    Leave the HKCU\Software\FlyingAzure configuration values in place.

.EXAMPLE
    ./uninstall.ps1
    Removes the per-user install and deactivates the screensaver.

.EXAMPLE
    ./uninstall.ps1 -System
    (Run as Administrator) Also removes the System32 copy.
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

$userScr   = Join-Path $env:LOCALAPPDATA 'FlyingAzure\FlyingAzure.scr'
$systemScr = Join-Path $env:WINDIR 'System32\FlyingAzure.scr'
$targets   = @($userScr)
if ($System) { $targets += $systemScr }

# 1. Deactivate if the current screensaver is one of ours ---------------------
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
    # SPI_SETSCREENSAVEACTIVE = 0x0011, uiParam 0 = disable; UPDATEINIFILE | SENDCHANGE
    [FlyingAzureSpi]::SystemParametersInfo(0x0011, 0, [IntPtr]::Zero, 0x0003) | Out-Null
    Write-Host 'Deactivated the current Flying Azure screensaver selection.' -ForegroundColor Green
}

# 2. Remove the .scr file(s) --------------------------------------------------
if ($System -and -not (Test-Admin) -and (Test-Path $systemScr)) {
    throw "-System requires an elevated session to remove '$systemScr'. Re-run as Administrator."
}

foreach ($scr in $targets) {
    if (Test-Path $scr) {
        Remove-Item -Path $scr -Force
        Write-Host "Removed: $scr" -ForegroundColor Green
    }
}

# Clean up the empty per-user folder if nothing else is in it.
$userDir = Split-Path $userScr -Parent
if ((Test-Path $userDir) -and -not (Get-ChildItem -Path $userDir -Force)) {
    Remove-Item -Path $userDir -Force
}

# 3. Remove saved settings (unless asked to keep) -----------------------------
if (-not $KeepSettings) {
    $settingsKey = 'HKCU:\Software\FlyingAzure'
    if (Test-Path $settingsKey) {
        Remove-Item -Path $settingsKey -Recurse -Force
        Write-Host 'Removed saved settings (HKCU\Software\FlyingAzure).' -ForegroundColor Green
    }
}

Write-Host ''
Write-Host 'Uninstall complete.' -ForegroundColor Green
