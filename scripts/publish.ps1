<#
.SYNOPSIS
    Publishes MemoDock.

.DESCRIPTION
    Supports two publish shapes:
      - Framework-dependent multi-file (default): requires the user to install
        the .NET 10 Desktop Runtime.
      - Self-contained single-file: no .NET preinstall needed; ReadyToRun is
        enabled for faster startup and Brotli compression shrinks the bundle to
        roughly 1/3 (native libraries load from memory). (Trimming is
        intentionally disabled because the tray icon uses WinForms, which is
        not trim-compatible.)

    The version number is read from Directory.Build.props in the repo root,
    so there is a single place to bump before tagging.

.EXAMPLE
    # Framework-dependent (default)
    .\scripts\publish.ps1

.EXAMPLE
    # Self-contained single-file (win-x64)
    .\scripts\publish.ps1 -SelfContained

.EXAMPLE
    # Self-contained single-file (win-arm64)
    .\scripts\publish.ps1 -SelfContained -Runtime win-arm64
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [switch]$SelfContained,

    [string]$OutputRoot = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot 'src\MemoDock\MemoDock.csproj'

# Prefer the in-repo local SDK (.dotnet-sdk); fall back to PATH.
$LocalDotnet = Join-Path $RepoRoot '.dotnet-sdk\dotnet.exe'
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } else { 'dotnet' }

# Read the version from Directory.Build.props to avoid duplication.
$VersionLine = Select-String -Path (Join-Path $RepoRoot 'Directory.Build.props') -Pattern '<Version>([^<]+)</Version>'
if ($null -eq $VersionLine) { throw 'Missing <Version> in Directory.Build.props.' }
$Version = $VersionLine.Matches[0].Groups[1].Value

if ($SelfContained) {
    # Self-contained publish needs the runtime packages from nuget.org
    # (the repo NuGet.Config intentionally clears remote sources).
    $Output = Join-Path $RepoRoot (Join-Path $OutputRoot "MemoDock-$Version-$Runtime")
    Write-Host "Publishing self-contained single-file: $Runtime" -ForegroundColor Cyan
    & $Dotnet publish $Project `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --source https://api.nuget.org/v3/index.json `
        --output $Output
}
else {
    $Output = Join-Path $RepoRoot (Join-Path $OutputRoot 'MemoDock')
    Write-Host 'Publishing framework-dependent (requires .NET 10 Desktop Runtime)' -ForegroundColor Cyan
    & $Dotnet publish $Project `
        --configuration $Configuration `
        --no-restore `
        --output $Output
}

if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host ''
Write-Host "Published to: $Output" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
