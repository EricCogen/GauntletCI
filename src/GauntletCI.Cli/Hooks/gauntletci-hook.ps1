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

function Write-FindingsSummary {
    param($Result)

    if ($null -eq $Result.Findings -or @($Result.Findings).Count -eq 0) {
        Write-Host "GauntletCI found no issues." -ForegroundColor Green
        return
    }

    $blockFindings = $Result.Findings | Where-Object { $_.Severity -eq 3 }
    $warnFindings  = $Result.Findings | Where-Object { $_.Severity -eq 2 }
    $infoFindings  = $Result.Findings | Where-Object { $_.Severity -eq 1 }
    $total = @($Result.Findings).Count

    Write-Host ""
    Write-Host "GauntletCI found $total issue(s):" -ForegroundColor Yellow
    foreach ($f in ($blockFindings + $warnFindings + $infoFindings)) {
        $color = switch ($f.Severity) {
            3 { "Red" }
            2 { "Yellow" }
            default { "Gray" }
        }
        Write-Host "  - [$($f.RuleId)] $($f.Summary)" -ForegroundColor $color
        if ($f.Evidence) {
            Write-Host "    $($f.Evidence)" -ForegroundColor DarkGray
        }
    }

    if ($Result.Delivery) {
        $dropped = [int]$Result.Delivery.DroppedByGlobalCap + [int]$Result.Delivery.DroppedByPerRuleCap
        if ($dropped -gt 0) {
            Write-Host ""
            Write-Host "Note: $dropped finding(s) were dropped by delivery caps (see Delivery in JSON output)." -ForegroundColor DarkYellow
        }
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
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = Invoke-GauntletCi -Invocation $gauntletci -CliArgs @(
        "analyze", "--staged", "--output", "json", "--no-banner"
    ) 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $prevEap

    $jsonText = ($output | Where-Object { $_ -is [string] }) -join [Environment]::NewLine
    $result = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($jsonText)) {
            $result = $jsonText | ConvertFrom-Json
        }
    }
    catch {
        $result = $null
    }

    if ($exitCode -ne 0) {
        Write-Host ""
        Write-Host "Commit aborted. GauntletCI exited with code $exitCode (see .gauntletci.json exitOn and sensitivity)." -ForegroundColor Red
        if ($null -ne $result) {
            Write-FindingsSummary -Result $result
        }
        else {
            Write-Host $output
        }
        exit 1
    }

    if ($null -ne $result) {
        Write-FindingsSummary -Result $result
    }
    else {
        Write-Host $output
    }
}
catch {
    Write-Host "GauntletCI error: $_" -ForegroundColor Red
    exit 1
}
