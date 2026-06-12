# Troubleshooting

Common issues and their fixes.

---

## Installation

### `dotnet tool install -g GauntletCI` fails

**Symptom:** `error NU1301: Unable to load the service index`

**Fix:** Add the GitHub Packages source, or confirm you're on .NET 8 SDK or later:

```bash
dotnet --version   # must be 8.x or later
```

---

### `gauntletci: command not found` after install

**Symptom:** The tool installs but the shell can't find it.

**Fix:** The .NET global tools directory is not on your `PATH`. Add it:

```bash
# Linux/macOS
export PATH="$HOME/.dotnet/tools:$PATH"

# Windows (PowerShell)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

Add the export to your shell profile (`.bashrc`, `.zshrc`, or PowerShell `$PROFILE`) to make it permanent.

From the GauntletCI repo you can pack, install, and add PATH in one step:

```powershell
./scripts/install-gauntletci-global-tool.ps1 -AddToPath
```

---

### Pre-commit hook fails with `dotnet gauntletci` not found

**Symptom:** `dotnet tool list -g` shows `gauntletci`, but commits fail with `Could not execute because the specified command or file was not found`.

**Cause:** GauntletCI is installed as a standalone global tool shim (`gauntletci.exe`), not as a `dotnet` subcommand. Older hooks incorrectly invoked `dotnet gauntletci`.

**Fix:**

1. Reinstall hooks: `gauntletci init --force` (or rerun init from a current build).
2. Ensure the shim exists: `%USERPROFILE%\.dotnet\tools\gauntletci.exe` (Windows) or `~/.dotnet/tools/gauntletci` (Unix).
3. Add `~/.dotnet/tools` to PATH, or run `./scripts/install-gauntletci-global-tool.ps1 -AddToPath`.

The hook resolves, in order: `gauntletci` on PATH, the dotnet tools directory shim, then `dotnet run --project src/GauntletCI.Cli` when working inside this repository.

---

## Analysis

### `No diff source provided`

**Symptom:** Running `gauntletci analyze` with no flags produces this error.

**Fix:** Provide a diff source:

```bash
gauntletci analyze --staged          # analyze git staged changes
gauntletci analyze --commit HEAD~1   # analyze a specific commit
gauntletci analyze --diff pr.diff    # analyze a diff file
```

---

### Analysis produces 0 findings on a large PR

**Possible causes:**

1. **All files were filtered as ineligible**: GauntletCI skips binary files, generated code, vendored dependencies, and lock files. Run with `--verbose` to see the file eligibility report.
2. **Rules are disabled in config**: check `.gauntletci.json` for `"enabled": false` entries.
3. **Diff was empty**: `git diff --staged` with nothing staged produces an empty diff.

---

### Too many findings / noise

**Fix:** Tune severity thresholds in `.gauntletci.json`:

```json
{
  "rules": {
    "GCI0004": { "severity": "High" },
    "GCI0001": { "enabled": false }
  },
  "minSeverity": "High"
}
```

Use `gauntletci ignore add GCI00XX` to add a one-time suppression for a specific rule.

---

## LLM enrichment

### `--with-llm` produces empty enrichment text

**Symptom:** Findings show no enrichment text despite the flag being set.

**Causes and fixes:**

| Cause | Fix |
|-------|-----|
| Model not downloaded | Run `gauntletci model download` first |
| CPU inference timeout | Inference on CPU takes 30-90 seconds per finding. The default timeout is 60 seconds: this is usually sufficient, but a heavily loaded machine may need longer. |
| DirectML not available | The model falls back to CPU automatically. GPU via DirectML greatly speeds up inference on Windows. |

---

### `gauntletci model download` is slow or stalls

The Phi-4 Mini ONNX model is ~2.6 GB. Download speed depends on your connection. The model is cached at `~/.gauntletci/models/phi4-mini` after the first download.

If the download stalls, cancel and retry: the download is resumable.

---

### `--with-expert-context` returns no context

**Symptom:** Flag is set but no expert context appears in findings.

**Fix:** The vector store must be seeded first:

```bash
gauntletci llm seed
```

This writes 11 hand-curated .NET expert facts to `~/.gauntletci/expert-embeddings.db`. The Ollama embedding engine must be running:

```bash
ollama serve   # in a separate terminal
```

---

## Corpus pipeline

### `corpus discover` finds 0 candidates

**Possible causes:**

1. **No GitHub token configured**: set `GITHUB_TOKEN` environment variable or place a PAT in `.misc/ghapi.key`
2. **Rate limit hit**: the GitHub API has rate limits. Wait and retry, or use a token with higher limits.
3. **Network timeout**: retry with `--max-pages 1` to test connectivity.

---

### `corpus run-all` is slow

The corpus runner evaluates all rules against every fixture. With 500+ fixtures and 20 active rules, this can take 10-30 minutes. Run a subset:

```bash
gauntletci corpus run-all --limit 50
```

---

### Corpus DB is corrupted or needs reset

```bash
# Back up first
Copy-Item data/gauntletci-corpus.db data/gauntletci-corpus.db.bak

# Delete and reinitialize (data loss: be sure you have a backup)
Remove-Item data/gauntletci-corpus.db
gauntletci corpus discover   # recreates the DB with fresh schema
```

---

## Telemetry

### How do I opt out of telemetry?

```bash
gauntletci telemetry opt-out
```

This writes `"consent": "OptedOut"` to `~/.gauntletci/config.json`. No data is sent after opt-out.

### Where is telemetry stored locally?

`~/.gauntletci/telemetry-queue.json`: a bounded JSON queue (max 500 events). Inspect it at any time.

---

## GitHub Actions

### Action fails with `gauntletci: not found`

**Fix:** Ensure `dotnet-version` is set to `8.0.x` in the action input and the .NET tools path is on `PATH`. The composite action handles this automatically: verify you're using the action correctly:

```yaml
- uses: EricCogen/GauntletCI@v1
  with:
    commit: ${{ github.sha }}
```

### Exit code 1 blocks the PR

GauntletCI exits with code 1 when high-severity findings are present and `fail-on-findings: true` (the default). To make it advisory only:

```yaml
- uses: EricCogen/GauntletCI@v1
  with:
    fail-on-findings: false
```

---

## Licensing (offline / air-gapped)

### Network unreachable with a valid license

GauntletCI caches license status for 24 hours. If the network is down and no fresh cache exists, validation fails closed unless you qualify for enterprise air-gap mode.

**Enterprise air-gap (no network call):**

```bash
export GAUNTLETCI_ENTERPRISE_AIRGAP=1
export GAUNTLETCI_OFFLINE=1
export GAUNTLETCI_LICENSE="<enterprise-jwt>"
gauntletci license status --offline
```

Requirements (all must hold):

- License JWT tier is **Enterprise**
- `GAUNTLETCI_ENTERPRISE_AIRGAP=1` is set
- `GAUNTLETCI_OFFLINE=1` skips the remote status call entirely

**Stale cache grace:** when the network fails but a prior successful cache exists for the same token, GauntletCI reuses it for up to **72 hours**.

Non-enterprise tokens cannot use offline mode; fix network access or refresh cache while online.

### `corpus doctor` shows fewer fixtures than indexed

If SQLite lists fixtures with tier `unknown` (or other invalid values), they are indexed but not materialized. Run:

```bash
gauntletci corpus doctor --db ~/.gauntletci/corpus.db --fixtures ./data/fixtures --verbose
```

Invalid-tier fixture IDs print under `--verbose`. Re-hydrate or delete stale index rows, or set tier to `Discovery` before normalization.

### `corpus errors` fails with `no such column: logged_at`

Upgrade to a build that maps `pipeline_errors.recorded_at` and `message` (not `logged_at` / `error_message`). If you are on an older CLI, query directly:

```sql
SELECT recorded_at, step, provider, repo, error_code, message
FROM pipeline_errors ORDER BY recorded_at DESC LIMIT 20;
```

### `corpus fetch-diffs` for missing `diff.patch`

```bash
gauntletci corpus fetch-diffs --db ~/.gauntletci/corpus.db --fixtures ./data/fixtures --tier discovery --dry-run
gauntletci corpus fetch-diffs --db ~/.gauntletci/corpus.db --fixtures ./data/fixtures --tier discovery
```

HTTP 404 usually means the PR was deleted; remove the fixture row or re-point to a valid PR.

---

## Merge blocked / bypass required (GitHub rulesets)

The `main` branch ruleset requires **CodeQL** (`code_scanning`), **Copilot code review** (advisory; does not block merge), and these GitHub Actions status checks:

| Check context | Workflow job |
|---------------|--------------|
| `GauntletCI Self-Analysis` | `.github/workflows/ci.yml` `gauntletci-analyze` |
| `build-and-test (ubuntu-latest)` | `ci.yml` matrix |
| `build-and-test (windows-latest)` | `ci.yml` matrix |
| `benchmark-suite` | `benchmark-suite.yml` (path-filtered PRs only; **not** a required merge check) |

Canonical ruleset JSON: `.github/rulesets/main-update.json`. Re-apply after edits:

```bash
gh api --method PUT repos/EricCogen/GauntletCI/rulesets/14756937 --input .github/rulesets/main-update.json
```

Only checks that run on **every** PR are required (CI build/test and Self-Analysis). Path-filtered workflows such as `benchmark-suite` stay optional so doc-only PRs can merge.

Repository admins retain ruleset bypass (`bypass_mode: always`). Non-admin merges need all required checks green.

### All checks green but merge still shows "bypass required"

GitHub evaluates **rulesets separately from Actions job success**. A green `build-and-test` row does not by itself satisfy `code_scanning` or `required_status_checks`.

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `mergeStateStatus: BLOCKED`, CodeQL rollup `NEUTRAL` | Stale CodeQL category on `main` | See [CodeQL NEUTRAL](#codeql-status-check-is-neutral-merge-still-blocked) below |
| BLOCKED, no failing job, `strict_required_status_checks_policy: true` | Head commit behind `main` | Click **Update branch** on the PR (strict policy requires up-to-date head) |
| BLOCKED, admin user, all rules satisfied | Leftover `creation`/`update` rules, or API lag while checks finish | Fix ruleset first (see rows above). If `mergeStateStatus` is `CLEAN`, merge normally. Admins can use `gh pr merge N --squash --admin` only when a rule is intentionally unsatisfied. |
| BLOCKED, `statusCheckRollup.state: SUCCESS`, CodeQL `success` | `creation` / `update` ruleset entries fighting `pull_request` | **Remove** `creation` and `update` from `main-update.json`; `pull_request` already blocks direct pushes. Verified on PR #262 (`CLEAN` + merge without `--admin`). |
| BLOCKED, `statusCheckRollup.state: SUCCESS`, strict policy | `strict_required_status_checks_policy: true` | Use `false` in `main-update.json` (loose up-to-date) |
| BLOCKED, invisible rule | Former `code_quality` ruleset entry | Keep `code_quality` out of `main-update.json` until Code Quality is configured |

**Verify merge readiness** (replace `N` with PR number):

```bash
gh pr view N --json mergeStateStatus,mergeable,statusCheckRollup
gh api repos/EricCogen/GauntletCI/pulls/N/commits --jq '.[-1].sha' | ForEach-Object {
  gh api "repos/EricCogen/GauntletCI/commits/$_/check-runs" --jq '[.check_runs[] | {name,conclusion,status}]'
}
```

When every required context is `success` and CodeQL rollup is `success`, `mergeStateStatus` should become **`CLEAN`** within a few minutes. If it stays `BLOCKED` after 10+ minutes with no failing checks, inspect **Settings → Rules → Rulesets → main → Rule insights** on the PR.

### CodeQL status check is `NEUTRAL` (merge still blocked)

The rollup check `CodeQL` (app: `github-advanced-security`) goes **NEUTRAL** when `main` has a stale CodeQL **category** that the PR did not upload. Common after switching from a single CodeQL job to a language matrix.

**Symptoms:** Matrix jobs `CodeQL (csharp)` and `CodeQL (javascript-typescript)` pass, but the rollup stays `NEUTRAL` and the `code_scanning` ruleset blocks merge.

**Fix:**

1. Keep a **single** CodeQL workflow with a language matrix (do not add a second workflow). Let `github/codeql-action/analyze` use its default category (`.github/workflows/security.yml:codeql/language:…`). Do **not** set a custom `category` unless it matches what is already on `main`.

2. Delete obsolete analyses for category `.github/workflows/security.yml:codeql` on `main`:

   ```bash
   python scripts/cleanup-stale-codeql-analyses.py --ref refs/heads/main
   ```

3. Re-run Security workflow on the PR (push empty commit or re-run jobs).

**Verify:** `gh api repos/OWNER/REPO/code-scanning/analyses?ref=refs/heads/main --jq '[.[].category] | unique'` should list only `.github/workflows/security.yml:codeql/language:csharp` and `…/javascript-typescript`, not bare `…:codeql`.

### `code_quality` ruleset blocks merge with no visible check

The `code_quality` rule (GitHub Code Quality product) can block merges while no failing Actions check appears. This repo omits that rule from `.github/rulesets/main-update.json`. Re-enable only after Code Quality is configured and thresholds are understood.

To change required checks, edit the JSON and PUT again, or use GitHub: Settings → Rules → Rulesets → `main`.

---

## Still stuck?

Open an issue on GitHub with:
- GauntletCI version (`gauntletci --version`)
- OS and .NET SDK version (`dotnet --version`)
- The command you ran
- The full output (use `--verbose` if applicable)
