#!/usr/bin/env pwsh
# Quick corpus audit runner with 55 rules

param(
    [int]$Limit = 235
)

$corpusDb = "$env:USERPROFILE\.gauntletci\corpus.db"
$repoRoot = (Get-Location).Path
$fixturesDir = Join-Path $repoRoot "data/fixtures"
$cliPath = Join-Path $repoRoot "src/GauntletCI.Cli/bin/Release/net8.0/gauntletci.exe"

Write-Host "Starting corpus audit (55 rules, $Limit fixtures)..." -ForegroundColor Cyan

$fixtures = & python3 -c @"
import sqlite3
db = sqlite3.connect(r'$corpusDb')
cursor = db.cursor()
cursor.execute('''
SELECT fixture_id, repo, pr_number, path
FROM fixtures
WHERE tier = 'Discovery' AND path IS NOT NULL
LIMIT $Limit
''')
rows = cursor.fetchall()
db.close()
for row in rows:
    print(f"{row[0]}|{row[1]}|{row[2]}|{row[3]}")
"@

$total = ($fixtures | Measure-Object -Line).Lines
$count = 0
$findings = 0
$startTime = Get-Date

Write-Host "Found $total fixtures. Starting audit..." -ForegroundColor Yellow
Write-Host ""

foreach ($line in $fixtures) {
    $count++
    $parts = $line -split '\|'
    $fixtureId = $parts[0]
    $repo = $parts[1]
    $prNum = $parts[2]
    $relPath = $parts[3]
    
    $diffPath = Join-Path $repoRoot $relPath "diff.patch"
    
    if (Test-Path $diffPath) {
        try {
            $diff = Get-Content $diffPath -Raw -ErrorAction Stop
            $output = $diff | & $cliPath analyze --stdin 2>&1
            
            # Parse findings count
            if ($output -match "Findings:\s+(\d+)") {
                $f = [int]$matches[1]
                $findings += $f
                if ($f -gt 0) {
                    Write-Host "[$count/$total] ${fixtureId}: $f findings" -ForegroundColor Yellow
                } else {
                    if ($count % 50 -eq 0) {
                        Write-Host "[$count/$total] OK ${fixtureId}" -ForegroundColor Green
                    }
                }
            }
        }
        catch {
            Write-Host "[$count/$total] ERROR on $fixtureId : $_" -ForegroundColor Red
        }
    } else {
        Write-Host "[$count/$total] SKIP (no diff): $fixtureId" -ForegroundColor Gray
    }
}

$elapsed = ((Get-Date) - $startTime).TotalSeconds
$avgTime = $elapsed / $total

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════════"
Write-Host "Audit Complete" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════════"
Write-Host "Fixtures audited:  $total"
Write-Host "Total findings:    $findings"
Write-Host "Time elapsed:      $([int]$elapsed)s"
Write-Host "Avg per fixture:   $($avgTime.ToString('F2'))s"
Write-Host ""
