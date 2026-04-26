# GauntletCI — Agent Instructions

## Project
GauntletCI is a deterministic pre-commit change-risk detection engine built in C#/.NET 8.

## Build & Test
```
dotnet build GauntletCI.slnx -v quiet --nologo
dotnet test GauntletCI.slnx --no-build --nologo -q
```
All tests must pass before committing.

## Architecture
- `src/GauntletCI.Core/` — rule engine, diff parser, configuration, models
- `src/GauntletCI.Cli/` — System.CommandLine CLI (beta4), banner, commands
- `src/GauntletCI.Llm/` — LLM integration: NullLlmEngine (default), LocalLlmEngine (ONNX/Phi-4), RemoteLlmEngine (OpenAI-compatible); Embeddings sub-folder contains OllamaEmbeddingEngine, VectorStore, Distillery, LlmAdjudicator, ExpertSeedFacts
- `src/GauntletCI.Corpus/` — corpus pipeline: hydration, labeling, maintainer fetcher, storage (SQLite)
- `src/GauntletCI.Tests/` — xUnit tests
- Rules: `src/GauntletCI.Core/Rules/Implementations/GCI00XX_*.cs` — each implements `IRule`
- Rules are auto-discovered via reflection in `RuleOrchestrator` — no manual registration needed
- System.CommandLine beta4: use `InvocationContext` overload to bypass 8-param `SetHandler` limit

## New CLI Commands
- `gauntletci analyze --with-llm` — enriches high findings with a locally running Ollama LLM
- `gauntletci analyze --with-expert-context` — attaches closest expert fact from local vector store
- `gauntletci llm seed` — seeds 11 hand-curated .NET expert facts into `~/.gauntletci/expert-embeddings.db`
- `gauntletci llm distill --input <file.json>` — distills expert facts from GitHub issues via LLM
- `gauntletci corpus maintainers fetch` — fetches high-signal PRs from top .NET OSS contributors

## Key Conventions
- Do NOT include a Co-authored-by trailer in commit messages
- No markdown files committed except README.md and files listed in .gitignore exceptions
- Live LLM tests skipped without ANTHROPIC_API_KEY or OPENAI_API_KEY env vars
- Banner suppression: CI env vars (CI, GITHUB_ACTIONS, TF_BUILD, BUILD_BUILDID, JENKINS_URL)

## Commit Tags
Prefix commit messages with a tag in brackets:

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
