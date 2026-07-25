#!/usr/bin/env pwsh
# Publishes MicBoost as a single self-contained Windows executable (no .NET runtime install required).
#
# Usage:
#   ./publish.ps1              -> publish/win-x64/MicBoost.App.exe
#   ./publish.ps1 -Install     -> also installs to %LocalAppData%\Programs\MicBoost
#                                 and adds a Start Menu shortcut (searchable via the Windows key)
#
# Once installed, later plain `./publish.ps1` runs refresh the installed copy
# automatically, so the Start Menu entry always points at the latest build.

[CmdletBinding()]
param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$outDir = Join-Path $root "publish/win-x64"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\MicBoost"
$installExe = Join-Path $installDir "MicBoost.exe"
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "MicBoost.lnk"

# Refresh an existing install automatically, so republishing never leaves a stale Start Menu app.
if (-not $Install -and (Test-Path $installExe)) {
    $Install = $true
    Write-Host "Existing install detected - it will be refreshed." -ForegroundColor DarkGray
}

dotnet publish (Join-Path $root "src/MicBoost.App/MicBoost.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $outDir

Write-Host ""
Write-Host "Published: $outDir\MicBoost.App.exe" -ForegroundColor Green

if (-not $Install) {
    Write-Host "Run './publish.ps1 -Install' to add it to the Start Menu." -ForegroundColor DarkGray
    return
}

# The installed exe can't be overwritten while it's running.
$running = Get-Process -Name "MicBoost" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $installExe }
if ($running) {
    Write-Host "Closing the running MicBoost to update it..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Process exit and the exe's file handle being released aren't the same instant, so a copy
# straight after Stop-Process still loses the race. Retry until Windows lets go of it.
$deadline = (Get-Date).AddSeconds(15)
while ($true) {
    try {
        Copy-Item (Join-Path $outDir "MicBoost.App.exe") $installExe -Force -ErrorAction Stop
        break
    }
    catch [System.IO.IOException] {
        if ((Get-Date) -ge $deadline) {
            throw "Couldn't replace $installExe - it's still in use. Close MicBoost and re-run."
        }
        Start-Sleep -Milliseconds 250
    }
}

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($shortcut)
$lnk.TargetPath = $installExe
$lnk.WorkingDirectory = $installDir
$lnk.IconLocation = "$installExe,0"
$lnk.Description = "System-wide microphone gain control"
$lnk.Save()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null

Write-Host "Installed:  $installExe" -ForegroundColor Green
Write-Host "Start Menu: $shortcut" -ForegroundColor Green
Write-Host "Press the Windows key and type 'MicBoost' to launch it." -ForegroundColor Green
