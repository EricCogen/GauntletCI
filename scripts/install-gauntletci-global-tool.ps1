#!/usr/bin/env pwsh
# Pack, install, and verify the GauntletCI global dotnet tool.
param(
    [switch]$AddToPath,
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

$toolsDir = Join-Path $env:USERPROFILE ".dotnet\tools"
$nupkgDir = Join-Path $RepoRoot "nupkg-local"

Write-Host "Packing GauntletCI CLI..."
dotnet pack src/GauntletCI.Cli/GauntletCI.Cli.csproj -o $nupkgDir -v quiet --nologo

$installed = dotnet tool list -g 2>$null | Select-String -Pattern "\bgauntletci\b" -Quiet
if ($installed) {
    Write-Host "Uninstalling existing global gauntletci tool..."
    dotnet tool uninstall -g gauntletci | Out-Null
}

Write-Host "Installing global gauntletci tool from $nupkgDir..."
dotnet tool install -g GauntletCI --add-source $nupkgDir | Out-Null

$toolExe = Join-Path $toolsDir "gauntletci.exe"
if (-not (Test-Path $toolExe)) {
    throw "Expected tool shim at $toolExe after install."
}

$version = & $toolExe --version
Write-Host "Installed gauntletci $version at $toolExe"

if ($AddToPath) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrWhiteSpace($userPath)) {
        $userPath = $toolsDir
    }
    elseif ($userPath -notlike "*$toolsDir*") {
        $userPath = "$userPath;$toolsDir"
    }

    [Environment]::SetEnvironmentVariable("Path", $userPath, "User")
    if ($env:PATH -notlike "*$toolsDir*") {
        $env:PATH = "$env:PATH;$toolsDir"
    }

    Write-Host "Added $toolsDir to user PATH. Restart terminals to pick up the change."
}
else {
    Write-Host ""
    Write-Host "Note: $toolsDir is not on PATH in this shell."
    Write-Host "Run with -AddToPath to persist, or invoke directly:"
    Write-Host "  & '$toolExe' analyze --staged"
}
