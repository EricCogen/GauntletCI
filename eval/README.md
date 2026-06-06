# Competitive evaluation artifacts

Public proof for GauntletCI vs **free tools that claim the same promise**: diff-scoped pull request review for behavioral and logic regression risk.

**Scope manifest:** [`competitor-scope.json`](competitor-scope.json) (authoritative list of comparable vs adjacent tools).  
**Positioning matrix:** [`competitive-matrix.json`](competitive-matrix.json) (subjective axes for comparable tools only).

## Who we compare (and who we do not)

| Include | Exclude (`adjacent_not_comparable`) |
|---------|-----------------------------------|
| Same claim: PR/diff review for bugs and behavioral risk | Paid-only clouds (e.g. CodeGuru) |
| Free tier for OSS or local CLI in the harness | Repo-wide quality-only (Codacy, DeepSource) |
| Harvest or API path documented | Suite assistants (Rovo, GitLab Duo), refactor bots (Sourcery) |

**Measured harness today**

| Segment | Tools |
|---------|--------|
| `anchor_only` | GauntletCI, CodeQL, CodeRabbit, Greptile, Qodo (eval-lab PR #7 harvest) |
| `gold_cross_repo` | GauntletCI vs CodeQL same-defect on `ci_required` defects |

LLM reviewers stay **anchor-only** until additional eval-lab mirrors exist.

**Gold expansion (full pipeline):**

```powershell
./scripts/run-gold-expansion.ps1
```

Produces gold fixtures, enriches **CodeQL + SonarCloud + Semgrep** on changed `.cs` paths, runs GauntletCI, scores `ci_required` recall by tool and `fn_class`, and writes [`eval/reports/gold-expansion.json`](reports/gold-expansion.json).

**Maximal cohort (~570 PRs with local diff):**

```powershell
./scripts/run-gold-scale-expansion.ps1 -SkipAnalyze -SkipPromote   # select + export only (fast)
./scripts/run-gold-scale-expansion.ps1                            # full pipeline (hours)
```

Corpus ceiling today: **~571** fixtures with `diff.patch` under `data/fixtures/discovery` (not 300+ labeled defects). Only **~59** have `expected_findings` suitable for scored recall until more are promoted.

| Gold static tool | Script |
|------------------|--------|
| CodeQL | `python scripts/enrich-benchmark-codeql.py --gold-only` |
| SonarCloud | `python scripts/enrich-benchmark-sonarcloud.py --gold-only` |
| Semgrep | `python scripts/enrich-benchmark-semgrep.py --gold-only` (requires `semgrep` on PATH) |

**Gold noise audit** (recall guard + sensitivity sweep):

```powershell
./scripts/run-gold-noise-audit.ps1
./scripts/run-gold-noise-audit.ps1 -FullSweep   # strict/balanced/permissive (slow)
```

Writes `eval/reports/gold-noise-sweep.json`. Benchmark runs default to `--sensitivity balanced`.

**Repair known harness limits** (weak gold, empty file/line, static enrich auth):

```powershell
./scripts/run-fix-gold-limits.ps1
```

| Token file | Env var | Used by |
|------------|---------|---------|
| `gh auth login` (preferred) | `GH_TOKEN` via `gh auth token` | CodeQL enrich, upstream PR harvest |
| `%USERPROFILE%\.tokens\cursor_security.token` | `GH_TOKEN` fallback | CodeQL (`security_events`; add `repo` if 403) |
| `%USERPROFILE%\.tokens\sonarcloud.token` | `SONAR_TOKEN` | SonarCloud enrich (`Bearer` on Sonar API) |

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
| `competitor-scope.json` | Comparable vs adjacent tools; harness segment tool lists |
| `benchmark-suite.json` | Cross-repo manifest (610-fixture pool) |
| `ground-truth/*.json` | Adjudicated defects per fixture |
| `competitor-runs/<fixture_id>/` | Harvested tool outputs |
| `scorecards/*.json` | Per-fixture + `competitive-suite.json` rollup |
| `competitive-matrix.json` | Subjective axis scores (comparable tools only) |
| `redis-2995-scorecard.json` | Legacy summary + anchor pointer |
| `../tests/GauntletCI.Benchmarks/Fixtures/curated/` | In-repo diff regressions (17 asserted); separate from Silver corpus |

CI runs anchor via `redis-benchmark.yml`; gold fixtures with `primary_rules` via `benchmark-suite.yml` (`-CiOnly` until ground truth expands). In-repo curated fixtures run via `dotnet test tests/GauntletCI.Benchmarks --filter Category=Benchmark`.

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
| `%USERPROFILE%\.tokens\cursor_security.token` | `security_events` (add `repo` for private repos) | CodeQL fallback after `gh auth token` |
| `%USERPROFILE%\.tokens\sonarcloud.token` | Sonar user token | SonarCloud project/issue API |

Enrich scripts prefer `gh auth token`, then env vars, then `~/.tokens` files. For Code Scanning: `gh auth refresh -h github.com -s repo,security_events`.

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
