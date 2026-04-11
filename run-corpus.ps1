# GauntletCI Corpus Pipeline Runner
# Usage: .\run-corpus.ps1 [-StartDate "2025-03-01"] [-Limit 50] [-Language "C#"] [-MinComments 2] [-Tier "discovery"] [-SkipTo <step>]

param(
    [string]$StartDate    = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd"),
    [int]   $Limit        = 50,
    [string]$Language     = "",
    [int]   $MinComments  = 2,
    [string]$Tier         = "discovery",
    [string]$Db           = "./data/corpus.db",
    [string]$Fixtures     = "./data/fixtures",
    [string]$Report       = "./data/scorecard.md",
    [int]   $SkipTo       = 1   # Set to 2-6 to resume from a specific step
)

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

    Invoke-Expression "$cli $($discoverArgs -join ' ')"
    if ($LASTEXITCODE -ne 0) { throw "Discover failed" }
}

# ── Step 2: Hydrate ───────────────────────────────────────────────────────────
if ($SkipTo -le 2) {
    Step 2 "Hydrate candidates via GitHub REST API"

    if (-not $env:GITHUB_TOKEN) {
        Write-Warning "GITHUB_TOKEN not set — unauthenticated rate limit is 60 req/hr. Set it for faster hydration."
    }

    Invoke-Expression "$cli corpus batch-hydrate --db $Db --fixtures $Fixtures --tier $Tier"
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

    Invoke-Expression "$cli corpus label-all --fixtures $Fixtures"
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
