# GauntletCI

GauntletCI is a pre-commit code review tool that runs a single LLM-backed audit over a changeset before commit. It applies 18 evidence-driven rules, blocks on deterministic pre-flight failures first, and returns structured findings using `GCI001` through `GCI018`.

## What Is In This Repo

- `src/GauntletCI.Core` - Shared evaluation engine, gates, prompt assembly, model client, finding parsing, config loading, and telemetry emission
- `src/GauntletCI.Cli` - Console app and .NET global tool entry point (`gauntletci`)
- `src/GauntletCI.CopilotExtension` - Shared command processor for Copilot-style review, explain, and status flows backed by Core
- `src/GauntletCI.PrIntegration` - Pull request orchestration and GitHub API publishing logic
- `src/GauntletCI.PrIntegration.Host` - Minimal webhook host for GitHub pull request events
- `tests/GauntletCI.Core.Tests` - Core tests
- `tests/GauntletCI.Cli.Tests` - CLI tests

## Current Capabilities

- Branch currency gate: aborts if the current branch is behind upstream
- Test passage gate: runs configured or auto-detected test command before model evaluation
- Single model call per evaluation
- Structured JSON finding parsing
- Rule IDs use `GCI` prefixes, for example `GCI012`
- CLI review flow, config flow, and pre-commit hook installation
- Copilot command surface for `review`, `explain`, and `status`
- PR review orchestration with status-check and review-comment publishing hooks
- Local .NET tool packaging

## Prerequisites

- .NET 8 SDK
- Git
- A configured upstream branch for branch-currency checks
- One model API key in the environment:
	- `ANTHROPIC_API_KEY` for Claude models
	- `OPENAI_API_KEY` for OpenAI models

## Configuration

Repo configuration lives in `.gauntletci.json`.

Current default repo config:

```json
{
	"test_command": "dotnet test",
	"disabled_rules": [],
	"blocking_rules": ["GCI012", "GCI004"],
	"telemetry": true,
	"model": "claude-sonnet-4-5"
}
```

User configuration lives at `~/.gauntletci/config.json` and stores telemetry consent plus default model settings.

## Build And Test

```powershell
dotnet restore GauntletCI.slnx
dotnet build GauntletCI.slnx
dotnet test GauntletCI.slnx
```

## Pack And Install The Tool

Build the local NuGet package:

```powershell
dotnet pack src/GauntletCI.Cli/GauntletCI.Cli.csproj -c Release
```

Install from the local package output:

```powershell
dotnet tool install -g --add-source ./artifacts GauntletCI --version 0.1.0
```

If the tool is already installed:

```powershell
dotnet tool update -g --add-source ./artifacts GauntletCI --version 0.1.0
```

## CLI Usage

```powershell
gauntletci
gauntletci --full
gauntletci --fast
gauntletci --rule GCI005
gauntletci --format json
gauntletci --no-telemetry
gauntletci config
gauntletci install
```

`gauntletci install` writes a `pre-commit` hook into `.git/hooks` that runs `gauntletci`.

## Copilot Command Surface

The shared Copilot command processor currently supports these command forms:

```text
@gauntletci review
@gauntletci review --rule GCI012
@gauntletci explain GCI007
@gauntletci status
```

## PR Integration

The PR integration is split into a reusable library and a webhook host:

- `GauntletCI.PrIntegration` fetches PR diffs, evaluates them through Core, and publishes review comments and a status check
- `GauntletCI.PrIntegration.Host` exposes a minimal HTTP endpoint for GitHub pull request webhooks

The webhook host expects `GITHUB_TOKEN` in the environment.

## Notes

- Rule identifiers are `GCI001` through `GCI018`
- The repo enforces a markdown policy in `.gitignore`; `README.md` is the only markdown file intended to be committed by default
- The pre-commit hook will fail if no supported model API key is configured in the environment
