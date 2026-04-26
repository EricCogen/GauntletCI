# GauntletCI Pre-Commit Hook (PowerShell)
# Installed by `gauntletci init`

$ErrorActionPreference = "Stop"

# Resolve the gauntletci binary
$gauntletciCmd = $null
if (Get-Command gauntletci -ErrorAction SilentlyContinue) {
    $gauntletciCmd = "gauntletci"
} elseif (dotnet tool list -g 2>$null | Select-String -Quiet "gauntletci") {
    $gauntletciCmd = "dotnet gauntletci"
} else {
    Write-Host "⚠️  GauntletCI not found in PATH or as a global dotnet tool. Skipping pre-commit check." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "--- Engineering Rules Checklist (docs/core-engineering-rules.md) ---" -ForegroundColor DarkCyan
Write-Host "  [ ] Scope     : change is limited to what was requested" -ForegroundColor DarkCyan
Write-Host "  [ ] Contracts : no silent breaking changes to public APIs or schemas" -ForegroundColor DarkCyan
Write-Host "  [ ] Failures  : no swallowed exceptions or silent failures" -ForegroundColor DarkCyan
Write-Host "  [ ] Tests     : public behavior changes have coverage" -ForegroundColor DarkCyan
Write-Host "  [ ] Risk      : every behavioral change classified (Critical/High/Medium/Low/Negligible)" -ForegroundColor DarkCyan
Write-Host "  [ ] Release   : treat this change as production-bound" -ForegroundColor DarkCyan
Write-Host "--------------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "🔍 GauntletCI: Analyzing staged changes..." -ForegroundColor Cyan

try {
$output = if ($gauntletciCmd -eq "gauntletci") {
        gauntletci analyze --staged --output json --no-banner 2>&1
    } else {
        dotnet gauntletci analyze --staged --output json --no-banner 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ GauntletCI failed to run. Commit aborted." -ForegroundColor Red
        Write-Host $output
        exit 1
    }

    $result = $output | ConvertFrom-Json

    # Confidence: 0=Low, 1=Medium, 2=High
    $highFindings   = $result.Findings | Where-Object { $_.Confidence -eq 2 }
    $mediumFindings = $result.Findings | Where-Object { $_.Confidence -eq 1 }
    $lowFindings    = $result.Findings | Where-Object { $_.Confidence -eq 0 }

    $total     = @($result.Findings).Count
    $highCount = @($highFindings).Count

    if ($highCount -gt 0) {
        Write-Host ""
        Write-Host "🚨 GauntletCI found $highCount high-confidence issue(s):" -ForegroundColor Red
        foreach ($f in $highFindings) {
            Write-Host "  • [$($f.RuleId)] $($f.Summary)" -ForegroundColor Red
            Write-Host "    $($f.Evidence)" -ForegroundColor DarkRed
        }
        Write-Host ""
        Write-Host "❌ Commit aborted. Fix high-confidence issues or use --no-verify to bypass." -ForegroundColor Red
        exit 1
    }
    elseif ($total -gt 0) {
        Write-Host ""
        Write-Host "⚠️  GauntletCI found $total issue(s) (none high-confidence):" -ForegroundColor Yellow
        foreach ($f in $result.Findings) {
            $color = if ($f.Confidence -eq 1) { "Yellow" } else { "Gray" }
            Write-Host "  • [$($f.RuleId)] $($f.Summary)" -ForegroundColor $color
        }
        Write-Host ""
        Write-Host "✅ Commit allowed, but consider reviewing." -ForegroundColor Green
    }
    else {
        Write-Host "✅ GauntletCI found no issues." -ForegroundColor Green
    }
}
catch {
    Write-Host "❌ GauntletCI error: $_" -ForegroundColor Red
    exit 1
}