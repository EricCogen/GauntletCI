# GauntletCI

<!-- badges -->
[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI)
[![NuGet downloads](https://img.shields.io/nuget/dt/GauntletCI)](https://www.nuget.org/packages/GauntletCI)
[![License](https://img.shields.io/badge/license-ELv2-blue)](LICENSE)

---

Your tests passed. Your PR was approved. Your change still broke production.

Tests confirm existing behavior. Code review confirms intent.  
Neither validates what your change actually does.

**GauntletCI analyzes pull request diffs and flags unverified behavioral changes before they reach code review** - logic shifts, removed guards, silent regressions, and hidden contract breaks that pass tests and reviewers alike.

- ⚡ Sub-second analysis: no compilation, no AST, no network
- 🔒 Runs locally: no code leaves your machine
- 🎯 Up to 3 high-signal findings per run, no noise
- 🔢 Fully deterministic: 30+ rules, no LLM required

---

## Quick Start

```bash
dotnet tool install -g GauntletCI
gauntletci analyze --staged
```

Requires .NET 8+. Also available via [self-contained binaries](https://gauntletci.com/docs).

> Running against StackExchange.Redis PR#2995 - GauntletCI flags a swallowed exception in production connection handling.

[![GauntletCI terminal demo](https://raw.githubusercontent.com/EricCogen/GauntletCI/refs/heads/main/site/public/gauntletci-terminal-demo.gif)](https://github.com/EricCogen/GauntletCI)

---

## What it catches

A single line removed from a production service:

```diff
 public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
 {
-    if (request is null) throw new ArgumentNullException(nameof(request));
     var order = new Order(request.CustomerId, request.Items);
     return await _repo.SaveAsync(order);
 }
```

Tests passed. PR approved ("cleaned up redundant null check"). Shipped.

Callers relying on the early `ArgumentNullException` now receive a `NullReferenceException` deeper in the call stack, with no context.

GauntletCI flags it before the commit is created:

```
[High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
longer thrown on null input. Callers relying on this contract will see
NullReferenceException deeper in the call stack.
```

---

## See it live - before installing anything

The [GauntletCI-Demo repo](https://github.com/EricCogen/GauntletCI-Demo/pulls) is a realistic ASP.NET Core `OrderService` with 36 scenarios across 3 tiers:

- Tier 1: 6 headline scenarios (core rules)
- Tier 2: 12 single-rule scenarios (one rule per scenario)
- Tier 3: 18 behavioral regression scenarios with competitive analysis (GauntletCI 18/18 vs Competitors 0/18)

View the live demo results showing GauntletCI catches behavioral changes that CodeQL, Semgrep, SonarQube, StyleCop, and Snyk all miss:

[Browse live demo PRs](https://github.com/EricCogen/GauntletCI-Demo/pulls) | [View Tier 3 multi-tool comparison](https://github.com/EricCogen/GauntletCI-Demo/blob/main/DEMO_FINDINGS.md)

| PR | Scenario | Expected verdict |
|----|----------|-----------------|
| 01 | Safe typo fix | ✅ clean: no findings |
| 02 | Silent `catch { }` around payment call | ❌ GCI0007 Error Handling Integrity |
| 03 | Hardcoded API key in Program.cs | ❌ GCI0012 Secret Hygiene |
| 04 | `CancellationToken` dropped from `IPaymentClient` | ❌ GCI0004 Public API Contract |
| 05 | Customer email logged in `LogInformation` | ❌ GCI0029 PII Logging Leak |
| 06 | Static counter mutated without sync | ❌ GCI0016 Concurrency Safety |

---

## Validated against real-world projects

| Project | What GauntletCI caught |
|---------|----------------------|
| dotnet/efcore | O(n²) performance risk (LINQ in loops) |
| StackExchange.Redis | Context mutation in property getter |
| Dapper | Null-forgiving operator misuse |
| SharpCompress | Numeric overflow risk |
| AngleSharp | Enum member removal breaking serialization |

---

## Add to GitHub Actions

```yaml
- uses: EricCogen/GauntletCI@main
  with:
    fail-on-findings: "false"   # advisory mode while you tune signal
    inline-comments: "true"
```

Start in advisory mode. Once signal quality is tuned for your repo, set `fail-on-findings: "true"` to block risky changes.

→ [Full GitHub Actions reference](https://gauntletci.com/docs/integrations/github-action)

---

## Introducing GauntletCI into an existing codebase

```bash
gauntletci baseline create    # snapshot current state
gauntletci analyze --staged   # only new risks from your current change
```

Baseline delta mode means you get signal on what you're changing today, not noise from legacy code.

---

## Documentation

| | |
|--|--|
| [Getting Started](https://gauntletci.com/docs) | Install, first run, pre-commit hook |
| [Rule Library](https://gauntletci.com/docs/rules) | All 30+ detection rules with examples |
| [CLI Reference](https://gauntletci.com/docs/cli-reference) | All commands and flags |
| [Configuration](https://gauntletci.com/docs/configuration) | `.gauntletci.json` reference |
| [Local LLM Setup](https://gauntletci.com/docs/local-llm) | Optional explanation layer via Ollama |
| [CI/CD Integrations](https://gauntletci.com/docs/integrations) | GitHub Actions, Azure DevOps, GitLab, Bitbucket |

---

## License

[Elastic License 2.0](LICENSE). Free for individuals and teams. Built by [Eric Cogen](https://gauntletci.com/about).
