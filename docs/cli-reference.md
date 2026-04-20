# GauntletCI CLI Reference

**Version:** 2.0.0  
**Tool command:** `gauntletci`

GauntletCI is a deterministic pre-commit risk detection engine. It analyses git diffs for risky change patterns using a rule-based engine with optional local LLM enrichment.

---

## Global options

These options are available on the root command:

| Flag | Description |
|------|-------------|
| `--version` | Display the version number |
| `--help`, `-h`, `-?` | Show help for the command or subcommand |

---

## Environment variables

| Variable | Affects | Description |
|----------|---------|-------------|
| `GITHUB_TOKEN` | `corpus add-pr`, `corpus batch-hydrate`, `corpus discover` | Personal access token for GitHub REST API calls. Required for hydrating PRs from private repos; used for authenticated rate limits on public repos. |
| `GAUNTLETCI_NO_BANNER` | All commands | Set to any non-empty value to suppress the ASCII banner without passing `--no-banner`. |
| `CI` | Banner, telemetry | Suppresses the ASCII banner and telemetry consent prompt automatically. |
| `GITHUB_ACTIONS` | Banner, telemetry | Suppresses banner and telemetry prompt (set automatically by GitHub Actions runners). |
| `TF_BUILD` | Banner, telemetry | Suppresses banner and telemetry prompt (Azure Pipelines). |
| `BUILD_BUILDID` | Banner | Suppresses banner (Azure Pipelines legacy). |
| `JENKINS_URL` | Banner | Suppresses banner (Jenkins). |

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success — no findings detected (for `analyze` / `postmortem`) or command completed normally |
| `1` | Findings detected (for `analyze` / `postmortem`), or invalid input for other commands |
| `2` | Unhandled error / exception |

---

## analyze

Analyse a git diff for pre-commit risks. Runs all enabled rules against the diff and reports findings.

```
gauntletci analyze [options]
```

Exactly one diff source should be specified. If none is provided, diff content is read from stdin.

### Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--diff <path>` | `file` | — | Path to a `.diff` file |
| `--commit <sha>` | `string` | — | Commit SHA to analyse |
| `--staged` | `bool` | `false` | Analyse staged changes (`git diff --cached`) |
| `--unstaged` | `bool` | `false` | Analyse unstaged changes (`git diff`) |
| `--all-changes` | `bool` | `false` | Analyse all local changes: staged + unstaged (`git diff HEAD`) |
| `--repo <path>` | `directory` | CWD | Repository root (used for config loading and git operations) |
| `--output <format>` | `string` | `text` | Output format: `text` or `json` |
| `--with-llm` | `bool` | `false` | Enable LLM enrichment of High-confidence findings (requires `gauntletci model download`; adds latency) |
| `--github-annotations` | `bool` | `false` | Emit GitHub Actions workflow commands for inline PR annotations |
| `--ascii` | `bool` | `false` | Use ASCII-only output (for terminals without Unicode support) |
| `--no-banner` | `bool` | `false` | Disable ASCII banner |

> **Note:** `--no-llm` is a hidden deprecated flag. LLM is opt-in via `--with-llm`.

### Examples

```bash
# Analyse staged changes before a commit
gauntletci analyze --staged

# Analyse all local changes (staged + unstaged)
gauntletci analyze --all-changes

# Analyse a specific commit
gauntletci analyze --commit abc1234

# Analyse from a saved diff file
gauntletci analyze --diff changes.diff

# Pipe a diff from stdin
git diff HEAD | gauntletci analyze

# Output JSON for downstream tooling
gauntletci analyze --staged --output json

# Enable LLM enrichment (requires model download first)
gauntletci analyze --staged --with-llm

# Emit GitHub Actions inline annotations
gauntletci analyze --staged --github-annotations

# Use in CI without banner
gauntletci analyze --staged --no-banner --output json

# Analyse a specific repo from a different working directory
gauntletci analyze --staged --repo /path/to/my-repo
```

### Pre-commit hook

After running `gauntletci init`, a git `pre-commit` hook is installed automatically. It runs `gauntletci analyze --staged` and blocks the commit if findings are detected (exit code 1).

---

## corpus

Manage the GauntletCI fixture corpus — a local database of pull requests used to evaluate and score rule quality.

```
gauntletci corpus <subcommand> [options]
```

> **Note:** Most corpus subcommands require `GITHUB_TOKEN` for GitHub API access. The default corpus database path is `./data/gauntletci-corpus.db` and the default fixtures root is `./data/fixtures`.

---

### corpus add-pr

Hydrate a single pull request and add it to the corpus.

```
gauntletci corpus add-pr --url <url> [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--url <url>` | `string` | **required** | GitHub PR URL (e.g. `https://github.com/owner/repo/pull/42`) |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Add a single PR to the corpus
gauntletci corpus add-pr --url https://github.com/owner/repo/pull/42
```

---

### corpus normalize

Re-normalize a fixture from its existing raw snapshots (re-runs the normalization pipeline without re-fetching from GitHub).

```
gauntletci corpus normalize --fixture <id> [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--fixture <id>` | `string` | **required** | Fixture ID (e.g. `owner_repo_pr1234`) |
| `--tier <tier>` | `string` | `discovery` | Fixture tier: `gold`, `silver`, or `discovery` |
| `--owner <owner>` | `string` | — | Repo owner override |
| `--repo <repo>` | `string` | — | Repo name override |
| `--pr <number>` | `int` | — | PR number override |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
gauntletci corpus normalize --fixture owner_repo_pr42 --tier gold
```

---

### corpus list

Enumerate and filter corpus fixtures.

```
gauntletci corpus list [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--tier <tier>` | `string` | — | Filter by tier: `gold`, `silver`, or `discovery` |
| `--language <lang>` | `string` | — | Filter by language (e.g. `cs`, `py`) |
| `--tag <tag>` | `string[]` | — | Filter by tag (repeatable or comma-separated) |
| `--output <format>` | `string` | `text` | Output format: `text` or `json` |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# List all fixtures
gauntletci corpus list

# List only gold-tier C# fixtures
gauntletci corpus list --tier gold --language cs

# List as JSON
gauntletci corpus list --output json

# Filter by tag
gauntletci corpus list --tag security --tag large-diff
```

---

### corpus discover

Discover pull request candidates from GitHub and persist them to the corpus database.

```
gauntletci corpus discover --provider <provider> [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--provider <provider>` | `string` | **required** | Discovery provider: `gh-search` or `gh-archive` |
| `--limit <n>` | `int` | `100` | Maximum candidates to fetch |
| `--language <lang>` | `string` | — | Filter by programming language (e.g. `cs`, `python`) |
| `--min-stars <n>` | `int` | `0` | Minimum stars on the repository |
| `--min-comments <n>` | `int` | `0` | Minimum review comment count |
| `--start-date <date>` | `DateTime` | — | Filter by merge/event date (inclusive, UTC) |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Discover up to 50 C# PRs with at least 2 review comments
gauntletci corpus discover --provider gh-search --limit 50 --language cs --min-comments 2

# Discover from GH Archive since a specific date
gauntletci corpus discover --provider gh-archive --start-date 2024-01-01 --limit 200
```

---

### corpus batch-hydrate

Bulk hydrate pending candidates from the corpus database.

```
gauntletci corpus batch-hydrate [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--limit <n>` | `int` | `10` | Maximum number of candidates to hydrate |
| `--tier <tier>` | `string` | `discovery` | Target tier: `gold`, `silver`, or `discovery` |
| `--dry-run` | `bool` | `false` | Print what would be processed without hydrating |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Hydrate up to 25 candidates into the discovery tier
gauntletci corpus batch-hydrate --limit 25

# Preview what would be hydrated
gauntletci corpus batch-hydrate --limit 10 --dry-run

# Hydrate into the silver tier
gauntletci corpus batch-hydrate --limit 50 --tier silver
```

---

### corpus run

Run GCI rules against a single corpus fixture.

```
gauntletci corpus run --fixture <id> [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--fixture <id>` | `string` | **required** | Fixture ID to run rules against |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
gauntletci corpus run --fixture owner_repo_pr42
```

---

### corpus run-all

Run GCI rules against all (or filtered) corpus fixtures.

```
gauntletci corpus run-all [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--tier <tier>` | `string` | — | Filter by tier: `gold`, `silver`, or `discovery` |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Run all fixtures
gauntletci corpus run-all

# Run only gold-tier fixtures
gauntletci corpus run-all --tier gold
```

---

### corpus score

Compute rule scorecards from corpus fixture results.

```
gauntletci corpus score [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--rule <id>` | `string` | — | Filter by rule ID (e.g. `GCI0001`) |
| `--tier <tier>` | `string` | — | Filter by tier: `gold`, `silver`, or `discovery` |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Score all rules across all fixtures
gauntletci corpus score

# Score a specific rule on gold fixtures only
gauntletci corpus score --rule GCI0003 --tier gold
```

---

### corpus report

Export a Markdown scorecard report for all rules.

```
gauntletci corpus report [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--output <path>` | `string` | `./corpus-report.md` | Output file path for the Markdown report |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
gauntletci corpus report

gauntletci corpus report --output ./reports/2024-06-scorecard.md
```

---

### corpus label

Apply silver heuristic labels to a single corpus fixture.

```
gauntletci corpus label --fixture <id> [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--fixture <id>` | `string` | **required** | Fixture ID to label |
| `--overwrite` | `bool` | `false` | Overwrite existing `HumanReview`/`Seed` labels with heuristic labels |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
gauntletci corpus label --fixture owner_repo_pr42

# Force overwrite existing human-reviewed labels
gauntletci corpus label --fixture owner_repo_pr42 --overwrite
```

---

### corpus label-all

Apply silver heuristic labels to all fixtures in a tier.

```
gauntletci corpus label-all [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--tier <tier>` | `string` | `discovery` | Fixture tier to process: `gold`, `silver`, or `discovery` |
| `--overwrite` | `bool` | `false` | Overwrite existing `HumanReview`/`Seed` labels with heuristic labels |
| `--db <path>` | `string` | `./data/gauntletci-corpus.db` | Path to corpus SQLite database |
| `--fixtures <path>` | `string` | `./data/fixtures` | Path to fixtures root directory |

```bash
# Label all discovery fixtures
gauntletci corpus label-all

# Label all silver fixtures, overwriting previous labels
gauntletci corpus label-all --tier silver --overwrite
```

---

## model

Manage the local LLM model used for finding enrichment. GauntletCI uses the **Phi-4 Mini INT4 ONNX** model (~2 GB) for offline enrichment of High-confidence findings.

```
gauntletci model <subcommand> [options]
```

---

### model download

Download the Phi-4 Mini INT4 ONNX model for offline enrichment.

```
gauntletci model download [options]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--dir <path>` | `string` | `~/.gauntletci/models/phi4-mini` | Directory to download the model into |

```bash
# Download to default location
gauntletci model download

# Download to a custom directory
gauntletci model download --dir /mnt/models/phi4-mini
```

After downloading, pass `--with-llm` to `analyze` to enable enrichment.

---

### model status

Show whether the local LLM model is downloaded and ready.

```
gauntletci model status
```

No options. Prints the cached model path and whether enrichment is available.

```bash
gauntletci model status
```

---

## feedback

Rate the quality of the last analysis. Feedback is stored as an anonymous telemetry event and uploaded with the next batch. Requires telemetry to be enabled (`gauntletci telemetry --enable`).

```
gauntletci feedback <vote>
```

| Argument | Type | Description |
|----------|------|-------------|
| `vote` | `string` | **required** — `up` (useful) or `down` (too noisy) |

```bash
# Mark the last analysis as useful
gauntletci feedback up

# Mark it as too noisy
gauntletci feedback down
```

> Feedback is silently accepted without error if telemetry is not enabled, but is not stored.

---

## telemetry

Manage telemetry preferences. Telemetry collects anonymous usage data to improve rule quality.

```
gauntletci telemetry [options]
```

With no options, prints the current telemetry status and install ID.

### Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--status` | `bool` | `false` | Show current telemetry status and install ID |
| `--mode <mode>` | `string` | — | Set telemetry mode: `shared`, `local`, or `off` |
| `--enable` | `bool` | `false` | Opt in to shared telemetry (alias for `--mode shared`) |
| `--disable` | `bool` | `false` | Disable telemetry completely (alias for `--mode off`) |

### Telemetry modes

| Mode | Behaviour |
|------|-----------|
| `shared` | Events stored locally **and** uploaded anonymously for aggregate analysis |
| `local` | Events stored locally only — no network calls |
| `off` | Telemetry disabled entirely |

```bash
# Show current status
gauntletci telemetry

# Opt in to shared telemetry
gauntletci telemetry --enable

# Store locally only
gauntletci telemetry --mode local

# Disable telemetry
gauntletci telemetry --disable
```

> The first-run consent prompt is skipped automatically in CI environments (`CI`, `GITHUB_ACTIONS`, `TF_BUILD`). It is also skipped when running `gauntletci telemetry` directly.

---

## postmortem

Analyse a past commit to see what GauntletCI would have caught at pre-commit time. Identical rule set to `analyze`, but targets a historical commit rather than local changes.

```
gauntletci postmortem --commit <sha> [options]
```

### Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--commit <sha>` | `string` | **required** | Commit SHA to analyse |
| `--repo <path>` | `directory` | CWD | Repository root |
| `--output <format>` | `string` | `text` | Output format: `text` or `json` |
| `--no-banner` | `bool` | `false` | Disable ASCII banner |
| `--ascii` | `bool` | `false` | ASCII-only output |

### Examples

```bash
# See what would have been caught on a specific commit
gauntletci postmortem --commit abc1234

# JSON output for scripting
gauntletci postmortem --commit abc1234 --output json

# Run against a repo in a different directory
gauntletci postmortem --commit abc1234 --repo /path/to/my-repo
```

Exit codes match `analyze`: `0` = no findings, `1` = findings detected, `2` = error.

---

## init

Create a default `.gauntletci.json` configuration file and install pre-commit hooks in the current git repository.

```
gauntletci init [options]
```

### Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--dir <path>` | `directory` | CWD | Directory to create `.gauntletci.json` in |
| `--force` | `bool` | `false` | Overwrite existing hook files if present |
| `--no-telemetry` | `bool` | `false` | Skip the telemetry consent prompt during init |

### What it does

1. Creates `.gauntletci.json` in the target directory with all rules enabled (`GCI0001`–`GCI0027`).
2. Installs `.git/hooks/pre-commit` (bash) and `.git/hooks/pre-commit.ps1` (PowerShell) hooks that run `gauntletci analyze --staged` on every commit.
3. Prompts for telemetry consent on first run (unless `--no-telemetry` is passed).

### Examples

```bash
# Initialize in the current directory
gauntletci init

# Initialize without telemetry prompt (e.g. in automated setup scripts)
gauntletci init --no-telemetry

# Re-install hooks after updating GauntletCI
gauntletci init --force

# Initialize in a specific directory
gauntletci init --dir /path/to/my-repo
```

---

## ignore

Add a suppression entry to `.gauntletci-ignore` to silence a specific rule globally or for a path pattern.

```
gauntletci ignore <rule-id> [options]
```

### Arguments

| Argument | Type | Description |
|----------|------|-------------|
| `rule-id` | `string` | **required** — Rule ID to suppress (e.g. `GCI0003`). Case-insensitive; normalized to uppercase. |

### Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--path <glob>` | `string` | — | Optional glob pattern to restrict suppression to matching paths (e.g. `src/Generated/**`) |
| `--repo <path>` | `directory` | CWD | Repository root (where `.gauntletci-ignore` lives) |

### Examples

```bash
# Suppress GCI0003 everywhere in the repo
gauntletci ignore GCI0003

# Suppress GCI0003 only in generated code
gauntletci ignore GCI0003 --path src/Generated/**

# Suppress in a specific repo
gauntletci ignore GCI0007 --path tests/** --repo /path/to/my-repo
```

> Entries are appended to `.gauntletci-ignore` in the repository root. The file is read on every `analyze` run. To re-enable a rule, remove the relevant line from `.gauntletci-ignore` manually.
