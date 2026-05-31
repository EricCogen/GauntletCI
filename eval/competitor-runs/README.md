# Competitor run artifacts

Harvested JSON per tool under `<fixture_id>/`. File names match [`competitor-scope.json`](../competitor-scope.json) `harvest_artifact` fields.

| File | Tool |
|------|------|
| `greptile.json` | Greptile |
| `coderabbit.json` | CodeRabbit |
| `qodo.json` | Qodo |
| `codeql.json` | CodeQL (code scanning / enrich) |
| `gauntletci.json` | GauntletCI (optional snapshot) |

Regenerate anchor: `./scripts/harvest-competitor-evidence.ps1`

Only tools in `harness_segments` are scored for recall. See scope manifest for planned artifacts (`sonarcloud.json`, `semgrep.json`).
