# Corpus Pipeline

The corpus pipeline is a six-step workflow that collects real-world pull requests, runs GauntletCI rules against them, applies heuristic labels, and produces per-rule precision/recall scorecards. The resulting fixtures and scores form the training signal for the **Moat** — GauntletCI's feedback loop that calibrates rule confidence over time.

```
discover → batch-hydrate → run-all → label-all → score → report
```

---

## Prerequisites

| Requirement | Detail |
|---|---|
| .NET 8 SDK | `dotnet --version` should report `8.x` |
| `GITHUB_TOKEN` | Required for `batch-hydrate` and the `gh-search` discover provider. `gh-archive` works unauthenticated but is rate-limited at 60 req/hr without a token. |
| Disk space | Allow ~1–5 MB per fixture. A 500-fixture corpus typically uses ~1 GB including raw API snapshots. |

Set the token before running:

```powershell
$env:GITHUB_TOKEN = "ghp_..."
```

---

## Quick Start (full pipeline)

The `run-corpus.ps1` script at the repo root orchestrates all six steps with sensible defaults:

```powershell
.\run-corpus.ps1
```

Common overrides:

```powershell
# Discover 200 C# PRs from the last 7 days with at least 3 review comments
.\run-corpus.ps1 -Limit 200 -Language "C#" -MinComments 3 -StartDate "2025-01-01"

# Resume from step 3 (skip discover + hydrate)
.\run-corpus.ps1 -SkipTo 3

# Use custom paths
.\run-corpus.ps1 -Db "./custom/corpus.db" -Fixtures "./custom/fixtures" -Report "./custom/scorecard.md"
```

**`run-corpus.ps1` parameters:**

| Parameter | Default | Description |
|---|---|---|
| `-Provider` | `gh-search` | Discovery provider: `gh-search` (token required) or `gh-archive` (unauthenticated). **Note:** `--min-stars` has no effect with `gh-archive` — event streams contain no star count. |
| `-StartDate` | Yesterday (UTC) | Filter PRs merged/created on or after this date |
| `-EndDate` | _(same as StartDate)_ | End of date range, inclusive |
| `-Limit` | `50` | Max candidates to discover |
| `-Language` | `C#` | Programming language filter (e.g. `C#`, `Python`). Pass empty string to include all. |
| `-MinComments` | `2` | Minimum review comment count |
| `-MinStars` | `500` | Minimum repository star count (`gh-search` only) |
| `-Tier` | `discovery` | Target fixture tier for hydration |
| `-Db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `-Fixtures` | `./data/fixtures` | Fixtures root directory |
| `-Report` | `./data/scorecard.md` | Output path for the markdown report |
| `-SkipTo` | `1` | Resume from step N (1–6) |

`run-corpus.ps1` also folds in any repositories previously recorded in the corpus DB as permanent hydration rejects (for example deleted/private repos) so later runs stop rediscovering and rehydrating them.

---

## Step 1 — `corpus discover`

Searches for pull request candidates and writes them to the corpus SQLite database. No fixture files are created yet.

```
gauntletci corpus discover --provider <provider> [options]
```

| Flag | Default | Description |
|---|---|---|
| `--provider` | _(required)_ | `gh-archive` (no token needed) or `gh-search` (token required) |
| `--limit` | `100` | Maximum candidates to fetch |
| `--language` | _(all)_ | Filter by programming language (e.g. `cs`, `python`) |
| `--min-stars` | `0` | Minimum repository star count |
| `--min-comments` | `0` | Minimum PR review comment count |
| `--start-date` | _(none)_ | Earliest merge/event date to include (UTC, e.g. `2025-03-01`) |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

**Examples:**

```powershell
# GH Archive — no token, fast, last 24 h
gauntletci corpus discover --provider gh-archive --limit 100 --start-date 2025-06-01

# GitHub Search — token required, language-filtered
gauntletci corpus discover --provider gh-search --limit 50 --language cs --min-comments 2
```

> Candidates already in the database are silently skipped (`INSERT OR IGNORE`).

---

## Step 2 — `corpus batch-hydrate`

Fetches full PR data (diff, metadata, review comments) for pending candidates via the GitHub REST API and writes fixture files to disk.

```
gauntletci corpus batch-hydrate [options]
```

| Flag | Default | Description |
|---|---|---|
| `--limit` | `10` | Maximum candidates to hydrate in this run |
| `--tier` | `discovery` | Fixture tier to assign (`gold`, `silver`, `discovery`) |
| `--dry-run` | `false` | Print what would be hydrated without writing any files |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

> **`GITHUB_TOKEN` is required.** Hydration fails immediately if the variable is not set.

**Examples:**

```powershell
# Hydrate next 20 candidates as discovery fixtures
gauntletci corpus batch-hydrate --limit 20

# Preview without writing (dry run)
gauntletci corpus batch-hydrate --limit 50 --dry-run

# Hydrate as silver-tier
gauntletci corpus batch-hydrate --limit 10 --tier silver
```

You can also hydrate a single PR directly by URL:

```powershell
gauntletci corpus add-pr --url https://github.com/owner/repo/pull/123
```

---

## Step 3 — `corpus run` / `corpus run-all`

Executes all GCI rules against fixture diffs and writes `actual.json` to each fixture folder.

### Single fixture

```
gauntletci corpus run --fixture <id> [options]
```

| Flag | Default | Description |
|---|---|---|
| `--fixture` | _(required)_ | Fixture ID, e.g. `torvalds_linux_pr4321` |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

### All fixtures

```
gauntletci corpus run-all [options]
```

| Flag | Default | Description |
|---|---|---|
| `--tier` | _(all tiers)_ | Limit to a single tier (`gold`, `silver`, `discovery`) |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

**Examples:**

```powershell
# Run a single fixture
gauntletci corpus run --fixture torvalds_linux_pr4321

# Run all discovery fixtures
gauntletci corpus run-all --tier discovery

# Run everything
gauntletci corpus run-all
```

Each fixture that has no `diff.patch` is skipped with a `SKIP` log line. Results are saved to `actual.json` (latest) and `actual.{runId}.json` (per-run archive) in the fixture folder.

---

## Step 4 — `corpus label` / `corpus label-all`

Applies silver heuristic labels to `expected.json`. Existing `HumanReview` or `Seed` labels are never overwritten unless `--overwrite` is passed.

### Single fixture

```
gauntletci corpus label --fixture <id> [options]
```

| Flag | Default | Description |
|---|---|---|
| `--fixture` | _(required)_ | Fixture ID to label |
| `--overwrite` | `false` | Replace existing `HumanReview`/`Seed` labels |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

### All fixtures in a tier

```
gauntletci corpus label-all [options]
```

| Flag | Default | Description |
|---|---|---|
| `--tier` | `discovery` | Tier to process (`gold`, `silver`, `discovery`) |
| `--overwrite` | `false` | Replace existing `HumanReview`/`Seed` labels |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

**Examples:**

```powershell
# Label all discovery fixtures
gauntletci corpus label-all

# Label all tiers, overwriting existing heuristic labels
gauntletci corpus label-all --tier gold --overwrite

# Label a single fixture
gauntletci corpus label --fixture torvalds_linux_pr4321
```

---

## Step 5 — `corpus score`

Reads `expected.json` and `actual.json` from all fixtures and computes per-rule scorecards. Results are printed to stdout and upserted into the `aggregates` table in the corpus database.

```
gauntletci corpus score [options]
```

| Flag | Default | Description |
|---|---|---|
| `--rule` | _(all rules)_ | Filter to a single rule ID (e.g. `GCI0007`) |
| `--tier` | _(all tiers)_ | Filter to a single tier |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

**Examples:**

```powershell
# Score all rules across all tiers
gauntletci corpus score

# Score only GCI0003 on silver fixtures
gauntletci corpus score --rule GCI0003 --tier silver
```

> If no `actual.json` files exist, the command reminds you to run `corpus run-all` first.

---

## Step 6 — `corpus report`

Exports the aggregate scorecards as a markdown file.

```
gauntletci corpus report [options]
```

| Flag | Default | Description |
|---|---|---|
| `--output` | `./corpus-report.md` | Destination file path |
| `--db` | `./data/gauntletci-corpus.db` | SQLite database path |
| `--fixtures` | `./data/fixtures` | Fixtures root directory |

**Example:**

```powershell
gauntletci corpus report --output ./data/scorecard.md
```

---

## Silver Heuristics

The `SilverLabelEngine` infers `expected.json` labels from diff content and review comments without human review. Labels have `LabelSource: Heuristic` and are never applied over existing `HumanReview` or `Seed` entries unless `--overwrite` is passed.

### Diff heuristics

| Rule | Trigger condition | Confidence |
|---|---|---|
| **GCI0003** — Empty catch block | Added lines contain empty or comment-only `catch { }` block | 0.65 |
| **GCI0004** — Breaking change | Added lines contain `[Obsolete]` or `throw new NotImplemented` on a public API | 0.55 |
| **GCI0005** — Missing tests | Diff path touches a test file (contains `Test`, `Spec.`, `.test.`) | 0.50 |
| **GCI0006** — Null dereference | Added lines assign `null` or use the null-forgiving operator (`!.`) | 0.50 |
| **GCI0007** — Secret exposure | Added lines contain `password`, `secret`, `api_key`, or `token` | 0.70 |
| **GCI0010** — Hardcoded config | Added lines contain hardcoded URL, host, or port literal | 0.55 |
| **GCI0016** — Sync-over-async | Added lines contain `.Result` or `.Wait()` | 0.60 |
| **GCI0021** — Migration file touched | Diff path contains `Migration` or `_migration` | 0.60 |
| **GCI0022** — Binary/generated file | Diff path contains `.min.js`, `.bundle.`, `.dll`, `.exe`, `.png`, `.jpg` | 0.65 |

### Review comment heuristics

When a fixture has `raw/review-comments.json`, comment bodies are also scanned:

| Rule | Keywords that trigger it | Confidence |
|---|---|---|
| **GCI0003** — Empty catch block | `exception`, `catch`, `swallowing`, `ignored exception` | 0.60 |
| **GCI0004** — Breaking change | `breaking change`, `backwards compat`, `semver`, `api break` | 0.65 |
| **GCI0005** — Missing tests | `needs tests`, `add test`, `missing test`, `no test` | 0.70 |
| **GCI0006** — Null dereference | `null`, `nullable`, `null reference`, `nullreferenceexception` | 0.60 |
| **GCI0007** — Secret exposure | `secret`, `password`, `credential`, `api key`, `token` | 0.75 |
| **GCI0010** — Hardcoded config | `hardcoded`, `magic string`, `magic number`, `config`, `environment variable` | 0.60 |
| **GCI0016** — Sync-over-async | `.result`, `.wait()`, `async`, `blocking`, `deadlock`, `configureawait`, `thread safe`, `race condition`, `lock` | 0.65 |
| **GCI0021** — Migration file touched | `migration`, `schema change`, `db migration`, `database migration` | 0.65 |
| **GCI0022** — Binary/generated file | `large file`, `file size`, `binary file`, `binary blob` | 0.60 |
| **GCI0023** — Broad change | `rename`, `too many files`, `broad change`, `sweeping change` | 0.55 |

When no heuristic matches, no label is written (an empty `expected.json` is not created).

---

## Scoring Formulas

Scorecards are computed per `(RuleId, Tier)` pair. Fixtures marked `IsInconclusive: true` are excluded from precision/recall but counted in the inconclusive rate.

| Metric | Formula |
|---|---|
| **TriggerRate** | `triggered / total` |
| **Precision** | `TP / (TP + FP)` |
| **Recall** | `TP / (TP + FN)` |
| **InconclusiveRate** | `inconclusive / fixtureCount` |
| **AvgUsefulness** | `AVG(usefulness)` from the `evaluations` table (0–5 scale) |

Where:
- `total` = conclusive fixture count for this rule/tier
- `triggered` = fixtures where `actual.json` contains the rule with `DidTrigger: true`
- `TP` = triggered AND `ShouldTrigger: true`
- `FP` = triggered AND `ShouldTrigger: false`
- `FN` = `ShouldTrigger: true` AND not triggered

---

## Fixture Structure

Each fixture is a folder under `{fixtures}/{tier}/{fixtureId}/`.

### Folder layout

```
data/fixtures/
├── gold/
│   └── owner_repo_pr1234/
│       ├── metadata.json
│       ├── expected.json
│       ├── actual.json          ← latest run
│       ├── actual.<runId>.json  ← per-run archive
│       ├── diff.patch
│       ├── notes.md
│       └── raw/                 ← raw GitHub API snapshots
├── silver/
│   └── ...
└── discovery/
    └── ...
```

**Fixture ID format:** `{owner}_{repo}_pr{number}` — all lowercase, `/` and `\` replaced with `_`, spaces replaced with `-`.

Example: `github.com/torvalds/linux/pull/4321` → `torvalds_linux_pr4321`

### `metadata.json`

```json
{
  "FixtureId": "torvalds_linux_pr4321",
  "Tier": "Discovery",
  "Repo": "torvalds/linux",
  "PullRequestNumber": 4321,
  "Language": "C",
  "RuleIds": [],
  "Tags": [],
  "PrSizeBucket": "Large",
  "FilesChanged": 12,
  "HasTestsChanged": false,
  "HasReviewComments": true,
  "BaseSha": "abc123",
  "HeadSha": "def456",
  "Source": "batch",
  "CreatedAtUtc": "2025-06-01T00:00:00Z"
}
```

### `expected.json`

Array of `ExpectedFinding` objects written by `corpus label` / `corpus label-all`:

```json
[
  {
    "RuleId": "GCI0007",
    "ShouldTrigger": true,
    "ExpectedConfidence": 0.7,
    "Reason": "Diff contains credential-related keyword on added lines",
    "LabelSource": "Heuristic",
    "IsInconclusive": false
  }
]
```

| Field | Description |
|---|---|
| `RuleId` | GCI rule identifier |
| `ShouldTrigger` | Whether the rule is expected to fire |
| `ExpectedConfidence` | Heuristic confidence estimate (0–1) |
| `Reason` | Human-readable justification |
| `LabelSource` | `Heuristic`, `HumanReview`, or `Seed` |
| `IsInconclusive` | If `true`, fixture is excluded from precision/recall |

### `actual.json`

Array of `ActualFinding` objects written by `corpus run` / `corpus run-all`:

```json
[
  {
    "RuleId": "GCI0007",
    "DidTrigger": true,
    "ActualConfidence": 0.85,
    "Message": "Possible secret or credential detected",
    "ChangeImplication": "Credential leak risk",
    "Evidence": "Line 42: api_key = \"abc123\"",
    "ExecutionTimeMs": 3
  }
]
```

---

## Tier System

| Tier | Trust level | How fixtures arrive | Label source |
|---|---|---|---|
| **Gold** | Highest | Manually curated by a human reviewer | `HumanReview` or `Seed` |
| **Silver** | Medium | Hydrated and heuristically labeled | `Heuristic` (validated by spot-check) |
| **Discovery** | Lowest | Freshly hydrated, not yet labeled | None (or heuristic pending review) |

Scoring separates results by tier so that Gold-tier precision/recall can be reported independently of noisier Discovery-tier data. When upgrading a fixture from Discovery → Silver or Silver → Gold, update the `Tier` field in `metadata.json` (or re-run `corpus normalize --tier silver`) and move the folder to the appropriate sub-directory.

Gold labels always take precedence over Silver and Heuristic labels. The `SilverLabelEngine` will never overwrite a `HumanReview` or `Seed` entry unless `--overwrite` is explicitly passed.
