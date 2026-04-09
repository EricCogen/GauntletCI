<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

# GauntletCI

[![CI](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml/badge.svg)](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml)
[![License: Elastic-2.0](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

Pre-commit change-risk detection for pull requests.

------------------------------------------------------------------------

## The idea in one sentence

You changed what the code does.
Nothing proves it still works.

------------------------------------------------------------------------

## What GauntletCI does

GauntletCI evaluates your diff before it lands and answers one question:

Did this change introduce behavior that is not properly validated?

It focuses on behavioral risk introduced by change, not code quality or syntax.

------------------------------------------------------------------------

## Quickstart

Install:

```bash
dotnet tool install -g GauntletCI
```

Create `.gauntletci.json` in your repo root (optional):

```json
{
  "rules": {
    "GCI0002": { "enabled": true },
    "GCI0012": { "enabled": true }
  }
}
```

Run against a commit:

```bash
gauntletci analyze --commit <sha>
```

Run without the local LLM (deterministic rules only):

```bash
gauntletci analyze --commit <sha> --no-llm
```

Run in ASCII mode (for terminals without Unicode support):

```bash
gauntletci analyze --commit <sha> --ascii
```

------------------------------------------------------------------------

## Why this exists

Code review checks intent.
Tests check correctness.

Neither answers:

Did this change introduce behavior that is not properly validated?

Most production issues are not syntax errors.

They are small changes that looked safe and were not fully validated.

------------------------------------------------------------------------

## A real example

A small diff removes locking.

Code gets simpler. Nothing else changes.

Now concurrent requests can corrupt shared state.

This is the class of problem GauntletCI surfaces.

------------------------------------------------------------------------

## What makes this different

Most tools analyze code.

GauntletCI analyzes what changed.

- Tests verify outcomes
- Linters enforce rules
- Static analysis inspects structure
- AI tools suggest improvements

GauntletCI highlights where behavior may have changed without validation.

------------------------------------------------------------------------

## How GauntletCI Compares to Other Tools

Most tools answer one question well. GauntletCI answers a different one.

| Tool Type | What It Does | What It Misses | GauntletCI's Advantage |
|-----------|--------------|----------------|------------------------|
| **Unit / Integration Tests** | Verifies expected outcomes for specific scenarios | Behavior changes outside tested paths; untested edge cases | Flags when behavior changed but no new test covers that change |
| **Linters (ESLint, RuboCop, etc.)** | Enforces style, syntax, and basic code quality | Behavioral risk; semantic changes; logic flow | Catches *what* changed, not just *how it looks* |
| **Static Analysis (SonarQube, Roslyn)** | Finds code smells, potential bugs in the whole codebase | Diff-specific behavioral changes; assumes current code is the baseline | Analyzes the *difference* - what's new or removed |
| **AI Code Review (Copilot, Cursor, etc.)** | Suggests improvements, explains code, finds obvious bugs | Often misses validation gaps; focuses on style or known patterns | Specifically looks for behavior changes without proof of validation |
| **Test Coverage (Coverlet, JaCoCo)** | Measures which lines are executed by tests | 100% coverage doesn't mean behavior is correct; doesn't analyze *changes* | Points to changed lines that lack any test covering them |
| **GauntletCI** | **Evaluates the diff to find unvalidated behavioral changes** | Does not prove correctness; may have false positives | Fills the gap *between* "tests pass" and "behavior is still correct" |

### One-Line Summary

| Tool | The Gap It Leaves |
|------|-------------------|
| Tests | "You only test what you think to test." |
| Linters | "You can follow style and still break behavior." |
| Static Analysis | "The code is clean, but the change is risky." |
| AI Review | "Suggests improvements, but doesn't ask if you validated the change." |
| Coverage | "Every line runs, but does it do the right thing?" |
| **GauntletCI** | **"You changed what the code does. Nothing proves it still works."** |

------------------------------------------------------------------------

## Rules

GauntletCI ships 20 deterministic rules. Each rule produces evidence-backed findings with a confidence level.

| ID | Name | What It Detects |
|----|------|-----------------|
| GCI0001 | Diff Integrity | Truncated or corrupted diffs |
| GCI0002 | Goal Alignment | Files unrelated to the commit message |
| GCI0003 | Behavioral Change Detection | Logic removed without test changes; signature changes |
| GCI0004 | Breaking Change Risk | Public API surface changes |
| GCI0005 | Test Coverage Relevance | Changed files with no corresponding test changes |
| GCI0006 | Edge Case Handling | Missing null checks, boundary conditions |
| GCI0007 | Error Handling Integrity | Swallowed exceptions; empty catch blocks |
| GCI0008 | Complexity Control | Method length and complexity growth |
| GCI0009 | Consistency With Patterns | Deviations from established patterns in the codebase |
| GCI0010 | Hardcoding and Configuration | Magic numbers, hardcoded URLs, environment-specific strings |
| GCI0011 | Performance Risk | Queries in loops; unnecessary allocations |
| GCI0012 | Security Risk | SQL injection, hardcoded credentials, unvalidated input |
| GCI0013 | Observability / Debuggability | Missing logging on critical paths |
| GCI0014 | Rollback Safety | Changes without a clear rollback path |
| GCI0015 | Data Integrity Risk | Unsafe data mutations; missing transactions |
| GCI0016 | Concurrency and State Risk | Shared mutable state; missing synchronization |
| GCI0017 | Scope Discipline | Commits touching too many unrelated areas |
| GCI0018 | Production Readiness | Debug code, TODO comments, disabled feature flags |
| GCI0019 | Confidence and Evidence | Low-confidence changes lacking supporting context |
| GCI0020 | Accountability Standard | Exception swallowing, secrets in code, commented-out blocks |

------------------------------------------------------------------------

## What it returns

- Evidence-backed findings
- Affected files and locations
- Why the change matters
- Suggested validation actions

------------------------------------------------------------------------

## Model usage

The model is optional. It is used to:

- Interpret diffs in context
- Explain behavioral impact
- Suggest validation steps

Deterministic rules run first. Pass `--no-llm` to skip the model entirely and run only the 20 deterministic rules.

------------------------------------------------------------------------

## What this is

A diff-first system that surfaces behavior changes that may not be fully validated.

------------------------------------------------------------------------

## What this is not

Not a linter.
Not static analysis.
Not code generation.

Does not replace tests or code review.

------------------------------------------------------------------------

## Contributing

- [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md)
- [.github/CODE_OF_CONDUCT.md](.github/CODE_OF_CONDUCT.md)
- [.github/SECURITY.md](.github/SECURITY.md)

------------------------------------------------------------------------

Last Updated: 04/09/2026