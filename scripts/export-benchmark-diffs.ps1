#!/usr/bin/env pwsh
param(
    [string]$SuitePath = (Join-Path $PSScriptRoot "..\eval\benchmark-suite.json"),
    [switch]$CiOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$suite = Get-Content $SuitePath -Raw | ConvertFrom-Json
$fixtures = @($suite.fixtures)
if ($CiOnly) {
    $fixtures = @(
        $fixtures | Where-Object {
            $_.ci_regression -eq $true -and $_.primary_rules -and $_.primary_rules.Count -gt 0
        }
    )
    if ($fixtures.Count -eq 0) {
        $fixtures = @($suite.fixtures | Where-Object { $_.fixture_id -eq "stackexchange-redis-pr-2995" })
    }
}

$outDir = Join-Path $repoRoot "eval\diffs"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Get-GitHubToken {
    if ($env:GH_TOKEN) { return $env:GH_TOKEN }
    if ($env:GITHUB_TOKEN) { return $env:GITHUB_TOKEN }
    return $null
}

function Export-PrDiff {
    param(
        [string]$OwnerRepo,
        [int]$PrNumber,
        [string]$DestPath
    )
    if ($PrNumber -le 0) {
        throw "Invalid PR number for $OwnerRepo"
    }
    $token = Get-GitHubToken
    $uri = "https://api.github.com/repos/$OwnerRepo/pulls/$PrNumber"
    if ($token) {
        Invoke-WebRequest `
            -Uri $uri `
            -Headers @{
                Authorization = "Bearer $token"
                Accept        = "application/vnd.github.diff"
            } `
            -OutFile $DestPath
    }
    else {
        gh api "repos/$OwnerRepo/pulls/$PrNumber" `
            -H "Accept: application/vnd.github.diff" | Set-Content -Path $DestPath -Encoding utf8
    }
    if (-not (Test-Path $DestPath) -or (Get-Item $DestPath).Length -lt 10) {
        throw "Failed to fetch diff for $OwnerRepo PR #$PrNumber"
    }
}

foreach ($f in $fixtures) {
    $fid = $f.fixture_id
    $dest = Join-Path $outDir "$fid.patch"
    if (Test-Path $dest) {
        continue
    }

    $cp = $f.corpus_path
    if ($cp) {
        $local = Join-Path $repoRoot ($cp -replace '/', '\')
        $localPatch = Join-Path $local "diff.patch"
        if (Test-Path $localPatch) {
            Copy-Item $localPatch $dest
            Write-Host "Exported $fid (corpus)"
            continue
        }
    }

    $repo = $f.repo
    $pr = [int]$f.pr_number
    if ($repo -and $pr -gt 0) {
        Write-Host "Fetching $fid from GitHub ($repo PR #$pr)..."
        Export-PrDiff -OwnerRepo $repo -PrNumber $pr -DestPath $dest
        Write-Host "Exported $fid (GitHub)"
        continue
    }

    Write-Warning "Skip $fid — no local diff and no repo/pr_number"
}
