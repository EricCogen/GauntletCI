# GauntletCI Corpus Pipeline Runner
# Usage: .\run-corpus.ps1 [-Help] [-Provider "gh-search"] [-StartDate "2025-03-01"] [-EndDate "2025-03-31"] [-Limit 50] [-Language "C#"] [-MinComments 2] [-MinStars 500] [-Tier "discovery"] [-Db <path>] [-Fixtures <path>] [-Report <path>] [-SkipTo <step>]

param(
    [switch] $Help,
    [string]$Provider     = "gh-search",
    [string]$StartDate    = "",    # Start of date range (yyyy-MM-dd). Leave empty to search all-time.
    [string]$EndDate      = "",    # End of date range (inclusive). Leave empty for no upper bound.
    [int]   $Limit        = 50,
    [string]$Language     = "C#",
    [int]   $MinComments  = 5,
    [int]   $MinStars     = 1000,
    [string]$Tier         = "discovery",
    [string]$Db           = "./data/gauntletci-corpus.db",
    [string]$Fixtures     = "./data/fixtures",
    [string]$Report       = "./data/scorecard.md",
    [int]   $SkipTo       = 1,   # Set to 2-7 to resume from a specific step
    # Known game repositories and other low-signal repos to exclude from discovery.
    # Add owner/repo strings here to prevent them from entering the corpus.
    [string[]]$RepoBlocklist = @(
        "Goob-Station/Goob-Station",
        "corvax-team/ss14-wl",
        "Gaby-Station/Gaby-Station",
        "Simple-Station/Einstein-Engines",
        "zunath/SWLOR_NWN"
    ),
    # Curated allowlist from data/top-csharp-projects.md — high-quality OSS C# libraries
    # with active PR review culture. Discovery searches each repo directly; global search is disabled.
    [string[]]$RepoAllowlist = @(
        "AvaloniaUI/Avalonia",
        "App-vNext/Polly",
        "AutoMapper/AutoMapper",
        "Azure/azure-sdk-for-net",
        "AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet",
        "DapperLib/Dapper",
        "DevToys-app/DevToys",
        "FluentValidation/FluentValidation",
        "HangfireIO/Hangfire",
        "Humanizr/Humanizer",
        "IdentityServer/IdentityServer4",
        "JamesNK/Newtonsoft.Json",
        "JetBrains/Annotations",
        "MediatR/MediatR",
        "NLog/NLog",
        "PowerShell/PowerShell",
        "RicoSuter/NJsonSchema",
        "RicoSuter/Namotion.Reflection",
        "ShareX/ShareX",
        "SixLabors/ImageSharp",
        "StackExchange/StackExchange.Redis",
        "aaubry/YamlDotNet",
        "adamhathcock/sharpcompress",
        "apache/logging-log4net",
        "aws/aws-sdk-net",
        "bcgit/bc-csharp",
        "domaindrivendev/Swashbuckle.AspNetCore",
        "dotnet/aspnetcore",
        "dotnet/efcore",
        "dotnet/maui",
        "dotnet/orleans",
        "dotnet/reactive",
        "dotnet/roslyn",
        "dotnet/runtime",
        "files-community/Files",
        "fluentassertions/fluentassertions",
        "googleapis/google-api-dotnet-client",
        "grpc/grpc-dotnet",
        "icsharpcode/SharpZipLib",
        "jbogard/MediatR",
        "jellyfin/jellyfin",
        "joshclose/CsvHelper",
        "mgravell/Pipelines.Sockets.Unofficial",
        "microsoft/PowerToys",
        "mongodb/mongo-csharp-driver",
        "moodmosaic/Fare",
        "moq/moq4",
        "mono/t4",
        "npgsql/npgsql",
        "nunit/nunit",
        "open-telemetry/opentelemetry-dotnet",
        "rabbitmq/rabbitmq-dotnet-client",
        "restsharp/RestSharp",
        "serilog/serilog",
        "sshnet/SSH.NET",
        "xunit/xunit",
        "zzzprojects/html-agility-pack"
    )
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
    Write-Host "  -Provider  <name>      Discovery provider: gh-search (default) | gh-archive"
    Write-Host "                         gh-search requires GITHUB_TOKEN; gh-archive is unauthenticated"
    Write-Host "  -StartDate <date>      Start of date range to discover (default: empty = all-time, yyyy-MM-dd)"
    Write-Host "  -EndDate   <date>      End of date range, inclusive (default: empty = no upper bound)"
    Write-Host "  -Limit     <n>         Max candidates to discover AND hydrate per run (default: 50)"
    Write-Host "  -Language  <lang>      Filter by language, default C# (pass empty to include all)"
    Write-Host "  -MinComments <n>       Minimum review comment count (default: 5)"
    Write-Host "  -MinStars  <n>         Minimum repository star count (default: 1000)"
    Write-Host "                         NOTE: --min-stars has no effect with gh-archive (no star data in event stream)"
    Write-Host "  -Tier      <tier>      Fixture tier: gold | silver | discovery (default: discovery)"
    Write-Host "  -Db        <path>      SQLite database path (default: ./data/gauntletci-corpus.db)"
    Write-Host "  -Fixtures  <path>      Fixtures root directory (default: ./data/fixtures)"
    Write-Host "  -Report    <path>      Scorecard output path (default: ./data/scorecard.md)"
    Write-Host "  -RepoBlocklist <repos>  Repos to exclude (owner/repo, repeatable). Defaults to known game forks."
    Write-Host "  -RepoAllowlist <repos>  Curated repos to target (owner/repo, repeatable)."
    Write-Host "                         When set, searches each repo directly (no global keyword search)."
    Write-Host "                         Defaults to 15 high-quality C# OSS repos. Pass @() to disable."
    Write-Host "  -SkipTo    <step>      Resume from step N (1–7, default: 1)"
    Write-Host ""
    Write-Host "STEPS" -ForegroundColor Yellow
    Write-Host "  1  Discover   — pull PR candidates from GH Archive"
    Write-Host "  2  Hydrate    — fetch diffs + metadata from GitHub REST API"
    Write-Host "  2.5 Purge     — remove language-mismatched / no-review-comment fixtures"
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
    Write-Host "  .\run-corpus.ps1 -SkipTo 3"    Write-Host ""
    Write-Host "NOTES" -ForegroundColor Yellow
    Write-Host "  Requires GITHUB_TOKEN env var for Step 1 (gh-search discover) and Step 2 (hydration)."
    Write-Host "  If not set, the script will load it from .misc/ghapi.key automatically."
    Write-Host "  The database accumulates across runs — each run adds new candidates."
    Write-Host ""
    exit 0
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
Push-Location $RepoRoot

# Auto-load GITHUB_TOKEN from .misc/ghapi.key if not already set
if (-not $env:GITHUB_TOKEN) {
    $keyFile = Join-Path $RepoRoot ".misc\ghapi.key"
    if (Test-Path $keyFile) {
        $env:GITHUB_TOKEN = (Get-Content $keyFile -Raw).Trim()
        Write-Host "Loaded GITHUB_TOKEN from .misc/ghapi.key" -ForegroundColor DarkGray
    }
}

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
        "--provider",     $Provider,
        "--limit",        $Limit,
        "--min-comments", $MinComments,
        "--db",           $Db
    )
    if ($Language    -ne "") { $discoverArgs += "--language", $Language }
    if ($StartDate   -ne "") { $discoverArgs += "--start-date", $StartDate }
    if ($EndDate     -ne "") { $discoverArgs += "--end-date", $EndDate }
    # In allowlist mode, stars are irrelevant — we know the repos already
    if ($RepoAllowlist.Count -eq 0 -and $MinStars -gt 0) {
        $discoverArgs += "--min-stars", $MinStars
    }
    foreach ($allowed in $RepoAllowlist) {
        $discoverArgs += "--repo-allowlist", $allowed
    }
    foreach ($blocked in $RepoBlocklist) {
        $discoverArgs += "--repo-blocklist", $blocked
    }

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

# ── Step 2.5: Purge low-quality fixtures ─────────────────────────────────────
if ($SkipTo -le 2) {
    Step "2.5" "Purge language-mismatched and no-review-comment fixtures"

    $purgeArgs = @(
        "corpus", "purge",
        "--require-review-comments",
        "--db",       $Db,
        "--fixtures", $Fixtures
    )
    if ($Language -ne "") { $purgeArgs += "--language", $Language }
    foreach ($blocked in $RepoBlocklist) {
        $purgeArgs += "--repo-blocklist", $blocked
    }

    Invoke-Expression "$cli $($purgeArgs -join ' ')"
    if ($LASTEXITCODE -ne 0) { throw "Purge failed" }
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
