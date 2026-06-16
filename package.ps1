<#
.SYNOPSIS
    Builds a self-contained, single-file FlyingAzure.scr (no .NET runtime required on
    the target machine) and zips it with the dist install/uninstall scripts + README
    into FlyingAzure-Screensaver.zip — ready to hand to anyone on 64-bit Windows.
#>
[CmdletBinding()]
param([string]$OutputZip)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$rid = 'win-x64'
$publishDir = Join-Path $root "src\FlyingAzure\bin\Release\net10.0-windows\$rid\publish"
if (-not $OutputZip) { $OutputZip = Join-Path $root 'FlyingAzure-Screensaver.zip' }

Write-Host 'Publishing self-contained single-file (.NET runtime bundled)...' -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\FlyingAzure\FlyingAzure.csproj') `
    -c Release -r $rid -p:SelfContained=true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$scr = Join-Path $publishDir 'FlyingAzure.scr'
if (-not (Test-Path $scr)) { throw "Published screensaver not found at '$scr'." }

$stage = Join-Path $root 'build\FlyingAzure-Screensaver'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item $scr (Join-Path $stage 'FlyingAzure.scr')
Copy-Item (Join-Path $root 'dist\install.ps1')   $stage
Copy-Item (Join-Path $root 'dist\uninstall.ps1') $stage
Copy-Item (Join-Path $root 'dist\README.txt')    $stage

if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $OutputZip

$mb = [math]::Round((Get-Item $OutputZip).Length / 1MB, 1)
Write-Host ''
Write-Host "Created $OutputZip ($mb MB)" -ForegroundColor Green
Write-Host 'Contents: FlyingAzure.scr, install.ps1, uninstall.ps1, README.txt'
