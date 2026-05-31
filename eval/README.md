# Competitive evaluation artifacts

Public proof for GauntletCI vs CodeQL, CodeRabbit, Greptile, and Qodo.

## Refresh workflow

1. **Anchor harvest** (eval lab PR #7):
   ```powershell
   ./scripts/harvest-competitor-evidence.ps1
   ```
2. **GauntletCI on Redis #2995**:
   ```powershell
   ./scripts/run-redis-benchmark.ps1
   ```
3. **Reselect corpus suite** (optional):
   ```powershell
   python scripts/select-benchmark-fixtures.py --write
   ./scripts/export-benchmark-diffs.ps1
   ```
   CI uses `-CiOnly` (fixtures with `ci_regression` and `primary_rules`); missing local corpus diffs are fetched from GitHub via `GH_TOKEN`.
4. **Score**:
   ```powershell
   python scripts/score-competitive-benchmark.py --fixture stackexchange-redis-pr-2995
   python scripts/score-competitive-benchmark.py --all-with-ground-truth
   ```

## Layout

| Path | Role |
|------|------|
| `benchmark-suite.json` | Cross-repo manifest (610-fixture pool) |
| `ground-truth/*.json` | Adjudicated defects per fixture |
| `competitor-runs/<fixture_id>/` | Harvested tool outputs |
| `scorecards/*.json` | Per-fixture + `competitive-suite.json` rollup |
| `redis-2995-scorecard.json` | Legacy summary + anchor pointer |

CI runs anchor via `redis-benchmark.yml`; gold fixtures with `primary_rules` via `benchmark-suite.yml` (`-CiOnly` until ground truth expands).

## Corpus promotion

```powershell
python scripts/promote-ground-truth-from-corpus.py
python scripts/score-competitive-benchmark.py --all-with-ground-truth
```

## CodeQL enrichment (corpus)

Requires GitHub token with **security_events** scope (Code Scanning API). Never commit token files or paste values into the repo.

### Token (local)

| File | Scopes | Used by |
|------|--------|---------|
| `%USERPROFILE%\.tokens\cursor_security.token` | `security_events` (add `repo` for private repos) | `enrich-benchmark-codeql.py`, `gh api` |

The benchmark script auto-loads that file into `GH_TOKEN` when `GH_TOKEN` / `GITHUB_TOKEN` are unset. Alternative: `gh auth refresh -h github.com -s repo,security_events` (device flow).

Verify auth (no secret output):

```powershell
gh auth status
gh api user -q .login
```

### Full corpus enrich (CLI)

```powershell
dotnet build GauntletCI.slnx -v quiet --nologo
dotnet run --project src/GauntletCI.Cli --no-build -- corpus codescanning enrich `
  --db "$env:USERPROFILE\.gauntletci\corpus.db" `
  --fixtures ./data/fixtures `
  --tier discovery
```

### Benchmark-only (54 fixtures)

```powershell
python scripts/enrich-benchmark-codeql.py
python scripts/score-competitive-benchmark.py --all-with-ground-truth
```

**Interpreting results:** `done processed=N fixtures_with_matches=0` means the API succeeded but no open CodeQL alert overlapped changed `.cs` paths in each diff — a valid measured outcome, not missing auth. Only **401/403** from `gh api user` indicates a bad or under-scoped token. Per-repo **404** on `code-scanning/alerts` often means scanning is disabled on that repo, not a scope error.
