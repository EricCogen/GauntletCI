#!/usr/bin/env pwsh
# Runs GauntletCI against dotnet/efcore PR #38024 and writes eval/efcore-38024-latest.json
param(
    [string]$DiffPath = "$env:TEMP\efcore-38024-eval.diff",
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

if (-not (Test-Path $DiffPath)) {
    Write-Host "Fetching diff from GitHub..."
    $diffDir = Split-Path $DiffPath -Parent
    if ($diffDir) {
        New-Item -ItemType Directory -Force -Path $diffDir | Out-Null
    }

    $token = if ($env:GH_TOKEN) { $env:GH_TOKEN } elseif ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } else { $null }
    if ($token) {
        Invoke-WebRequest `
            -Uri "https://api.github.com/repos/dotnet/efcore/pulls/38024" `
            -Headers @{
                Authorization = "Bearer $token"
                Accept        = "application/vnd.github.diff"
            } `
            -OutFile $DiffPath
    }
    else {
        gh api repos/dotnet/efcore/pulls/38024 `
            --header "Accept: application/vnd.github.diff" `
            | Set-Content -Path $DiffPath -Encoding utf8
    }
}

if (-not (Test-Path $DiffPath)) {
    throw "Failed to fetch EF Core PR #38024 diff to $DiffPath"
}

$configDir = Join-Path $env:TEMP "gci-efcore-eval"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
@'
{
  "domain": { "profile": "library" },
  "output": { "delivery": { "enabled": true, "globalMaxFindings": 25 } },
  "provenance": { "enabled": true },
  "semantics": { "enabled": true }
}
'@ | Set-Content (Join-Path $configDir ".gauntletci.json") -Encoding utf8

dotnet build GauntletCI.slnx -v quiet --nologo | Out-Null
$out = Join-Path $RepoRoot "eval\efcore-38024-latest.json"
dotnet run --project src/GauntletCI.Cli --no-build -- analyze `
    --diff $DiffPath `
    --repo $configDir `
    --output $out `
    --sensitivity permissive `
    --no-banner | Out-Null

Write-Host "Wrote $out"
python -c @"
import json, pathlib, collections
p = pathlib.Path(r'$out')
d = json.loads(p.read_text(encoding='utf-8-sig'))
fs = d.get('Findings', [])
print('findings', len(fs))
counts = collections.Counter(f.get('RuleId') for f in fs)
for rule, n in sorted(counts.items()):
    print(rule, n)
g04 = [f for f in fs if f.get('RuleId') == 'GCI0004']
print('GCI0004', len(g04))
print('ground_truth_gci0004', bool(g04))
"@
