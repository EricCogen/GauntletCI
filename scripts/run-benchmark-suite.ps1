#!/usr/bin/env pwsh
param(
    [string]$SuitePath = (Join-Path $PSScriptRoot "..\eval\benchmark-suite.json"),
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [ValidateSet("strict", "balanced", "permissive")]
    [string]$Sensitivity = "balanced",
    [switch]$CiOnly,
    [switch]$GoldOnly,
    [switch]$AllowCapExceed
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot
$suite = Get-Content $SuitePath -Raw | ConvertFrom-Json
$fixtures = @($suite.fixtures)
if ($GoldOnly) {
    $fixtures = @($fixtures | Where-Object { $_.suite_tier -eq "gold" })
} elseif ($CiOnly) {
    $fixtures = @($fixtures | Where-Object { $_.ci_regression -eq $true -and $_.primary_rules -and $_.primary_rules.Count -gt 0 })
    if ($fixtures.Count -eq 0) {
        $fixtures = @($suite.fixtures | Where-Object { $_.fixture_id -eq "stackexchange-redis-pr-2995" })
    }
}

dotnet build GauntletCI.slnx -v quiet --nologo | Out-Null
$runsDir = Join-Path $RepoRoot "eval\runs\gauntletci"
New-Item -ItemType Directory -Force -Path $runsDir | Out-Null

$total = $fixtures.Count
$i = 0
foreach ($f in $fixtures) {
    $i++
    Write-Progress -Activity "GauntletCI benchmark" -Status $f.fixture_id -PercentComplete (($i / $total) * 100)
    $fid = $f.fixture_id
    $diff = Join-Path $RepoRoot "eval\diffs\$fid.patch"
    if (-not (Test-Path $diff)) {
        $cp = $f.corpus_path
        if ($cp -and (Test-Path (Join-Path $cp "diff.patch"))) {
            New-Item -ItemType Directory -Force -Path (Split-Path $diff) | Out-Null
            Copy-Item (Join-Path $cp "diff.patch") $diff -Force
        } elseif ($fid -eq "stackexchange-redis-pr-2995") {
            & (Join-Path $PSScriptRoot "run-redis-benchmark.ps1")
            Copy-Item (Join-Path $RepoRoot "eval\redis-2995-latest.json") (Join-Path $runsDir "$fid.json") -Force
            continue
        } else {
            Write-Warning "Skip $fid — no diff"
            continue
        }
    }

    $cfgDir = Join-Path $env:TEMP "gci-bench-$fid"
    New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
    $profile = if ($f.domain_profile) { $f.domain_profile } else { "library" }
    @"
{
  "domain": { "profile": "$profile" },
  "output": { "delivery": { "enabled": true, "globalMaxFindings": 25 } },
  "provenance": { "enabled": true }
}
"@ | Set-Content (Join-Path $cfgDir ".gauntletci.json") -Encoding utf8

    $out = Join-Path $runsDir "$fid.json"
    dotnet run --project src/GauntletCI.Cli --no-build -- analyze `
        --diff $diff `
        --repo $cfgDir `
        --output $out `
        --sensitivity $Sensitivity `
        --no-banner | Out-Null

    foreach ($rule in $f.primary_rules) {
        $doc = Get-Content $out -Raw | ConvertFrom-Json
        $delivered = @($doc.Findings | Where-Object { $_.RuleId -eq $rule })
        $fired = @($doc.RuleMetrics | Where-Object {
            $_.RuleId -eq $rule -and ($_.FindingCount -gt 0 -or $_.Outcome -eq 1)
        })
        if ($delivered.Count -lt 1 -and $fired.Count -lt 1) {
            throw "Fixture $fid missing required rule $rule (not delivered and rule did not fire)"
        }
    }
    $count = @((Get-Content $out -Raw | ConvertFrom-Json).Findings).Count
    if ($count -gt 25) {
        $capMsg = "Fixture $fid exceeded delivery cap: $count"
        if (-not $AllowCapExceed) { throw $capMsg }
        Write-Warning $capMsg
    }
    Write-Host "[$i/$total] OK $fid ($count findings)"
}
Write-Progress -Activity "GauntletCI benchmark" -Completed