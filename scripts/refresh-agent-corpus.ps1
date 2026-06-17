# Agent corpus refresh: discovery run-all, score, validation summary.
# Log: %USERPROFILE%\.gauntletci\corpus-run-all-YYYYMMDD-HHmmss.log
$ErrorActionPreference = "Continue"
$corpus = Join-Path $env:USERPROFILE ".gauntletci\corpus.db"
$log = Join-Path $env:USERPROFILE (".gauntletci\corpus-run-all-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
$repo = "C:\Users\ericc\source\repos\GauntletCI"
Set-Location $repo

function Write-Log([string]$Message) {
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $Message
    Write-Output $line
    Add-Content -Path $log -Value $line
}

Write-Log "Starting discovery run-all (606 fixtures)"
Write-Log "Corpus DB: $corpus"
Write-Log "Fixtures: $repo\data\fixtures"

dotnet run --project src/GauntletCI.Cli -- corpus run-all --tier discovery --db $corpus --fixtures ./data/fixtures 2>&1 |
    ForEach-Object { Write-Log $_ }

if ($LASTEXITCODE -ne 0) {
    Write-Log "run-all failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Log "Ensuring corpus read indexes"
python (Join-Path $repo "scripts\corpus_db_read.py") 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Applying corpus label overrides"
python (Join-Path $repo "scripts\apply-corpus-label-overrides.py") --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Starting corpus score"
dotnet run --project src/GauntletCI.Cli -- corpus score --db $corpus --fixtures ./data/fixtures 2>&1 |
    ForEach-Object { Write-Log $_ }

if ($LASTEXITCODE -ne 0) {
    Write-Log "score failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Log "Writing validation summary JSON"
python (Join-Path $repo "scripts\corpus-validation-summary.py") --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Writing audit snapshot"
dotnet run --project src/GauntletCI.Cli -- corpus audit-snapshot --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Regenerating eval/rule-audit.json"
python (Join-Path $repo "scripts\build-rule-audit.py") --full-corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Exporting benchmark discovery sweep JSON"
python (Join-Path $repo "scripts\export-benchmark-discovery-sweep.py") --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Checking benchmark discovery metrics vs corpus DB"
python (Join-Path $repo "scripts\corpus-benchmark-discovery-drift.py") --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Checking rule audit labeled metrics vs corpus DB"
python (Join-Path $repo "scripts\corpus-rule-audit-drift.py") --db $corpus 2>&1 |
    ForEach-Object { Write-Log $_ }

Write-Log "Corpus refresh complete"
