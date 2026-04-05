# GauntletCI

[![CI](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml/badge.svg)](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/GauntletCI.svg)](https://www.nuget.org/packages/GauntletCI)
[![License: Elastic-2.0](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

**GauntletCI is a pre-commit change-risk gate.**

It evaluates your diff before it lands and answers one question:

> **Did this change introduce behavior that is not properly validated?**

## What it does

GauntletCI evaluates staged or full diffs against the GCI rule set and returns evidence-backed findings with suggested actions. It is optimized for review risks that often pass normal test suites and basic static checks: behavioral drift, missing validation, rollback hazards, observability loss, concurrency regressions, and dependency-risk changes.

## Requirements

- .NET SDK **8.0** (projects target `net8.0`)
- Git repository with staged or committed changes to evaluate
- One configured model provider (for example Anthropic/OpenAI key, or local compatible endpoint)
- Supported OS: **Windows, macOS, Linux**

## Installation

```bash
dotnet tool install -g GauntletCI
```

If you are running from source instead of a published package:

```bash
dotnet build GauntletCI.slnx
```

## Quickstart

1. Configure your repo-level `.gauntletci.json`:

```json
{
  "test_command": "dotnet test",
  "disabled_rules": [],
  "blocking_rules": ["GCI012", "GCI004"],
  "telemetry": true,
  "model": "claude-sonnet-4-6"
}
```

2. Export an API key for your selected model (example: Anthropic):

```bash
set ANTHROPIC_API_KEY=your_key_here
```

3. Run GauntletCI in your repository:

```bash
gauntletci
```

4. Optional modes:

```bash
gauntletci --full
gauntletci --rule GCI012
gauntletci --format json
```

## Real benchmark examples

- **Mixed-concern change set with hidden dependency risk**  
  `tests/GauntletCI.Benchmarks/Fixtures/curated/gci0001/09_real_true_positive_rust_yarn_check.diff`  
  [Full analysis](tests/GauntletCI.Benchmarks/README.md#example-1-mixed-concern-change-set-with-hidden-dependency-risk)

- **Async race condition from lock removal**  
  `tests/GauntletCI.Benchmarks/Fixtures/curated/gci0017/09_real_true_positive_race_condition_state_corruption.diff`  
  [Full analysis](tests/GauntletCI.Benchmarks/README.md#example-2-async-race-condition-from-lock-removal)

- **Observability loss with no compile/test breakage**  
  `tests/GauntletCI.Benchmarks/Fixtures/curated/gci0013/07_real_true_positive_dropped_correlation_id.diff`  
  [Full analysis](tests/GauntletCI.Benchmarks/README.md#example-3-observability-loss-with-no-functional-breakage)

## Configuration reference (`.gauntletci.json`)

| Field | Type | Default | Description |
|---|---|---|---|
| `test_command` | string | `dotnet test` | Command run by the test-passage gate before model evaluation. |
| `disabled_rules` | string[] | `[]` | Rules to suppress from output filtering. |
| `blocking_rules` | string[] | `[]` | Rules that force non-zero exit when they fire. |
| `telemetry` | boolean | `true` | Enables anonymous usage telemetry if consent has been recorded. |
| `model` | string | `claude-sonnet-4-6` | Preferred model identifier for evaluation. |

## Rule catalog

Default severity below is the baseline used for triage; model output may escalate or de-escalate severity based on concrete evidence in the diff.

| Rule ID | Rule name | Description | Default severity |
|---|---|---|---|
| GCI001 | Diff Integrity and Scope | Detect mixed concerns and incomplete companion changes that reduce reviewability. | medium |
| GCI002 | Goal Alignment | Check whether implementation behavior matches the apparent goal of the change. | medium |
| GCI003 | Behavioral Change Detection | Detect material control-flow/data-flow/side-effect behavior changes. | high |
| GCI004 | Breaking Change Risk | Catch caller-facing contract breaks and integration-impacting semantic shifts. | high |
| GCI005 | Test Coverage Relevance | Verify tests actually validate the changed behavior paths. | medium |
| GCI006 | Edge Case Handling | Find missing guards for nulls, boundaries, invalid input, and state extremes. | medium |
| GCI007 | Error Handling Integrity | Detect swallowed exceptions and context-losing error paths. | high |
| GCI008 | Complexity Control | Flag disproportionate complexity growth that raises defect probability. | medium |
| GCI009 | Consistency with Existing Patterns | Catch dangerous deviations from established repository patterns. | medium |
| GCI010 | Hardcoding and Configuration | Detect brittle hardcoded environment assumptions and sensitive literals. | high |
| GCI011 | Performance Risk | Detect obvious runtime regressions (N+1, repeated heavy work, blocking hot paths). | medium |
| GCI012 | Security Risk | Detect vulnerabilities and weakened security controls. | high |
| GCI013 | Observability and Debuggability | Detect loss of critical logs, traces, and correlation context. | medium |
| GCI014 | Rollback Safety | Detect irreversible or unsafe deployment and rollback behavior. | high |
| GCI015 | Data Integrity Risk | Detect silent corruption, mapping drift, and partial-write hazards. | high |
| GCI016 | Concurrency and State Risk | Detect race conditions, unsafe async patterns, and shared-state hazards. | high |
| GCI017 | Production Readiness | Synthesize aggregate risk into Ready / Needs Work / High Risk verdicts. | high |
| GCI018 | Accountability Standard | Apply senior ownership bar for unresolved quality risk. | medium |
| GCI019 | Duplication and DRY Discipline | Detect copy-paste logic growth and divergence risk. | medium |
| GCI020 | Dependency and Supply Chain Risk | Detect unsafe dependency/versioning and lockfile integrity regressions. | high |

## Repository layout notes

`GauntletCI.CopilotExtension` remains in the repository but is currently excluded from the default `GauntletCI.slnx` build scope while CLI/Core adoption is prioritized.

## License

GauntletCI is source-available under the [Elastic License 2.0 (ELv2)](LICENSE).

This means you can use, modify, and distribute the source freely **except**:

- You may not offer GauntletCI as a managed service or SaaS product.

If you are building an internal tool or using the CLI directly, ELv2 places no restrictions on you.
