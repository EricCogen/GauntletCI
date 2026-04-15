[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

# GauntletCI - Pre-commit change-risk detection for pull request diffs

**GauntletCI** is a .NET CLI tool that analyzes pull request diffs or as a pre-commit audit that detects **behavioral change-risk before code is merged**.

It answers one question:

> Did this change introduce behavior that is not properly validated?

---

## 📖 Why This Exists

Even experienced developers miss things in diffs.

Not because they lack skill — but because diffs are deceptive.

A small change can silently alter behavior:

- A new null check changes execution flow
- A guard clause introduces new exceptions
- A method signature changes without test updates
- A dependency call is modified without validation
- A conditional branch shifts logic in subtle ways

These are not syntax errors.  
They are **behavior changes** — and they regularly slip through code review.

GauntletCI exists to catch them **before they reach production**.

Read the full [story](STORY.md) behind GauntletCI.

---

## 🧠 Philosophy & Principles

GauntletCI is built on a clear set of principles defined in the **[GauntletCI Charter](CHARTER.md)**:

- **Coverage is Not Correctness** — Tests prove execution, not survival.
- **Falsification Over Verification** — We seek to disprove safety, not confirm compliance.
- **Intent is Material Context** — We cross-reference PR diffs with linked issues to detect semantic drift.
- **Privacy is Absolute** — All reasoning happens locally; no code ever leaves your machine.
- **Determinism Anchors Intelligence** — The local AI *explains*; deterministic Roslyn rules *enforce*.

---

## 🔍 What GauntletCI is (and is not)

### ✅ What it is
- Diff-aware change-risk detector
- Pre-commit / pre-merge safety layer
- Focused on behavior, not style
- **Intent-aware** — Cross-references implementation with linked issues (GitHub/Jira)

### ❌ What it is not
- Not a linter
- Not a test runner
- Not a static analysis replacement
- Not a code formatter

GauntletCI complements your existing tools; it does not replace them.

---

## 🚀 What it does

- Analyzes only **what changed** in a diff
- Detects **unvalidated behavior changes**
- Flags **missing or weak test coverage**
- Identifies **execution flow changes** (guards, exceptions, branching)
- Surfaces **API and contract changes**
- **Intent Alignment:** Compares PR diff with linked GitHub Issue to detect when the implementation drifts from the stated goal.
- Outputs actionable findings with file paths and line numbers

---

## 📦 Installation

```bash
dotnet tool install -g GauntletCI
```

---

## ⚡ Quick Start

```bash
# Analyze staged changes before committing
gauntletci analyze --staged

# Analyze a pull request diff file
gauntletci analyze --diff pr.diff

# Analyze a specific commit
gauntletci analyze --commit abc1234

# Export audit history as CSV
gauntletci audit export --format csv --output report.csv

# Expose GauntletCI to an AI assistant via MCP
gauntletci mcp serve
```

---

## 🛠 Commands

### `gauntletci analyze` — Analyze a diff

```bash
# Analyze staged changes
gauntletci analyze --staged

# Analyze a diff file
gauntletci analyze --diff pr.diff

# Analyze a specific commit
gauntletci analyze --commit abc1234

# Output as JSON
gauntletci analyze --staged --output json

# Emit GitHub Actions annotations
gauntletci analyze --staged --github-annotations

# Enrich high-confidence findings with a locally running Ollama LLM
gauntletci analyze --staged --with-llm

# Attach the closest expert knowledge citation from the local vector store
gauntletci analyze --staged --with-expert-context

# Both LLM enrichment and expert context (requires Ollama + seeded vector store)
gauntletci analyze --staged --with-llm --with-expert-context
```

### `gauntletci audit` — Local audit trail

Every scan is automatically logged to `~/.gauntletci/audit-log.ndjson`.

```bash
# Export full audit log as JSON
gauntletci audit export

# Export as CSV
gauntletci audit export --format csv --output report.csv

# Filter to last 30 scans
gauntletci audit export --last 30

# Filter by date
gauntletci audit export --since 2025-01-01

# Quick summary stats
gauntletci audit stats
```

### `gauntletci mcp serve` — AI assistant integration (MCP)

GauntletCI exposes itself as a [Model Context Protocol](https://modelcontextprotocol.io/) server. Any MCP-compatible AI assistant (Claude Desktop, Cursor, Copilot, Windsurf) can call GauntletCI tools mid-conversation.

```bash
gauntletci mcp serve
```

**Claude Desktop config** (`~/.claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "gauntletci": {
      "command": "gauntletci",
      "args": ["mcp", "serve"]
    }
  }
}
```

**Available MCP tools:**

| Tool | Description |
|------|-------------|
| `analyze_staged` | Analyze staged git changes |
| `analyze_diff` | Analyze a raw diff string |
| `analyze_commit` | Analyze a specific commit |
| `list_rules` | List all 42+ analysis rules |
| `audit_stats` | Aggregate stats from local audit log |

When Ollama is running locally, the MCP server can optionally use `RemoteLlmEngine` to provide LLM-powered explanations for high-confidence findings inline in your AI assistant conversation.

### `gauntletci llm` — Local expert knowledge commands

GauntletCI can build a local vector store of expert .NET facts to attach as citations alongside findings. This requires [Ollama](https://ollama.com) running locally with `nomic-embed-text` pulled.

```bash
# Install the embedding model (one time)
ollama pull nomic-embed-text

# Seed 11 hand-curated .NET expert facts into the local vector store
gauntletci llm seed

# Distill expert facts from a GitHub issues JSON file via LLM
gauntletci llm distill --input issues.json

# Limit distillation to the top 50 highest-engagement issues
gauntletci llm distill --input issues.json --max-records 50
```

Vector store location: `~/.gauntletci/expert-embeddings.db`

### `gauntletci corpus maintainers fetch` — Fetch high-signal OSS PRs

Fetches merged PRs and issues from top .NET OSS contributors for use in the corpus pipeline.

```bash
# Fetch from default repos (dotnet/runtime, dotnet/roslyn, etc.)
gauntletci corpus maintainers fetch

# Limit results per label
gauntletci corpus maintainers fetch --max-per-label 20

# Write output to a specific file
gauntletci corpus maintainers fetch --output maintainers.json

# Scope to a specific repository
gauntletci corpus maintainers fetch --repo dotnet/runtime
```

Requires `GITHUB_TOKEN` env var for authenticated requests (5000 req/hr vs 60 unauthenticated).

### Other commands

- `gauntletci init` — Initialize GauntletCI config in your repo
- `gauntletci ignore` — Manage the ignore list
- `gauntletci postmortem` — Run postmortem analysis
- `gauntletci feedback` — Submit feedback on a finding
- `gauntletci telemetry` — Manage telemetry opt-in/out
- `gauntletci llm seed` — Seed local expert knowledge vector store
- `gauntletci llm distill` — Distill expert facts from GitHub issues via LLM
- `gauntletci corpus maintainers fetch` — Fetch high-signal OSS maintainer PRs

---

## 🤖 Local LLM Integration

GauntletCI supports optional local LLM enrichment via [Ollama](https://ollama.com). All inference runs on your machine — no data leaves your environment.

### Setup

```bash
# Install Ollama: https://ollama.com
# Pull the embedding model
ollama pull nomic-embed-text

# Seed expert knowledge into the local vector store
gauntletci llm seed
```

### Usage

```bash
# Enrich findings with LLM explanations (Ollama must be running)
gauntletci analyze --staged --with-llm

# Attach expert knowledge citations
gauntletci analyze --staged --with-expert-context

# Both together
gauntletci analyze --staged --with-llm --with-expert-context
```

When `--with-expert-context` is active, each high-confidence finding is embedded and matched against `~/.gauntletci/expert-embeddings.db`. If a match scores above the similarity threshold, the expert citation is shown inline (CLI) and included in GitHub annotations and MCP tool responses.

**All local LLM processing is fully offline.** Ollama runs locally; nothing is sent to any cloud service.

---

## 📏 Rules

GauntletCI ships **42 built-in rules** (GCI0001–GCI0042) covering:

- Behavioral change detection and goal alignment
- Security risk, PII logging, authorization coverage
- Test coverage and test quality gaps
- Async safety, resource lifecycle, disposable resource management
- Data schema compatibility, idempotency/retry safety
- Observability, structured logging, rollback safety
- Architecture layer discipline and dependency injection safety

See [`docs/rules.md`](docs/rules.md) for the full rule catalogue.

---

## ⚙️ Configuration

Run `gauntletci init` to generate a `.gauntletci.json` config file in your repository root.

### Severity Levels

Every finding has a severity that controls display and exit-code behavior:

| Severity | Color | Shown by default | Blocks commit |
|----------|-------|-----------------|---------------|
| **Block** | 🔴 Red | ✅ Yes | ✅ Yes (by default) |
| **Warn** | 🟡 Yellow | ✅ Yes | ❌ No (unless `exitOn: "Warn"`) |
| **Info** | ⬜ Grey | ❌ No (use `--verbose`) | ❌ No |
| **None** | — | ❌ Never | ❌ Rule is skipped entirely |

### Configure via `.editorconfig`

```ini
[*.cs]
dotnet_diagnostic.GCI0003.severity = error       # Block
dotnet_diagnostic.GCI0008.severity = warning     # Warn
dotnet_diagnostic.GCI0011.severity = suggestion  # Info
dotnet_diagnostic.GCI0023.severity = none        # Disable
```

### Configure via `.gauntletci.json`

`.gauntletci.json` takes precedence over `.editorconfig`:

```json
{
  "exitOn": "Block",
  "rules": {
    "GCI0008": { "severity": "Warn", "threshold": 20 },
    "GCI0035": { "severity": "Block" },
    "GCI0023": { "severity": "None" }
  }
}
```

**Priority order:** `.gauntletci.json` → `.editorconfig` → built-in defaults

A [JSON schema](schemas/gauntletci-schema.json) is provided for IntelliSense in VS Code.

### CLI Severity Flags

```bash
gauntletci analyze --staged               # Show Warn + Block (default)
gauntletci analyze --staged --verbose     # Also show Info findings
gauntletci analyze --staged --severity block  # Show only Block findings
```

### Exit Code Behavior

| Condition | Exit code |
|-----------|-----------|
| No Block findings (default `exitOn: "Block"`) | `0` |
| Any Block finding | `1` |
| `exitOn: "Warn"` + any Warn or Block finding | `1` |
| Internal error | `2` |

---

## 🔒 Privacy

All analysis is **local**. No code ever leaves your machine — including when using Ollama for local LLM enrichment.

- Telemetry is **opt-in** and anonymous (no code, no file paths, no content)
- All findings are stored only in `~/.gauntletci/audit-log.ndjson`
- Ollama runs locally; all LLM inference stays on your machine

See the [GauntletCI Charter](CHARTER.md) for the full privacy commitment.

---

## 📄 License

[Elastic License 2.0](LICENSE)
