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
