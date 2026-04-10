# GauntletCI — Copilot Agent Instructions

## Project
GauntletCI is a deterministic pre-commit change-risk detection engine built in C#/.NET 8.
Active branch: `v2`. Push with `git push origin HEAD`.

## Build & Test
```
dotnet build GauntletCI.slnx -v quiet --nologo
dotnet test GauntletCI.slnx --no-build --nologo -q
```
All 126 tests must pass before committing.

## Architecture
- `src/GauntletCI.Core/` — rule engine, diff parser, configuration, models
- `src/GauntletCI.Cli/` — System.CommandLine CLI (beta4), banner, commands
- `src/GauntletCI.Llm/` — ONNX LLM integration (NullLlmEngine in use)
- `src/GauntletCI.Tests/` — xUnit tests
- Rules: `src/GauntletCI.Core/Rules/Implementations/GCI00XX_*.cs` — each implements `IRuleEvaluator`
- Rules are auto-discovered via reflection in `RuleOrchestrator`
- System.CommandLine beta4: use `InvocationContext` overload to bypass 8-param `SetHandler` limit

## Session State
- Todos DB seed: `C:\Users\ericc\.gauntletci\copilot\todos-seed.sql`
- Checkpoints: `C:\Users\ericc\.gauntletci\copilot\checkpoints\`
- Latest checkpoint: `005-banner-rewrite-tier1-tier2-complete.md`
- To restore todos in a new session: run the SQL in todos-seed.sql via the sql tool
  (The two ALTER TABLE lines may error harmlessly if columns already exist — skip them)
- Verify: `SELECT status, COUNT(*) FROM todos GROUP BY status;` → done=15, pending=24, blocked=2

## Todo Priorities
- P1 (done): foundation fixes
- P2 (done): ship-it features
- P3 (pending): new rules GCI0021–0027, postmortem-mode, pr-annotations
- P4 (pending): moat telemetry pipeline, enterprise features, backlog

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
