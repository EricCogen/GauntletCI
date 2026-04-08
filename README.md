<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

# GauntletCI

[![CI](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml/badge.svg)](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml)
[![License:
Elastic-2.0](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

Pre-commit change-risk detection for pull requests.

------------------------------------------------------------------------

## The idea in one sentence

You changed what the code does.
Nothing proves it still works.

------------------------------------------------------------------------

## What GauntletCI does

GauntletCI evaluates your diff before it lands and answers one question:

Did this change introduce behavior that is not properly validated?

It focuses on behavioral risk introduced by change, not code quality or
syntax.

------------------------------------------------------------------------

## Quickstart

Install:

dotnet tool install -g GauntletCI

Create `.gauntletci.json`:

``` json
{
  "test_command": "dotnet test",
  "blocking_rules": ["GCI012", "GCI004"],
  "model_required": false
}
```

Run:
```bash
gauntletci
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

-   Tests verify outcomes
-   Linters enforce rules
-   Static analysis inspects structure
-   AI tools suggest improvements

GauntletCI highlights where behavior may have changed without
validation.

## How GauntletCI Compares to Other Tools

Most tools answer one question well. GauntletCI answers a different one.

| Tool Type | What It Does | What It Misses | GauntletCI’s Advantage |
|-----------|--------------|----------------|-------------------------|
| **Unit / Integration Tests** | Verifies expected outcomes for specific scenarios | Behavior changes outside tested paths; untested edge cases | Flags when behavior changed but no new test covers that change |
| **Linters (ESLint, RuboCop, etc.)** | Enforces style, syntax, and basic code quality | Behavioral risk; semantic changes; logic flow | Catches *what* changed, not just *how it looks* |
| **Static Analysis (SonarQube, Roslyn)** | Finds code smells, potential bugs in the whole codebase | Diff‑specific behavioral changes; assumes current code is the baseline | Analyzes the *difference* – what’s new or removed |
| **AI Code Review (Copilot, Cursor, etc.)** | Suggests improvements, explains code, finds obvious bugs | Often misses validation gaps; focuses on style or known patterns | Specifically looks for behavior changes without proof of validation |
| **Test Coverage (Coverlet, JaCoCo)** | Measures which lines are executed by tests | 100% coverage doesn’t mean behavior is correct; doesn’t analyze *changes* | Points to changed lines that lack any test covering them |
| **Performance Profilers** | Measures speed, memory, resource usage | Requires runtime data; doesn’t run pre‑commit | Works from diff alone – no execution needed |
| **GauntletCI** | **Evaluates the diff to find unvalidated behavioral changes** | Does not prove correctness; may have false positives | Fills the gap *between* “tests pass” and “behavior is still correct” |

### One‑Line Summary

| Tool | The Gap It Leaves |
|------|-------------------|
| Tests | “You only test what you think to test.” |
| Linters | “You can follow style and still break behavior.” |
| Static Analysis | “The code is clean, but the change is risky.” |
| AI Review | “Suggests improvements, but doesn’t ask if you validated the change.” |
| Coverage | “Every line runs, but does it do the right thing?” |
| **GauntletCI** | **“You changed what the code does. Nothing proves it still works.”** |

### Where GauntletCI Sits

- **Correctness (Tests)**  ← →  **Behavioral Risk (GauntletCI)**
- **Quality (Linters/SAST)** ← →  **Validation Gaps (GauntletCI)**
- **What the code IS**      ← →  **What CHANGED (GauntletCI)**

GauntletCI answers a question no other tool asks:  
*“Did this change introduce behavior that is not properly validated?”*
------------------------------------------------------------------------

## What it returns

-   Evidence-backed findings
-   Affected files and locations
-   Why the change matters
-   Suggested validation actions

------------------------------------------------------------------------

## Model usage

The model is optional.

It is used to: - interpret diffs in context
- explain behavioral impact
- suggest validation steps

Deterministic rules run first.

------------------------------------------------------------------------

## Experimental evidence

GauntletCI publishes reproducible benchmark evidence from the curated real-fixture corpus.

Generate the report locally:

```bash
dotnet run --project src/GauntletCI.BenchmarkReporter -- --repo-root . --output-dir docs/benchmarks
```

Published artifacts:

- `docs/benchmarks/latest.json` (overall metrics, per-rule metrics, case studies, fixture-level outcomes)
- `docs/benchmarks/latest.csv` (fixture-level export for external analysis)

Automation:

- `.github/workflows/benchmark-evidence.yml` runs report generation on schedule and manual dispatch and uploads the latest evidence artifacts.

------------------------------------------------------------------------

## Language coverage

GauntletCI is diff-first and language-agnostic.

Supported via extension normalization:

-   .cs → csharp
-   .ts/.tsx → typescript
-   .js → javascript
-   .py → python

Actively exercised languages:

C#, Go, Java, Python, Ruby, JavaScript, TypeScript/TSX, Rust, C/C
headers

------------------------------------------------------------------------

## What this is

A diff-first system that surfaces behavior changes that may not be fully
validated.

------------------------------------------------------------------------

## What this is not

Not a linter
Not static analysis
Not code generation

Does not replace tests or code review

------------------------------------------------------------------------

## Docs

-   [docs/change-risk-thesis.md](docs/change-risk-thesis.md)
-   [docs/change-risk-research.md](docs/change-risk-research.md)
-   [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md)
-   [.github/CODE_OF_CONDUCT.md](.github/CODE_OF_CONDUCT.md)
-   [.github/SECURITY.md](.github/SECURITY.md)

------------------------------------------------------------------------

Last Updated: 04/07/2026
