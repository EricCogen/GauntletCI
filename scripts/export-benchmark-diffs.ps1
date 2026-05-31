#!/usr/bin/env pwsh
param([string]$SuitePath = (Join-Path $PSScriptRoot "..\eval\benchmark-suite.json"))
$ErrorActionPreference = "Stop"
$suite = Get-Content $SuitePath -Raw | ConvertFrom-Json
$outDir = Join-Path (Split-Path $PSScriptRoot -Parent) "eval\diffs"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
foreach ($f in $suite.fixtures) {
    $fid = $f.fixture_id
    $dest = Join-Path $outDir "$fid.patch"
    if (Test-Path $dest) { continue }
    $cp = $f.corpus_path
    if ($cp -and (Test-Path (Join-Path $cp "diff.patch"))) {
        Copy-Item (Join-Path $cp "diff.patch") $dest
        Write-Host "Exported $fid"
    }
}