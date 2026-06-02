#!/usr/bin/env pwsh
# Maximal gold cohort: all corpus fixtures with local diff (~570). Scored recall only where labels exist.
param(
    [int]$GoldMax = 0,
    [int]$PerRepoCap = 0,
    [int]$PromoteBatchSize = 0,
    [switch]$SkipExport,
    [switch]$SkipPromote,
    [switch]$SkipAnalyze,
    [switch]$SkipEnrich,
    [switch]$SkipNoiseAudit,
    [switch]$SkipReconcile
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "=== 1/7 Select maximal gold (all fixtures with diff) ==="
python scripts/select-benchmark-fixtures.py --write --gold-all-with-diff --gold-max $GoldMax --per-repo-cap $PerRepoCap

if (-not $SkipExport) {
    Write-Host "=== 2/7 Export gold diffs to eval/diffs ==="
    ./scripts/export-benchmark-diffs.ps1 -GoldOnly
}

Write-Host "=== 3/7 Build ==="
dotnet build GauntletCI.slnx -v quiet --nologo

if (-not $SkipPromote) {
    Write-Host "=== 4/7 Promote ground truth (scoring_eligible gold only; long on full cohort) ==="
    if ($PromoteBatchSize -gt 0) {
        Write-Warning "Full --refresh-all on large cohort; batch size hint=$PromoteBatchSize (promote runs all gold)"
    }
    python scripts/promote-ground-truth-from-corpus.py --refresh-all
    if (-not $SkipReconcile) {
        Write-Host "=== 4b/7 Reconcile weak gold (only when replacements exist) ==="
        python scripts/reconcile-gold-suite.py
        python scripts/promote-ground-truth-from-corpus.py --refresh-all
    }
}

if (-not $SkipAnalyze) {
    Write-Host "=== 5/7 Run GauntletCI on all gold (hours for full cohort) ==="
    ./scripts/run-benchmark-suite.ps1 -GoldOnly -Sensitivity balanced
}

if (-not $SkipEnrich) {
    Write-Host "=== 6/7 Static enrich (gold) ==="
    python scripts/enrich-benchmark-codeql.py --gold-only
    python scripts/enrich-benchmark-sonarcloud.py --gold-only
    if (Get-Command semgrep -ErrorAction SilentlyContinue) {
        python scripts/enrich-benchmark-semgrep.py --gold-only
    }
}

if (-not $SkipNoiseAudit) {
    Write-Host "=== 7/7 Score + noise audit + report ==="
    python scripts/audit-gold-noise.py --use-main-runs --report-only
    python scripts/score-competitive-benchmark.py --all-with-ground-truth --check
    python scripts/generate-gold-expansion-report.py
}

Write-Host "Done. See eval/benchmark-suite.json selection_report and eval/reports/gold-expansion.json"
