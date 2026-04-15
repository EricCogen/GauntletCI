<#
.SYNOPSIS
    GauntletCI deterministic dead code audit.
    Covers: Roslyn warnings, stale archived rule tests, public types in exe projects,
    unused Python imports, orphaned HTML templates, duplicate private methods.

.DESCRIPTION
    Run this anytime to get a fast, repeatable dead code report.
    For semantic analysis (cross-file symbol references, YAML auditing, etc.)
    ask Copilot to "run the dead code audit" — see AGENTS.md for the full prompt.

.EXAMPLE
    .\audit-dead-code.ps1
#>

$RepoRoot    = $PSScriptRoot
$Src         = Join-Path $RepoRoot "src"
$TestRules   = Join-Path $Src "GauntletCI.Tests\Rules"
$ArchiveDir  = Join-Path $Src "GauntletCI.Core\Rules\Implementations\Archive"
$LabelerApp  = Join-Path $Src "GauntletCI.Corpus\GauntletCI_Labeler_App"
$TemplateDir = Join-Path $LabelerApp "templates"

$findings = [System.Collections.Generic.List[PSCustomObject]]::new()

function Add-Finding([string]$severity, [string]$file, [string]$symbol, [string]$issue) {
    $findings.Add([PSCustomObject]@{
        Severity = $severity
        File     = $file.Replace("$RepoRoot\", "")
        Symbol   = $symbol
        Issue    = $issue
    })
}

function Write-Header([string]$text) {
    Write-Host "`n[ $text ]" -ForegroundColor Cyan
}

function Write-Ok([string]$text) {
    Write-Host "  ✅ $text" -ForegroundColor Green
}

Write-Host "`n══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  GauntletCI Dead Code Audit" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan

# ──────────────────────────────────────────────────────────────
# 1. Roslyn analyzer warnings
# ──────────────────────────────────────────────────────────────
Write-Header "1/6  Roslyn analyzer warnings"
$buildOut = dotnet build "$RepoRoot\GauntletCI.slnx" --nologo `
    /p:EnforceCodeStyleInBuild=true /p:TreatWarningsAsErrors=false 2>&1
$roslynWarnings = $buildOut | Where-Object { $_ -match ": warning (CS|IDE)" }
if ($roslynWarnings) {
    foreach ($w in $roslynWarnings) {
        $filePart = ($w -split '\(')[0].Trim()
        Add-Finding "MEDIUM" $filePart "Roslyn" $w.Trim()
    }
} else {
    Write-Ok "No Roslyn warnings (EnforceCodeStyleInBuild=true)"
}

# ──────────────────────────────────────────────────────────────
# 2. Test files for archived rules
# ──────────────────────────────────────────────────────────────
Write-Header "2/6  Stale test files for archived rules"
if (Test-Path $ArchiveDir) {
    $archivedIds = Get-ChildItem $ArchiveDir -Filter "GCI*.cs" |
        ForEach-Object { ($_.BaseName -split '_')[0] }
    $stale = @()
    foreach ($id in $archivedIds) {
        $tf = Join-Path $TestRules "${id}Tests.cs"
        if (Test-Path $tf) { $stale += $tf }
    }
    if ($stale) {
        foreach ($tf in $stale) { Add-Finding "HIGH" $tf (Split-Path $tf -Leaf) "Test file for archived rule" }
    } else {
        Write-Ok "No stale archived rule test files"
    }
} else {
    Write-Ok "No Archive/ directory found"
}

# ──────────────────────────────────────────────────────────────
# 3. Public types in exe-only projects (skip projects referenced by others)
# ──────────────────────────────────────────────────────────────
Write-Header "3/6  Public types in exe-only projects (skip projects referenced by others)"
$allCsprojFiles = Get-ChildItem $Src -Recurse -Filter "*.csproj"
$exeProjects = $allCsprojFiles |
    Where-Object { (Get-Content $_.FullName -Raw) -match "<OutputType>Exe</OutputType>" } |
    Where-Object {
        $projName = $_.BaseName
        $otherCsprojContents = $allCsprojFiles |
            Where-Object { $_.BaseName -ne $projName } |
            ForEach-Object { Get-Content $_.FullName -Raw }
        $referencedByOther = $otherCsprojContents | Where-Object { $_ -match [regex]::Escape($projName) }
        ($referencedByOther | Measure-Object).Count -eq 0
    }
$exeFindings = 0
foreach ($proj in $exeProjects) {
    Get-ChildItem $proj.DirectoryName -Recurse -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" } |
        ForEach-Object {
            $content = Get-Content $_.FullName -Raw
            $ms = [regex]::Matches($content,
                '(?m)^public\s+(class|struct|record|enum|interface)\s+(\w+)')
            foreach ($m in $ms) {
                Add-Finding "LOW" $_.FullName $m.Groups[2].Value `
                    "public type in exe-only project '$($proj.BaseName)' — use internal"
                $exeFindings++
            }
        }
}
if ($exeFindings -eq 0) { Write-Ok "No public types in exe-only projects" }

# ──────────────────────────────────────────────────────────────
# 4. Unused Python imports
# ──────────────────────────────────────────────────────────────
Write-Header "4/6  Unused Python imports"
$pyFindings = 0
if (Test-Path $LabelerApp) {
    $pyFiles = Get-ChildItem $LabelerApp -Filter "*.py" |
        Where-Object { $_.FullName -notmatch "\.venv" }
    foreach ($py in $pyFiles) {
        $content = Get-Content $py.FullName -Raw
        $importMatches = [regex]::Matches($content, '(?m)^import (\w+)')
        foreach ($m in $importMatches) {
            $mod = $m.Groups[1].Value
            $usages = [regex]::Matches($content, "\b$mod\b").Count
            if ($usages -le 1) {
                Add-Finding "LOW" $py.FullName "import $mod" "Imported but never used"
                $pyFindings++
            }
        }
    }
}
if ($pyFindings -eq 0) { Write-Ok "No unused Python imports detected" }

# ──────────────────────────────────────────────────────────────
# 5. Orphaned HTML templates
# ──────────────────────────────────────────────────────────────
Write-Header "5/6  Orphaned HTML templates"
$tmplFindings = 0
if (Test-Path $TemplateDir) {
    $appPyContent = Get-Content (Join-Path $LabelerApp "app.py") -Raw
    $allTemplates = Get-ChildItem $TemplateDir -Filter "*.html" |
        ForEach-Object { Get-Content $_.FullName -Raw } | Out-String
    foreach ($tmpl in (Get-ChildItem $TemplateDir -Filter "*.html")) {
        $name = $tmpl.Name
        # Search for the filename string anywhere in app.py (handles multiline render_template calls)
        $inApp  = $appPyContent -match [regex]::Escape($name)
        $inTmpl = $allTemplates -match [regex]::Escape($name)
        if (-not $inApp -and -not $inTmpl) {
            Add-Finding "HIGH" $tmpl.FullName $name "Template not rendered by app.py or included by any other template"
            $tmplFindings++
        }
    }
}
if ($tmplFindings -eq 0) { Write-Ok "All HTML templates are referenced" }

# ──────────────────────────────────────────────────────────────
# 6. Duplicate private methods (same name + identical body)
# ──────────────────────────────────────────────────────────────
Write-Header "6/6  Duplicate private methods"
$seen = @{}
$dupFindings = 0
Get-ChildItem $Src -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(obj|bin|Archive)\\" } |
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        # Match private static methods with simple single-level bodies
        $ms = [regex]::Matches($content,
            '(?s)private static \S+ (\w+)\s*\([^)]*\)\s*\{([^{}]+)\}')
        foreach ($m in $ms) {
            $name = $m.Groups[1].Value
            $body = ($m.Groups[2].Value -replace '\s+', ' ').Trim()
            if ($body.Length -lt 30) { continue }
            $key = "$name||$body"
            if ($seen.ContainsKey($key)) {
                Add-Finding "MEDIUM" $_.FullName $name `
                    "Duplicate private method — identical to one in $($seen[$key])"
                $dupFindings++
            } else {
                $seen[$key] = $_.Name
            }
        }
    }
if ($dupFindings -eq 0) { Write-Ok "No duplicate private methods detected" }

# ──────────────────────────────────────────────────────────────
# Report
# ──────────────────────────────────────────────────────────────
Write-Host "`n══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan

if ($findings.Count -eq 0) {
    Write-Host "`n✅ No dead code found!" -ForegroundColor Green
} else {
    $highs   = @($findings | Where-Object Severity -eq "HIGH")
    $mediums = @($findings | Where-Object Severity -eq "MEDIUM")
    $lows    = @($findings | Where-Object Severity -eq "LOW")

    if ($highs)   { Write-Host "`n🔴 HIGH ($($highs.Count))"   -ForegroundColor Red;      $highs   | Format-Table Severity,File,Symbol,Issue -Wrap }
    if ($mediums) { Write-Host "`n🟠 MEDIUM ($($mediums.Count))" -ForegroundColor DarkYellow; $mediums | Format-Table Severity,File,Symbol,Issue -Wrap }
    if ($lows)    { Write-Host "`n🟡 LOW ($($lows.Count))"     -ForegroundColor Yellow;   $lows    | Format-Table Severity,File,Symbol,Issue -Wrap }

    Write-Host "Total: $($findings.Count) finding(s) — $($highs.Count) HIGH / $($mediums.Count) MEDIUM / $($lows.Count) LOW`n"
}

Write-Host "⚡ For semantic analysis (public symbol cross-referencing, YAML auditing, etc.)," -ForegroundColor DarkGray
Write-Host "   ask Copilot: 'run the dead code audit'  (see AGENTS.md for full prompt)" -ForegroundColor DarkGray
