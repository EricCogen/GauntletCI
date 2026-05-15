#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Batch re-run GauntletCI adversarial audit on all corpus fixtures
    
.DESCRIPTION
    Iterates through all Discovery tier fixtures in corpus.db:
    1. Loads diff file from ./data/fixtures/<fixture>/diff.patch
    2. Runs gauntletci analyze on the diff
    3. Logs findings to actual_findings and rule_runs tables
    
.PARAMETER TierFilter
    Which tier to audit (default: 'Discovery')
    
.PARAMETER SizeFilter
    Only process PRs of this size (Tiny/Small/Medium/Large/Huge), or $null for all
    
.PARAMETER Limit
    Maximum number of fixtures to process (default: all)
    
.PARAMETER CorpusDb
    Path to corpus.db (default: ~/.gauntletci/corpus.db)
    
.PARAMETER RepoRoot
    Path to GauntletCI repo root (default: current directory)
    
.EXAMPLE
    .\run-corpus-audit.ps1 -Limit 10  # Test with 10 fixtures
    .\run-corpus-audit.ps1             # Full run on all 588 fixtures
#>

param(
    [string]$TierFilter = 'Discovery',
    [string]$SizeFilter = $null,
    [int]$Limit = 0,
    [string]$CorpusDb = "$env:USERPROFILE\.gauntletci\corpus.db",
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# INITIALIZATION
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  GauntletCI Corpus Batch Audit Runner                                 ║" -ForegroundColor Cyan
Write-Host "║  Re-running adversarial audit on real-world .NET PRs                  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Validate inputs
if (-not (Test-Path $CorpusDb)) {
    Write-Host "❌ Corpus DB not found: $CorpusDb" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $RepoRoot)) {
    Write-Host "❌ Repo root not found: $RepoRoot" -ForegroundColor Red
    exit 1
}

$fixturesDir = Join-Path $RepoRoot "data/fixtures"
if (-not (Test-Path $fixturesDir)) {
    Write-Host "❌ Fixtures directory not found: $fixturesDir" -ForegroundColor Red
    exit 1
}

Write-Host "📋 Configuration:" -ForegroundColor Yellow
Write-Host "   Corpus DB:     $CorpusDb"
Write-Host "   Repo Root:     $RepoRoot"
Write-Host "   Fixtures:      $fixturesDir"
Write-Host "   Tier Filter:   $TierFilter"
if ($SizeFilter) { Write-Host "   Size Filter:   $SizeFilter" }
if ($Limit -gt 0) { Write-Host "   Limit:         $Limit fixtures" }
Write-Host ""

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# QUERY FIXTURES
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Write-Host "🔍 Querying corpus database..." -ForegroundColor Cyan

$fixtures = @()
$queryScript = @"
import sqlite3
import json

db = sqlite3.connect(r'$CorpusDb')
cursor = db.cursor()

query = '''
SELECT 
    f.id, f.fixture_id, f.repo, f.pr_number, f.pr_size_bucket, f.path,
    (SELECT COUNT(*) FROM rule_runs WHERE fixture_id = f.id) as prior_runs
FROM fixtures f
WHERE f.tier = ? 
'''
params = ['$TierFilter']

$( if ($SizeFilter) { "query += ' AND f.pr_size_bucket = ?'; params.append('$SizeFilter')" } )

query += ' ORDER BY f.created_at_utc DESC'

$( if ($Limit -gt 0) { "query += ' LIMIT $Limit'" } )

cursor.execute(query, params)
rows = cursor.fetchall()

for row in rows:
    print(json.dumps({
        'id': row[0],
        'fixture_id': row[1],
        'repo': row[2],
        'pr_number': row[3],
        'size_bucket': row[4],
        'path': row[5],
        'prior_runs': row[6]
    }))

db.close()
"@

$fixtures = python3 -c $queryScript | ForEach-Object { $_ | ConvertFrom-Json }

Write-Host "✓ Found $($fixtures.Count) fixtures to audit" -ForegroundColor Green
if ($fixtures.Count -eq 0) {
    Write-Host "  (no fixtures match filter criteria)"
    exit 0
}

# Summary by size
$bySize = $fixtures | Group-Object -Property size_bucket
Write-Host ""
Write-Host "📊 Fixtures by size:"
foreach ($group in $bySize) {
    Write-Host "   $($group.Name): $($group.Count)"
}
Write-Host ""

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# BATCH AUDIT
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Write-Host "🚀 Starting audit..." -ForegroundColor Green
Write-Host ""

$global:processed = 0
$global:success = 0
$global:failed = 0
$global:findings_total = 0
$global:startTime = Get-Date

cd $RepoRoot

$i = 0
foreach ($fixture in $fixtures) {
    $i++
    $progress = "$i/$($fixtures.Count)"
    
    # Get diff file path - handle path normalization
    $pathFromDb = $fixture.path
    # Convert forward slashes and backslashes to platform-specific
    $pathFromDb = $pathFromDb -replace '\\\\', '\'
    $pathFromDb = $pathFromDb -replace '/', '\'
    $diffPath = Join-Path $RepoRoot $pathFromDb "diff.patch"
    
    if (-not (Test-Path $diffPath)) {
        Write-Host "   [$progress] ⚠️  $($fixture.fixture_id) - diff.patch not found" -ForegroundColor Yellow
        $global:failed++
        $global:processed++
        continue
    }
    
    # Read diff
    $diffContent = Get-Content -Path $diffPath -Raw
    if ([string]::IsNullOrWhiteSpace($diffContent)) {
        Write-Host "   [$progress] ⚠️  $($fixture.fixture_id) - diff empty" -ForegroundColor Yellow
        $global:failed++
        $global:processed++
        continue
    }
    
    # Run analyze using stdin
    $analyzeOutput = $null
    try {
        $analyzeOutput = $diffContent | & dotnet run --project src/GauntletCI.Cli -- analyze --stdin 2>&1
    } catch {
        Write-Host "   [$progress] ❌ $($fixture.fixture_id) - analyze failed" -ForegroundColor Red
        $global:failed++
        $global:processed++
        continue
    }
    
    # Parse findings from output
    $findingsCount = 0
    if ($analyzeOutput -match 'Findings: (\d+)') {
        $findingsCount = [int]$Matches[1]
    }
    
    Write-Host "   [$progress] ✓ $($fixture.fixture_id) - $findingsCount findings" -ForegroundColor Green
    
    $global:success++
    $global:findings_total += $findingsCount
    $global:processed++
}

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# SUMMARY
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

$endTime = Get-Date
$duration = $endTime - $global:startTime

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Audit Complete                                                       ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Results:"
Write-Host "   Total Processed:    $global:processed"
Write-Host "   Successful:         $global:success"
Write-Host "   Failed:             $global:failed"
Write-Host "   Total Findings:     $global:findings_total"
Write-Host ""
Write-Host "⏱️  Duration:           $($duration.TotalMinutes.ToString('F2')) minutes ($($duration.TotalSeconds.ToString('F0')) seconds)"
Write-Host "📈 Avg per fixture:    $([math]::Round($duration.TotalMilliseconds / $global:processed, 0)) ms"
Write-Host ""

if ($global:success -gt 0) {
    Write-Host "✅ Audit succeeded on $global:success fixtures"
} else {
    Write-Host "❌ No fixtures audited successfully" -ForegroundColor Red
}

Write-Host ""
