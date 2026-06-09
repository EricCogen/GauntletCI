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
    if ($env:GAUNTLETCI_HOOK_OPTIONAL -eq "1") { exit 0 }
    exit 1
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

    # Severity: 1=Info, 2=Warn, 3=Block
    $blockFindings = $result.Findings | Where-Object { $_.Severity -eq 3 }
    $warnFindings  = $result.Findings | Where-Object { $_.Severity -eq 2 }
    $infoFindings  = $result.Findings | Where-Object { $_.Severity -eq 1 }

    $total      = @($result.Findings).Count
    $blockCount = @($blockFindings).Count

    if ($blockCount -gt 0) {
        Write-Host ""
        Write-Host "GauntletCI found $blockCount Block-severity issue(s):" -ForegroundColor Red
        foreach ($f in $blockFindings) {
            Write-Host "  - [$($f.RuleId)] $($f.Summary)" -ForegroundColor Red
            Write-Host "    $($f.Evidence)" -ForegroundColor DarkRed
        }
        Write-Host ""
        Write-Host "Commit aborted. Fix Block-severity issues or use --no-verify to bypass." -ForegroundColor Red
        exit 1
    }
    elseif ($total -gt 0) {
        Write-Host ""
        Write-Host "GauntletCI found $total issue(s) (Warn/Info only):" -ForegroundColor Yellow
        foreach ($f in ($warnFindings + $infoFindings)) {
            $color = if ($f.Severity -eq 2) { "Yellow" } else { "Gray" }
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
