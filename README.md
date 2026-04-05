# GauntletCI

[![CI](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml/badge.svg)](https://github.com/EricCogen/GauntletCI/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/GauntletCI.svg)](https://www.nuget.org/packages/GauntletCI)
[![License: Elastic-2.0](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

Pre-commit change-risk detection for pull requests.

GauntletCI evaluates your diff before it lands and answers one question:

Did this change introduce behavior that is not properly validated?

---

# Why this exists

Code review checks intent.  
Tests check correctness.  

Neither answers:

Did this change introduce behavior that is not properly validated?

Most production issues are not caused by syntax errors.

They are caused by small changes that looked safe and were not fully validated.

---

# What this catches

## Looks like cleanup, introduces a race condition

A small diff removes locking.

Code gets simpler. Nothing else changes.

Now concurrent requests can corrupt shared state.

![GauntletCI example](./gauntletci-example.png)

---

## Looks equivalent, changes runtime behavior

Async code becomes blocking. Error handling is simplified.

It still works. It still compiles.

But behavior under load and failure conditions is no longer the same.

---

## Tests pass, production gets slower

Cached data is replaced with per-request IO.

Tests pass. Output is correct.

Latency, throughput, and cost degrade in production.

---

# Where this fits

Most tools focus on:

- Code correctness (tests)
- Code quality (linters, static analysis)
- Code suggestions (AI review tools)

GauntletCI focuses on something different:

Behavioral risk introduced by a change.

---

# How this compares

Most developer tools analyze code.

GauntletCI analyzes what changed.

- Tests verify expected outcomes
- Linters enforce rules
- Static analysis inspects code structure
- AI review tools suggest improvements

GauntletCI highlights where behavior may have changed without sufficient validation.

---

# What it does

GauntletCI evaluates staged or full diffs and returns:

- evidence-backed findings
- affected files and locations
- why the change matters
- suggested validation actions

---

# What the model is used for

The model is used to:

- interpret diffs in context
- reason about behavioral impact
- explain why a change may be risky
- suggest validation steps

It does not generate code.

It is used as a reasoning layer on top of deterministic rule detection.

---

# What this is

A diff-first system that surfaces behavior changes that are likely not fully validated.

It does not try to prove correctness.

It highlights uncertainty.

---

# What this is not

Not a linter  
Not static analysis  
Not code generation  

Does not replace code review or tests

---

# Installation

dotnet tool install -g GauntletCI

---

# Quickstart

GauntletCI needs minimal configuration to understand:

- how to run your tests
- which rules should block a commit
- which model (remote or local) to use for analysis

Works with local models for offline or cost-controlled usage.

1. Create or edit `.gauntletci.json`:

```json
{
  "test_command": "dotnet test",
  "disabled_rules": [],
  "blocking_rules": ["GCI012", "GCI004"],
  "telemetry": true,
  "model": "claude-sonnet-4-6"
}
```

2. Configure a model provider

Supported:

- Hosted models (Anthropic, OpenAI, compatible APIs)
- Local models (Ollama or any OpenAI-compatible endpoint)

Example (Anthropic):

set ANTHROPIC_API_KEY=your_key_here

Example (Ollama):

- Install Ollama: https://ollama.com
- Run a model locally:
  ollama run llama3
- Configure endpoint in your environment or config

3. Run:

gauntletci

Optional:

gauntletci --full  
gauntletci --rule GCI012  
gauntletci --format json  

---

# License

GauntletCI is source-available under the Elastic License 2.0 (ELv2).

This means you can use, modify, and distribute the source freely except:

- You may not offer GauntletCI as a managed service or SaaS product.

If you are building an internal tool or using the CLI directly, ELv2 places no restrictions on you.
