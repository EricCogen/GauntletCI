#!/usr/bin/env pwsh
# Full gold benchmark expansion: select, export, promote, enrich static tools, run GauntletCI, score, report.
param(
    [int]$GoldMax = 25,
    [switch]$SkipAnalyze,
    [switch]$SkipSemgrep
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "=== 1/8 Select gold fixtures (max $GoldMax) ==="
python scripts/select-benchmark-fixtures.py --write --gold-max $GoldMax

Write-Host "=== 2/8 Export diffs (gold + CI) ==="
./scripts/export-benchmark-diffs.ps1 -CiOnly
./scripts/export-benchmark-diffs.ps1 -GoldOnly

Write-Host "=== 3/8 Build ==="
dotnet build GauntletCI.slnx -v quiet --nologo

Write-Host "=== 4/8 Promote ground truth + validate primary rules ==="
python scripts/promote-ground-truth-from-corpus.py --refresh-all

if (-not $SkipAnalyze) {
    Write-Host "=== 5/8 Run GauntletCI on gold CI fixtures ==="
    ./scripts/run-benchmark-suite.ps1 -CiOnly
    ./scripts/run-benchmark-suite.ps1 -GoldOnly
}

Write-Host "=== 6/8 Enrich static competitors (gold) ==="
python scripts/enrich-benchmark-codeql.py --gold-only
python scripts/enrich-benchmark-sonarcloud.py --gold-only
if (-not $SkipSemgrep) {
    python scripts/enrich-benchmark-semgrep.py --gold-only
} else {
    Write-Warning "Skipping Semgrep (--SkipSemgrep)"
}

Write-Host "=== 7/8 Score + check ==="
python scripts/score-competitive-benchmark.py --all-with-ground-truth --check

Write-Host "=== 8/8 Gold expansion report ==="
python scripts/generate-gold-expansion-report.py

Write-Host "Done. See eval/reports/gold-expansion.json and eval/scorecards/competitive-suite.json"
