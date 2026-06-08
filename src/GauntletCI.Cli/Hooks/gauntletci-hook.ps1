# GauntletCI Pre-Commit Hook (PowerShell)
# Installed by `gauntletci init`

$ErrorActionPreference = "Stop"

function Get-DotNetToolsDirectories {
    $dirs = [System.Collections.Generic.List[string]]::new()
    if ($env:USERPROFILE) {
        $dirs.Add((Join-Path $env:USERPROFILE ".dotnet\tools"))
    }

    if ($env:HOME) {
        $dirs.Add((Join-Path $env:HOME ".dotnet/tools"))
    }

    if ($env:DOTNET_ROOT) {
        $dirs.Add((Join-Path $env:DOTNET_ROOT "tools"))
    }

    return $dirs | Select-Object -Unique
}

function Get-RepoCliProjectPath {
    $toplevel = $null
    try {
        $toplevel = git rev-parse --show-toplevel 2>$null
    }
    catch {
        $toplevel = $null
    }

    if ($toplevel) {
        $candidate = Join-Path $toplevel "src\GauntletCI.Cli\GauntletCI.Cli.csproj"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $current = (Get-Location).Path
    while (-not [string]::IsNullOrEmpty($current)) {
        $candidate = Join-Path $current "src\GauntletCI.Cli\GauntletCI.Cli.csproj"
        if (Test-Path $candidate) {
            return $candidate
        }

        $parent = Split-Path $current -Parent
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            break
        }

        $current = $parent
    }

    return $null
}

function Resolve-GauntletCiInvocation {
    $repoProject = Get-RepoCliProjectPath
    if ($null -ne $repoProject) {
        return @{
            Command = "dotnet"
            PrefixArgs = @("run", "--project", $repoProject, "--")
        }
    }

    if (Get-Command gauntletci -ErrorAction SilentlyContinue) {
        return @{
            Command = "gauntletci"
            PrefixArgs = @()
        }
    }

    foreach ($toolsDir in Get-DotNetToolsDirectories) {
        $toolExe = Join-Path $toolsDir "gauntletci.exe"
        if (Test-Path $toolExe) {
            return @{
                Command = $toolExe
                PrefixArgs = @()
            }
        }

        $toolShim = Join-Path $toolsDir "gauntletci"
        if (Test-Path $toolShim) {
            return @{
                Command = $toolShim
                PrefixArgs = @()
            }
        }
    }

    return $null
}

function Invoke-GauntletCi {
    param(
        [hashtable]$Invocation,
        [string[]]$CliArgs
    )

    if ($Invocation.PrefixArgs.Count -gt 0) {
        & $Invocation.Command @($Invocation.PrefixArgs + $CliArgs)
    }
    else {
        & $Invocation.Command @CliArgs
    }
}

$gauntletci = Resolve-GauntletCiInvocation
if ($null -eq $gauntletci) {
    Write-Host "GauntletCI not found. Install with: dotnet tool install -g GauntletCI" -ForegroundColor Yellow
    Write-Host "Or from this repo: ./scripts/install-gauntletci-global-tool.ps1" -ForegroundColor Yellow
    exit 0
}

Write-Host "GauntletCI: Analyzing staged changes..." -ForegroundColor Cyan

try {
    $output = Invoke-GauntletCi -Invocation $gauntletci -CliArgs @(
        "analyze", "--staged", "--output", "json", "--no-banner"
    ) 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GauntletCI failed to run. Commit aborted." -ForegroundColor Red
        Write-Host $output
        exit 1
    }

    $result = $output | ConvertFrom-Json

    # Confidence: 0=Low, 1=Medium, 2=High
    $highFindings   = $result.Findings | Where-Object { $_.Confidence -eq 2 }
    $mediumFindings = $result.Findings | Where-Object { $_.Confidence -eq 1 }
    $lowFindings    = $result.Findings | Where-Object { $_.Confidence -eq 0 }

    $total     = @($result.Findings).Count
    $highCount = @($highFindings).Count

    if ($highCount -gt 0) {
        Write-Host ""
        Write-Host "GauntletCI found $highCount high-confidence issue(s):" -ForegroundColor Red
        foreach ($f in $highFindings) {
            Write-Host "  - [$($f.RuleId)] $($f.Summary)" -ForegroundColor Red
            Write-Host "    $($f.Evidence)" -ForegroundColor DarkRed
        }
        Write-Host ""
        Write-Host "Commit aborted. Fix high-confidence issues or use --no-verify to bypass." -ForegroundColor Red
        exit 1
    }
    elseif ($total -gt 0) {
        Write-Host ""
        Write-Host "GauntletCI found $total issue(s) (none high-confidence):" -ForegroundColor Yellow
        foreach ($f in $result.Findings) {
            $color = if ($f.Confidence -eq 1) { "Yellow" } else { "Gray" }
            Write-Host "  - [$($f.RuleId)] $($f.Summary)" -ForegroundColor $color
        }
        Write-Host ""
        Write-Host "Commit allowed, but consider reviewing." -ForegroundColor Green
    }
    else {
        Write-Host "GauntletCI found no issues." -ForegroundColor Green
    }
}
catch {
    Write-Host "GauntletCI error: $_" -ForegroundColor Red
    exit 1
}
