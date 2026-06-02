#!/usr/bin/env pwsh
# Repair gold benchmark limits: reconcile weak gold, refresh ground truth, re-enrich static tools.
param(
    [switch]$SkipAnalyze,
    [switch]$SkipSemgrep,
    [switch]$SkipUpstreamHarvest,
    [switch]$SkipReconcile
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "=== Export diffs (gold) ==="
./scripts/export-benchmark-diffs.ps1 -GoldOnly

Write-Host "=== Build ==="
dotnet build GauntletCI.slnx -v quiet --nologo

Write-Host "=== Promote ground truth (file/line backfill, ci_required) ==="
python scripts/promote-ground-truth-from-corpus.py --refresh-all

if (-not $SkipReconcile) {
    Write-Host "=== Reconcile weak gold (after promote) ==="
    python scripts/reconcile-gold-suite.py
    python scripts/promote-ground-truth-from-corpus.py --refresh-all
}

if (-not $SkipAnalyze) {
    Write-Host "=== Run GauntletCI on gold CI fixtures ==="
    ./scripts/run-benchmark-suite.ps1 -GoldOnly
}

Write-Host "=== Enrich static competitors ==="
python scripts/enrich-benchmark-codeql.py --gold-only
python scripts/enrich-benchmark-sonarcloud.py --gold-only
if (-not $SkipSemgrep) {
    python scripts/enrich-benchmark-semgrep.py --gold-only
}

if (-not $SkipUpstreamHarvest) {
    Write-Host "=== Harvest upstream PR bot comments (optional) ==="
    python scripts/harvest-upstream-pr-reviews.py --gold-only
}

Write-Host "=== Score + report ==="
python scripts/score-competitive-benchmark.py --all-with-ground-truth --check
python scripts/generate-gold-expansion-report.py

Write-Host "Done. See eval/reports/gold-expansion.json"
