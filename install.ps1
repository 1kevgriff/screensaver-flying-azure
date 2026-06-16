<#
.SYNOPSIS
    Builds and installs the Flying Azure screensaver, and (by default) sets it as
    the active Windows screensaver.

.DESCRIPTION
    Two install modes:

    * User scope (default) - copies FlyingAzure.scr to
      %LOCALAPPDATA%\FlyingAzure and points the current user's screensaver at it.
      No administrator rights required. The screensaver will NOT appear in the
      Windows "Screen Saver Settings" drop-down list (that list only scans the
      system folders), but it is fully active and its Settings/Preview buttons
      work via the registered path.

    * System scope (-System) - copies FlyingAzure.scr to %WINDIR%\System32 so it
      appears in the Screen Saver Settings drop-down. Requires an elevated
      (Administrator) PowerShell session.

.PARAMETER System
    Install into %WINDIR%\System32 (requires elevation). Makes the saver appear
    in the Windows screensaver drop-down list.

.PARAMETER TimeoutMinutes
    Idle minutes before the screensaver starts when activated. Default 10.

.PARAMETER NoActivate
    Install the .scr but do not change the current screensaver selection.

.PARAMETER NoBuild
    Skip 'dotnet build -c Release'; use the existing build output.

.EXAMPLE
    ./install.ps1
    Builds, installs to LOCALAPPDATA, activates with a 10-minute timeout.

.EXAMPLE
    ./install.ps1 -System
    (Run as Administrator) Builds, installs to System32, activates.
#>
[CmdletBinding()]
param(
    [switch]$System,
    [int]$TimeoutMinutes = 10,
    [switch]$NoActivate,
    [switch]$NoBuild,
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$rid = 'win-x64'
# The .scr is the single-file apphost from 'dotnet publish' (see CopyPublishToScr
# in FlyingAzure.csproj) — a standalone runnable file, unlike the managed .dll.
$publishDir = Join-Path $root "src\FlyingAzure\bin\Release\net10.0-windows\$rid\publish"
$buildScr = Join-Path $publishDir 'FlyingAzure.scr'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole(
        [Security.Principal.WindowsBuiltinRole]::Administrator)
}

# 0. Self-elevate for a System install ---------------------------------------
if ($System -and -not (Test-Admin)) {
    Write-Host 'A System32 install needs administrator rights — launching an elevated instance.' -ForegroundColor Yellow
    Write-Host 'Approve the UAC prompt when it appears...' -ForegroundColor Yellow
    $hostExe = (Get-Process -Id $PID).Path
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-System', '-TimeoutMinutes', $TimeoutMinutes)
    if ($NoActivate)    { $argv += '-NoActivate' }
    if ($NoBuild)       { $argv += '-NoBuild' }
    if ($SelfContained) { $argv += '-SelfContained' }
    Start-Process -FilePath $hostExe -Verb RunAs -ArgumentList $argv
    Write-Host 'Elevated installer launched. This window can be closed.' -ForegroundColor Cyan
    return
}

# 1. Publish single-file .scr (unless skipped) -------------------------------
if (-not $NoBuild) {
    $sc = if ($SelfContained) { 'true' } else { 'false' }
    Write-Host "Publishing single-file (Release, $rid, self-contained=$sc)..." -ForegroundColor Cyan
    dotnet publish (Join-Path $root 'src\FlyingAzure\FlyingAzure.csproj') `
        -c Release -r $rid -p:SelfContained=$sc `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}

if (-not (Test-Path $buildScr)) {
    throw "Screensaver not found at '$buildScr'. Run without -NoBuild to publish it first."
}

# 2. Resolve destination ------------------------------------------------------
if ($System) {
    $destDir = Join-Path $env:WINDIR 'System32'
} else {
    $destDir = Join-Path $env:LOCALAPPDATA 'FlyingAzure'
}

$dest = Join-Path $destDir 'FlyingAzure.scr'

# 3. Copy ---------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item -Path $buildScr -Destination $dest -Force
Write-Host "Installed: $dest" -ForegroundColor Green

# 4. Activate (unless skipped) ------------------------------------------------
if (-not $NoActivate) {
    $timeoutSeconds = [Math]::Max(60, $TimeoutMinutes * 60)
    $desktop = 'HKCU:\Control Panel\Desktop'
    Set-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE'      -Value $dest
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveActive'  -Value '1'
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveTimeOut' -Value "$timeoutSeconds"

    # Broadcast the change so it takes effect without a sign-out.
    if (-not ('FlyingAzureSpi' -as [type])) {
        Add-Type -Namespace '' -Name 'FlyingAzureSpi' -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, System.IntPtr pvParam, uint fWinIni);
'@
    }
    $SPI_SETSCREENSAVEACTIVE  = 0x0011
    $SPI_SETSCREENSAVETIMEOUT = 0x000F
    $SPIF_UPDATE_AND_SEND     = 0x0003   # UPDATEINIFILE | SENDCHANGE
    [FlyingAzureSpi]::SystemParametersInfo($SPI_SETSCREENSAVETIMEOUT, [uint32]$timeoutSeconds, [IntPtr]::Zero, $SPIF_UPDATE_AND_SEND) | Out-Null
    [FlyingAzureSpi]::SystemParametersInfo($SPI_SETSCREENSAVEACTIVE, 1, [IntPtr]::Zero, $SPIF_UPDATE_AND_SEND) | Out-Null

    Write-Host "Activated as the current screensaver ($TimeoutMinutes-minute idle timeout)." -ForegroundColor Green
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host "  Preview now:   `"$dest`" /s"
Write-Host "  Configure:     `"$dest`" /c"
if ($System) {
    Write-Host '  It also appears in: Settings > Personalization > Lock screen > Screen saver.'
}
$uninstallHint = if ($System) { './uninstall.ps1 -System  (run elevated)' } else { './uninstall.ps1' }
Write-Host "  Uninstall with: $uninstallHint"
