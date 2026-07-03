#!/usr/bin/env pwsh
# Publishes MicBoost as a single self-contained Windows executable (no .NET runtime install required).
# Usage: ./publish.ps1  ->  publish/win-x64/MicBoost.App.exe

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$outDir = Join-Path $root "publish/win-x64"

dotnet publish (Join-Path $root "src/MicBoost.App/MicBoost.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $outDir

Write-Host ""
Write-Host "Published: $outDir\MicBoost.App.exe" -ForegroundColor Green
