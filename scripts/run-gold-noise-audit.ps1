#!/usr/bin/env pwsh
# Phase 1 noise audit: balanced gold runs + sensitivity sweep report.
param(
    [ValidateSet("strict", "balanced", "permissive")]
    [string]$GateSensitivity = "balanced",
    [switch]$FullSweep,
    [switch]$SkipBenchmark,
    [switch]$ReportOnly
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "=== Build ==="
dotnet build GauntletCI.slnx -v quiet --nologo

if (-not $SkipBenchmark) {
    Write-Host "=== Export gold diffs ==="
    ./scripts/export-benchmark-diffs.ps1 -GoldOnly

    Write-Host "=== Run gold benchmark at gate sensitivity: $GateSensitivity ==="
    ./scripts/run-benchmark-suite.ps1 -GoldOnly -Sensitivity $GateSensitivity
}

$sweepArgs = @("scripts/audit-gold-noise.py", "--use-main-runs")
if ($FullSweep) {
    $sweepArgs += "--sweep"
}
if ($ReportOnly) {
    $sweepArgs += "--report-only"
}

Write-Host "=== Gold noise audit ==="
python @sweepArgs

Write-Host "=== Refresh competitive report ==="
python scripts/score-competitive-benchmark.py --all-with-ground-truth --check
python scripts/generate-gold-expansion-report.py

Write-Host "Done. See eval/reports/gold-noise-sweep.json"
