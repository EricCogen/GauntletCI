# GauntletCI Corpus Pipeline Runner
# Usage: .\run-corpus.ps1 [-Help] [-StartDate "2025-03-01"] [-EndDate "2025-03-31"] [-Limit 50] [-Language "C#"] [-MinComments 2] [-Tier "discovery"] [-Db <path>] [-Fixtures <path>] [-Report <path>] [-SkipTo <step>]

param(
    [switch] $Help,
    [string]$StartDate    = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd"),
    [string]$EndDate      = "",   # End of date range (inclusive). Defaults to StartDate (single day) if not set.
    [int]   $Limit        = 50,
    [string]$Language     = "",
    [int]   $MinComments  = 0,
    [string]$Tier         = "discovery",
    [string]$Db           = "./data/gauntletci-corpus.db",
    [string]$Fixtures     = "./data/fixtures",
    [string]$Report       = "./data/scorecard.md",
    [int]   $SkipTo       = 1   # Set to 2-6 to resume from a specific step
)

if ($Help) {
    Write-Host ""
    Write-Host "GauntletCI Corpus Pipeline Runner" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "USAGE" -ForegroundColor Yellow
    Write-Host "  .\run-corpus.ps1 [options]"
    Write-Host ""
    Write-Host "OPTIONS" -ForegroundColor Yellow
    Write-Host "  -Help                  Show this help message"
    Write-Host "  -StartDate <date>      Start of date range to discover (default: yesterday, yyyy-MM-dd)"
    Write-Host "  -EndDate   <date>      End of date range, inclusive (default: same as StartDate)"
    Write-Host "  -Limit     <n>         Max candidates to discover AND hydrate per run (default: 50)"
    Write-Host "  -Language  <lang>      Filter by language, e.g. C#, Python (default: all)"
    Write-Host "  -MinComments <n>       Minimum review comment count (default: 0)"
    Write-Host "  -Tier      <tier>      Fixture tier: gold | silver | discovery (default: discovery)"
    Write-Host "  -Db        <path>      SQLite database path (default: ./data/gauntletci-corpus.db)"
    Write-Host "  -Fixtures  <path>      Fixtures root directory (default: ./data/fixtures)"
    Write-Host "  -Report    <path>      Scorecard output path (default: ./data/scorecard.md)"
    Write-Host "  -SkipTo    <step>      Resume from step N (1–6, default: 1)"
    Write-Host ""
    Write-Host "STEPS" -ForegroundColor Yellow
    Write-Host "  1  Discover   — pull PR candidates from GH Archive"
    Write-Host "  2  Hydrate    — fetch diffs + metadata from GitHub REST API"
    Write-Host "  3  Run rules  — evaluate all GCI rules against fixtures"
    Write-Host "  4  Label      — apply silver heuristic labels"
    Write-Host "  5  Score      — compute precision / recall aggregates"
    Write-Host "  6  Report     — write markdown scorecard to -Report path"
    Write-Host ""
    Write-Host "EXAMPLES" -ForegroundColor Yellow
    Write-Host "  # Full pipeline for yesterday"
    Write-Host "  .\run-corpus.ps1"
    Write-Host ""
    Write-Host "  # Specific date range, C# only, 100 PRs"
    Write-Host "  .\run-corpus.ps1 -StartDate 2025-03-01 -EndDate 2025-03-31 -Language C# -Limit 100"
    Write-Host ""
    Write-Host "  # Skip discovery and hydration — re-score existing fixtures"
    Write-Host "  .\run-corpus.ps1 -SkipTo 3"
    Write-Host ""
    Write-Host "NOTES" -ForegroundColor Yellow
    Write-Host "  Requires GITHUB_TOKEN env var for Step 2 (hydration)."
    Write-Host "  The database accumulates across runs — each run adds new candidates."
    Write-Host ""
    exit 0
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
Push-Location $RepoRoot

function Step($n, $label) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "  Step ${n}: $label" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
}

$cli = "dotnet run --project src/GauntletCI.Cli --no-build --"

# Ensure data dirs exist
New-Item -ItemType Directory -Force -Path (Split-Path $Db)   | Out-Null
New-Item -ItemType Directory -Force -Path $Fixtures           | Out-Null

# Build once up front
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build GauntletCI.slnx --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# ── Step 1: Discover ──────────────────────────────────────────────────────────
if ($SkipTo -le 1) {
    Step 1 "Discover PR candidates from GH Archive"

    $discoverArgs = @(
        "corpus", "discover",
        "--provider", "gh-archive",
        "--limit",      $Limit,
        "--start-date", $StartDate,
        "--min-comments", $MinComments,
        "--db",         $Db
    )
    if ($Language -ne "") { $discoverArgs += "--language", $Language }
    if ($EndDate  -ne "") { $discoverArgs += "--end-date", $EndDate }

    Invoke-Expression "$cli $($discoverArgs -join ' ')"
    if ($LASTEXITCODE -ne 0) { throw "Discover failed" }
}

# ── Step 2: Hydrate ───────────────────────────────────────────────────────────
if ($SkipTo -le 2) {
    Step 2 "Hydrate candidates via GitHub REST API"

    if (-not $env:GITHUB_TOKEN) {
        Write-Warning "GITHUB_TOKEN not set — unauthenticated rate limit is 60 req/hr. Set it for faster hydration."
    }

    Invoke-Expression "$cli corpus batch-hydrate --db $Db --fixtures $Fixtures --tier $Tier --limit $Limit"
    if ($LASTEXITCODE -ne 0) { throw "Hydrate failed" }
}

# ── Step 3: Run rules ─────────────────────────────────────────────────────────
if ($SkipTo -le 3) {
    Step 3 "Run GauntletCI rules against all fixtures"

    Invoke-Expression "$cli corpus run-all --db $Db --fixtures $Fixtures"
    if ($LASTEXITCODE -ne 0) { throw "Run-all failed" }
}

# ── Step 4: Label ─────────────────────────────────────────────────────────────
if ($SkipTo -le 4) {
    Step 4 "Apply silver heuristic labels"

    Invoke-Expression "$cli corpus label-all --db $Db --fixtures $Fixtures"
    if ($LASTEXITCODE -ne 0) { throw "Label-all failed" }
}

# ── Step 5: Score ─────────────────────────────────────────────────────────────
if ($SkipTo -le 5) {
    Step 5 "Compute precision / recall scorecard"

    Invoke-Expression "$cli corpus score --db $Db --fixtures $Fixtures"
    if ($LASTEXITCODE -ne 0) { throw "Score failed" }
}

# ── Step 6: Report ────────────────────────────────────────────────────────────
if ($SkipTo -le 6) {
    Step 6 "Generate markdown report → $Report"

    Invoke-Expression "$cli corpus report --db $Db --fixtures $Fixtures --output $Report"
    if ($LASTEXITCODE -ne 0) { throw "Report failed" }

    Write-Host ""
    Write-Host "✅ Pipeline complete. Scorecard: $Report" -ForegroundColor Green
    if (Test-Path $Report) { Get-Content $Report | Select-Object -First 30 }
}

Pop-Location
