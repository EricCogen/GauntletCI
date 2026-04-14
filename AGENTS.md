# GauntletCI — Copilot Agent Instructions

## Project
GauntletCI is a deterministic pre-commit change-risk detection engine built in C#/.NET 8.
Active branch: `v2`. Push with `git push origin HEAD`.

## Build & Test
```
dotnet build GauntletCI.slnx -v quiet --nologo
dotnet test GauntletCI.slnx --no-build --nologo -q
```
All 615 tests must pass before committing.

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
- Todos DB seed: `C:\Users\ericc\.gauntletci\copilot\todos-seed.sql`
- Checkpoints: `C:\Users\ericc\.gauntletci\copilot\checkpoints\`
- Latest checkpoint: `005-banner-rewrite-tier1-tier2-complete.md`
- To restore todos in a new session: run the SQL in todos-seed.sql via the sql tool
  (The two ALTER TABLE lines may error harmlessly if columns already exist — skip them)
- Verify: `SELECT status, COUNT(*) FROM todos GROUP BY status;` → done=45, pending=17, blocked=4

## Todo Priorities
- P1 (done): foundation fixes
- P2 (done): ship-it features
- P3 (done): new rules GCI0021–0027, postmortem-mode, pr-annotations
- P4 (done): moat telemetry pipeline (feedback, opt-in, hashing, upload, local store)
- P4 (done): corpus ingestion (domain models, storage, manual ingestion, normalization)
- P4 (pending): enterprise features (audit-export, mcp-server, risk-score, risk-classifier)
- P4 (pending): corpus phase 2 (run, scoring, silver labels, batch hydration, gh-search, gh-archive, tests)
- P4 (pending): ecosystem (vscode-extension, winget/brew, moat-bootstrap, block-mode, instrument-rules, model-download)

## Seed Sync Convention (Option B)
- After every `UPDATE todos SET status = '...' WHERE id = '...'`, immediately make a surgical
  `edit` to the matching row in `todos-seed.sql` — just flip the status string for that one ID.
- Also update the `-- Status:` header counts in todos-seed.sql when they change.

## Key Conventions
- Commit trailer: `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
- Commit and push after each completed todo
- No markdown files committed except README.md and files listed in .gitignore exceptions
- AGENTS.md and .github/copilot-instructions.md are exceptions (tracked)
- Live LLM tests skipped without ANTHROPIC_API_KEY or OPENAI_API_KEY env vars
- Banner suppression: CI env vars (CI, GITHUB_ACTIONS, TF_BUILD, BUILD_BUILDID, JENKINS_URL)

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

Example: `[RULE] Add GCI0028 Entropy-Based Secret Detection`
