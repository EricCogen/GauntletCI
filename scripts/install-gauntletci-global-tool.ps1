#!/usr/bin/env pwsh
# Pack, install, and verify the GauntletCI global dotnet tool.
param(
    [switch]$AddToPath,
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

function Get-GlobalDotNetToolsDirectory {
    $profileRoot = if ($IsWindows -or ($env:OS -match 'Windows')) {
        $env:USERPROFILE
    }
    else {
        $env:HOME
    }

    if ([string]::IsNullOrWhiteSpace($profileRoot)) {
        $profileRoot = $env:HOME ?? $env:USERPROFILE
    }

    if ([string]::IsNullOrWhiteSpace($profileRoot)) {
        throw "Cannot resolve the .NET global tools directory (HOME/USERPROFILE is unset)."
    }

    return Join-Path $profileRoot ".dotnet/tools"
}

$toolsDir = Get-GlobalDotNetToolsDirectory
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

$toolShim = Join-Path $toolsDir "gauntletci"
$toolExe = Join-Path $toolsDir "gauntletci.exe"
$launcher = if (Test-Path $toolExe) { $toolExe } elseif (Test-Path $toolShim) { $toolShim } else { $null }
if ($null -eq $launcher) {
    throw "Expected tool shim at $toolShim or $toolExe after install."
}

$version = & $launcher --version
Write-Host "Installed gauntletci $version at $launcher"

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
    Write-Host "  & '$launcher' analyze --staged"
}
