<#
.SYNOPSIS
    Compiles installer\FlyingAzure.iss into FlyingAzure-Setup-x64.exe (repo root).
    Installs Inno Setup via Chocolatey if ISCC.exe isn't present.
.PARAMETER Version
    Version stamped into the installer (shown in Add/Remove Programs). Default 0.0.0.
.NOTES
    Expects the staged screensaver at build\FlyingAzure-Screensaver\FlyingAzure.scr
    (produced by package.ps1). Run package.ps1 first.
#>
[CmdletBinding()]
param([string]$Version = '0.0.0')

$ErrorActionPreference = 'Stop'
$iss = Join-Path $PSScriptRoot 'FlyingAzure.iss'
$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'

if (-not (Test-Path $iscc)) {
    Write-Host 'Inno Setup not found — installing via Chocolatey...' -ForegroundColor Cyan
    choco install innosetup -y --no-progress | Out-Null
}
if (-not (Test-Path $iscc)) { throw "ISCC.exe not found at '$iscc' after install." }

& $iscc "/DAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }

Write-Host "Built FlyingAzure-Setup-x64.exe (version $Version)" -ForegroundColor Green
