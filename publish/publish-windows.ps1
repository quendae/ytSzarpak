#!/usr/bin/env pwsh

<#
.SYNOPSIS
Publishes YTSzarpak as a self-contained single-file executable for Windows (x64).

.DESCRIPTION
This script builds a release binary for Windows x64 platform with ReadyToRun
and self-extraction support for maximum portability.
#>

param()

# Get the directory where this script is located
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "src\YtDlpGui.App\YtDlpGui.App.csproj"
$outputDir = Join-Path $scriptDir "output\win-x64"

Write-Host "Publishing YTSzarpak for Windows x64..." -ForegroundColor Cyan

# Create output directory if it doesn't exist
$null = New-Item -ItemType Directory -Path $outputDir -Force

# Run dotnet publish
$publishArgs = @(
    $projectPath,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $outputDir
)

& dotnet publish @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Find and report the executable
$exe = Get-ChildItem -Path $outputDir -Name "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($exe) {
    $exePath = Join-Path $outputDir $exe
    Write-Host "Successfully published to: $exePath" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Publish completed but executable not found in $outputDir" -ForegroundColor Red
    exit 1
}
