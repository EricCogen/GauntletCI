# GauntletCI — Copilot Agent Instructions

## Project
GauntletCI is a deterministic pre-commit change-risk detection engine built in C#/.NET 8.
Active branch: `main`. Push with `git push origin HEAD`.

## Build & Test
```
dotnet build GauntletCI.slnx -v quiet --nologo
dotnet test GauntletCI.slnx --no-build --nologo -q
```
All 760 tests must pass before committing.

## Architecture
- `src/GauntletCI.Core/` — rule engine, diff parser, configuration, models
- `src/GauntletCI.Cli/` — System.CommandLine CLI (beta4), banner, commands
- `src/GauntletCI.Llm/` — LLM integration: NullLlmEngine (default), LocalLlmEngine (ONNX/Phi-3), RemoteLlmEngine (OpenAI-compatible); Embeddings sub-folder contains OllamaEmbeddingEngine, VectorStore, Distillery, LlmAdjudicator, ExpertSeedFacts
- `src/GauntletCI.Corpus/` — corpus pipeline: hydration, labeling, maintainer fetcher, storage (SQLite)
- `src/GauntletCI.Tests/` — xUnit tests
- Rules: `src/GauntletCI.Core/Rules/Implementations/GCI00XX_*.cs` — each implements `IRule`
- Rules are auto-discovered via reflection in `RuleOrchestrator` — no manual registration needed
- System.CommandLine beta4: use `InvocationContext` overload to bypass 8-param `SetHandler` limit

## New CLI Commands (added after initial build)
- `gauntletci analyze --with-llm` — enriches high findings with a locally running Ollama LLM
- `gauntletci analyze --with-expert-context` — attaches closest expert fact from local vector store
- `gauntletci llm seed` — seeds 11 hand-curated .NET expert facts into `~/.gauntletci/expert-embeddings.db`
- `gauntletci llm distill --input <file.json>` — distills expert facts from GitHub issues via LLM
- `gauntletci corpus maintainers fetch` — fetches high-signal PRs from top .NET OSS contributors

## Session State
- **Durable todos DB**: `C:\Users\ericc\.gauntletci\copilot\todos.db` ← source of truth
- Sync script: `C:\Users\ericc\.gauntletci\copilot\sync-todos.py`
- **Rules DB**: `C:\Users\ericc\.gauntletci\copilot\rules.db` ← rule metadata + corpus scores
- Rules sync: `C:\Users\ericc\.gauntletci\copilot\sync-rules.py`
  - `--rebuild` — full re-sync from C# source + corpus scores (run after adding/archiving a rule)
  - `--scores`  — scores only from corpus DB (auto-run by build-corpus.ps1 after Step 5)
  - `--status`  — print summary
- Checkpoints: `C:\Users\ericc\.gauntletci\copilot\checkpoints\`
- Latest checkpoint: `005-banner-rewrite-tier1-tier2-complete.md`

### Restoring todos in a new session
The `sql` tool uses a per-session DB (resets each session). At session start, load from the durable DB:
```powershell
cd "C:\Users\ericc\.gauntletci\copilot"
python -c @"
import sqlite3, json
con = sqlite3.connect('todos.db')
con.row_factory = sqlite3.Row
todos = [dict(r) for r in con.execute('SELECT id, title, description, status FROM todos ORDER BY id')]
deps  = [dict(r) for r in con.execute('SELECT todo_id, depends_on FROM todo_deps')]
con.close()
with open('todos-export.json', 'w') as f: json.dump({'todos': todos, 'deps': deps}, f)
print(f'Exported {len(todos)} todos')
"@
```
Then use the `sql` tool to INSERT from the exported data, or just query `todos.db` directly via PowerShell.

To check current status without restoring: `python sync-todos.py --status`

### Updating todo status
After every status change, update BOTH the session SQL and the durable DB:
```powershell
python sync-todos.py --update <id> <status>
```
Do NOT maintain `todos-seed.sql` — it is retired. The durable DB is the only source of truth.

## Dead Code Audit

### Quick (deterministic) — run anytime
```powershell
.\audit-dead-code.ps1
```
Covers: Roslyn analyzer warnings, stale archived-rule test files, public types in exe-only
projects, unused Python imports, orphaned HTML templates, duplicate private methods.

### Full (semantic) — ask Copilot
Paste this prompt into Copilot to trigger a full three-agent audit:

> **"run the dead code audit"**

What the agents cover:
| Agent | Scope |
|-------|-------|
| C# deep audit | Roslyn, unused privates, dead branches, `NotImplementedException` stubs, empty catches, no-op overrides |
| Public symbol cross-reference | Every public/internal class + method grep'd across entire src/ + tests/ |
| Non-C# audit | Python functions, HTML templates, PowerShell params, YAML workflows, CSS/JS, .gitignore |

**Known false positive:** `RuleTestExtensions` (GauntletCI.Tests) — extension method on `IRule`,
used implicitly at every rule test call site via `rule.EvaluateAsync(diff, null)` syntax.
Do not delete it.

**After any archiving cycle:** run `sync-rules.py --rebuild` then `.\audit-dead-code.ps1`
to verify no stale test files remain.

## Todo Priorities
- P1 (done): foundation fixes
- P2 (done): ship-it features
- P3 (done): new rules GCI0021–0027, postmortem-mode, pr-annotations
- P4 (done): moat telemetry pipeline (feedback, opt-in, hashing, upload, local store)
- P4 (done): corpus ingestion (domain models, storage, manual ingestion, normalization)
- P4 (pending): LLM adjudicator + vector DB wiring
- P4 (pending): enterprise features (audit-export, mcp-server, risk-score, risk-classifier)
- P4 (pending): ecosystem (vscode-extension, winget/brew, moat-bootstrap, block-mode)

## Key Conventions
- Do NOT include a Co-authored-by trailer in commit messages
- Commit and push after each completed todo
- No markdown files committed except README.md and files listed in .gitignore exceptions
- AGENTS.md and .github/copilot-instructions.md are exceptions (tracked)
- Live LLM tests skipped without ANTHROPIC_API_KEY or OPENAI_API_KEY env vars
- Banner suppression: CI env vars (CI, GITHUB_ACTIONS, TF_BUILD, BUILD_BUILDID, JENKINS_URL)

## Pre-Commit & Push Rules (MANDATORY — follow every time)

### Before committing
1. Build must pass: `dotnet build GauntletCI.slnx -v quiet --nologo`
2. All tests must pass: `dotnet test GauntletCI.slnx --no-build --nologo -q`
3. Run GauntletCI on the staged diff: `git diff HEAD | dotnet run --project src/GauntletCI.Cli --no-build --`
4. Check core-engineering-rules.md (ask Copilot: "check core-engineering-rules.md on my changes")
5. **Both the self-audit (step 3) and the rules check (step 4) must pass — only then commit.**

### Before pushing
- **Always ask the user for approval before running `git push`.**
- Phrase it as a yes/no question: "Ready to push — shall I go ahead?"
- Only push after receiving a positive reply. Never push unilaterally.

## Commit Tags
Prefix commit messages with a tag in brackets when the change falls into a known category:

| Tag | When to use |
|-----|-------------|
| `[RULE]` | Adding, modifying, or deleting a rule (`GCI00XX_*.cs`) or its tests |
| `[CONFIG]` | Changes to configuration models, loaders, or schema |
| `[CLI]` | Changes to the CLI layer (`GauntletCI.Cli`) |
| `[TEST]` | Test-only changes (no production code touched) |
| `[INFRA]` | CI/CD, build, or tooling changes |
| `[DOCS]` | Documentation-only changes |
| `[EXP]` | Experimental features (anything under `experimental` config, Advisory-severity findings) |

Example: `[RULE] Add GCI0028 Entropy-Based Secret Detection`
Example: `[EXP] Add engineering policy LLM evaluation step`
