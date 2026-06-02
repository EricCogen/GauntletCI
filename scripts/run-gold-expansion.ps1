#!/usr/bin/env pwsh
# Full gold benchmark expansion: select, export, promote, enrich static tools, run GauntletCI, score, report.
param(
    [int]$GoldMax = 0,
    [switch]$GoldAllWithDiff,
    [switch]$SkipAnalyze,
    [switch]$SkipSemgrep
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

$selectArgs = @("scripts/select-benchmark-fixtures.py", "--write", "--gold-max", $GoldMax)
if ($GoldAllWithDiff -or $GoldMax -eq 0) {
    $selectArgs += "--gold-all-with-diff", "--per-repo-cap", "0"
}
Write-Host "=== 1/9 Select gold fixtures ==="
python @selectArgs

Write-Host "=== 2/9 Export diffs (gold + CI) ==="
./scripts/export-benchmark-diffs.ps1 -CiOnly
./scripts/export-benchmark-diffs.ps1 -GoldOnly

Write-Host "=== 3/9 Build ==="
dotnet build GauntletCI.slnx -v quiet --nologo

Write-Host "=== 4/9 Promote ground truth + validate primary rules ==="
python scripts/promote-ground-truth-from-corpus.py --refresh-all

Write-Host "=== 5/9 Reconcile weak gold (still missing primary_rules) ==="
python scripts/reconcile-gold-suite.py
python scripts/promote-ground-truth-from-corpus.py --refresh-all

if (-not $SkipAnalyze) {
    Write-Host "=== 6/9 Run GauntletCI on gold CI fixtures ==="
    ./scripts/run-benchmark-suite.ps1 -CiOnly -Sensitivity balanced
    ./scripts/run-benchmark-suite.ps1 -GoldOnly -Sensitivity balanced
}

Write-Host "=== 6b/9 Gold noise audit (balanced + optional sweep) ==="
python scripts/audit-gold-noise.py --use-main-runs

Write-Host "=== 7/9 Enrich static competitors (gold) ==="
python scripts/enrich-benchmark-codeql.py --gold-only
python scripts/enrich-benchmark-sonarcloud.py --gold-only
if (-not $SkipSemgrep) {
    python scripts/enrich-benchmark-semgrep.py --gold-only
} else {
    Write-Warning "Skipping Semgrep (--SkipSemgrep)"
}

Write-Host "=== 8/9 Score + check ==="
python scripts/score-competitive-benchmark.py --all-with-ground-truth --check

Write-Host "=== 9/9 Gold expansion report ==="
python scripts/generate-gold-expansion-report.py

Write-Host "Done. See eval/reports/gold-expansion.json and eval/scorecards/competitive-suite.json"
