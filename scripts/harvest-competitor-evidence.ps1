#!/usr/bin/env pwsh
param(
    [string]$EvalRepo = "EricCogen/ai-review-eval-stackexchange-redis",
    [int]$PrNumber = 7,
    [string]$FixtureId = "stackexchange-redis-pr-2995",
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\eval\competitor-runs")
)

$ErrorActionPreference = "Stop"
$dest = Join-Path $OutRoot $FixtureId
New-Item -ItemType Directory -Force -Path $dest | Out-Null
$ts = (Get-Date).ToUniversalTime().ToString("o")

$prJson = gh pr view $PrNumber --repo $EvalRepo --json body,comments,title,headRefOid 2>&1
if ($LASTEXITCODE -ne 0) { throw $prJson }
$pr = $prJson | ConvertFrom-Json
$body = [string]$pr.body

# Greptile
$greptile = @{
    harvested_at_utc = $ts
    tool = "Greptile"
    finding_count = $null
    body_excerpt = $(if ($body -match '(?s)<!-- greptile_comment -->(.*?)<!-- /greptile_comment -->') { $Matches[1].Substring(0, [Math]::Min(8000, $Matches[1].Length)) } else { $body.Substring(0, [Math]::Min(4000, $body.Length)) })
    mentions_inverted_condition = ($body -match 'inverted')
}
$greptile | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dest "greptile.json") -Encoding utf8

# CodeRabbit
$coderabbit = @{
    harvested_at_utc = $ts
    tool = "CodeRabbit"
    body_excerpt = $(if ($body -match '(?s)release notes by coderabbit.ai(.*?)<!-- end of auto-generated') { $Matches[1].Substring(0, [Math]::Min(6000, $Matches[1].Length)) } else { "" })
    mentions_inverted_condition = ($body -match 'inverted|RemoveDisconnectedEndpoints')
}
$coderabbit | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dest "coderabbit.json") -Encoding utf8

# Inline comments
$commentsJson = gh api "repos/$EvalRepo/pulls/$PrNumber/comments" --paginate 2>&1
$inline = @()
if ($LASTEXITCODE -eq 0) {
    $commentsJson | ConvertFrom-Json | ForEach-Object { $inline += $_ }
}
$qodoText = ($inline | Where-Object { $_.user.login -match 'qodo' } | ForEach-Object { $_.body }) -join "`n"
$qodo = @{
    harvested_at_utc = $ts
    tool = "Qodo"
    inline_count = @($inline | Where-Object { $_.user.login -match 'qodo' }).Count
    body_excerpt = $qodoText.Substring(0, [Math]::Min(8000, [Math]::Max(0, $qodoText.Length)))
    mentions_inverted_condition = ($qodoText -match 'inverted|IsSubscriberConnected')
}
$qodo | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dest "qodo.json") -Encoding utf8

# CodeQL on eval lab PR
$sha = $pr.headRefOid
$alerts = @()
try {
    $scan = gh api "repos/$EvalRepo/code-scanning/alerts?ref=refs/pull/$PrNumber/head&per_page=100" 2>$null
    if ($scan) { $alerts = $scan | ConvertFrom-Json }
} catch {}
$codeql = @{
    harvested_at_utc = $ts
    tool = "CodeQL"
    ref = $sha
    finding_count = $alerts.Count
    code_scanning_alerts = @($alerts | Select-Object -First 50 | ForEach-Object {
        @{
            changed_file = $_.most_recent_instance.location.path
            message = $_.most_recent_instance.message.text
            codeql_rule = $_.rule.id
            codeql_rule_name = $_.rule.name
            severity = $_.rule.severity
        }
    })
}
$codeql | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $dest "codeql.json") -Encoding utf8

Write-Host "Harvested to $dest"