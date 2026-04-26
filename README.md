# GauntletCI
<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

![GauntletCI](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/EricCogen/db3979f1a5d69ce37d425b73bdcf4ada/raw/gauntletci-badge.json)
[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

---

**Your tests passed. Your PR was approved.
Your change still broke production.**

Tests confirm existing behavior.
Code review confirms intent.

**Neither validates what your change actually does.**

GauntletCI detects **Behavioral Change Risk** in pull request diffs, identifying logic shifts,
missing validations, and hidden regressions that pass tests and code review.

---

## 🚀 What is GauntletCI?

**GauntletCI** is a pre-commit, diff-first change-risk detection tool.

It analyzes what changed in your code and flags **unverified behavioral changes**
before they reach code review.

* ⚡ Sub-second analysis — no compilation, no AST, no network
* 🔒 Runs locally — no code leaves your machine
* 🎯 High-signal output — designed to surface 0–3 findings per run

It answers one question:

> Did this change introduce behavior that is not properly validated?

GauntletCI detects **Behavioral Change Risk**: unverified behavior changes introduced by a diff.

---

## ⏱ What you get in 5 minutes

* Install the tool
* Run it on your current changes
* See 0–3 high-signal findings (or none)

No setup required.

---

## 🎬 See it live

Want to see GauntletCI catch real bugs in real PRs before installing anything?

The **[GauntletCI-Demo](https://github.com/EricCogen/GauntletCI-Demo)** repo
is a realistic ASP.NET Core OrderService with **6 always-open scenario PRs**.
Each PR makes a plausible multi-file change with a single risky line buried
inside. GauntletCI runs on every PR — open one and read the workflow output:

| PR | Scenario | Expected verdict |
| --- | --- | --- |
| 01 | Safe typo fix | ✅ clean — no findings |
| 02 | Silent `catch { }` around payment call | ❌ GCI0007 Error Handling Integrity |
| 03 | Hardcoded API key in `Program.cs` | ❌ GCI0012 Secret Hygiene |
| 04 | `CancellationToken` dropped from `IPaymentClient` | ❌ GCI0004 Public API Contract |
| 05 | Customer email logged in `LogInformation` | ❌ GCI0029 PII Logging Leak |
| 06 | Static counter mutated without sync | ❌ GCI0016 Concurrency Safety |

**[→ Browse the live demo PRs](https://github.com/EricCogen/GauntletCI-Demo/pulls)**

Want to drive it yourself? **[Fork or clone GauntletCI-Demo](https://github.com/EricCogen/GauntletCI-Demo#run-it-yourself-recommended)**
and run the scenarios on your own copy — the demo repo's README has a one-click
fork-and-run path plus a local-CLI walkthrough.

---

## 📖 Why This Exists

Tests and code review do not reliably validate behavioral changes.

Even experienced developers miss things in diffs.

Not because they lack skill — but because diffs are deceptive.

A small change can silently alter behavior:

* A null check changes execution flow
* A guard clause introduces new exceptions
* A method signature changes without test updates
* A dependency call is modified without validation
* A conditional branch shifts logic

These are not syntax errors.
They are **behavior changes** — and they regularly slip through code review.

---

## The Missing Layer: Change Validation

Modern development pipelines have strong tooling, but each layer answers a different question:

- Static analysis checks code quality
- Security tools check vulnerabilities
- Tests verify expected behavior
- Code review checks intent

**None of them validate the behavioral impact of a change.**

GauntletCI introduces a new layer: **Behavioral Change Risk detection**

It focuses only on the delta between versions and asks:

> Is this change safe?

---

## The Change That Looked Safe

A single line was removed from a production service:

```diff
 public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
 {
-    if (request is null) throw new ArgumentNullException(nameof(request));
     var order = new Order(request.CustomerId, request.Items);
     return await _repo.SaveAsync(order);
 }
```

- 1 line removed
- Tests passed
- PR approved ("cleaned up redundant null check")

Callers relying on the early `ArgumentNullException` now receive a `NullReferenceException`
deeper in the call stack, with no context. The change shipped.

GauntletCI flagged it before the commit was created:

```
[High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
longer thrown on null input. Callers relying on this contract will see
NullReferenceException deeper in the call stack.
```

This is Behavioral Change Risk: a change that compiles, passes tests, and passes review --
but alters runtime behavior in a way none of those checks can see.

---

## 🏆 Proven Reliability

GauntletCI rules have been validated against real-world pull requests:

| Project                 | What GauntletCI Caught                     |
| ----------------------- | ------------------------------------------ |
| **dotnet/efcore**       | O(n²) performance risk (LINQ in loops)     |
| **StackExchange.Redis** | Context mutation in property getter        |
| **Dapper**              | Null-forgiving operator misuse             |
| **SharpCompress**       | Numeric overflow risk                      |
| **AngleSharp**          | Enum member removal breaking serialization |

---

## ⚡ Quick Start

```bash
dotnet tool install -g GauntletCI

# Run before committing
gauntletci analyze --staged
```

---

## 🧪 What you see on first run

![GauntletCI terminal demo](docs/assets/gauntletci-terminal-demo.gif)

> Running against [StackExchange.Redis PR#2995](https://github.com/StackExchange/StackExchange.Redis/pull/2995) - GauntletCI flags a swallowed exception in production connection handling.
> GIF recorded with [ScreenToGif](https://github.com/NickeManarin/ScreenToGif) (open source)

Typical output includes **0-3 high-signal findings**.

---

## 🔇 Designed for high signal

GauntletCI avoids noise by design:

* Diff-only analysis (only what changed)
* No style or formatting checks
* Focused on behavioral risk only
* Baseline suppression for legacy code

---

## 📊 Baseline Delta Mode

Introduce GauntletCI into any codebase without noise:

```bash
gauntletci baseline create
gauntletci analyze --staged
```

Only **new risks introduced by the current change** are shown.

---

## 🚀 What it detects

### Behavior & Contract Safety

* Behavior changes without tests
* API and serialization changes

### Data & State Integrity

* Numeric truncation / overflow risks
* State mutation issues

### Async & Resource Safety

* Blocking async calls
* Disposable leaks

### Security & Privacy

* SQL injection risks
* Hardcoded secrets
* PII exposure (auto-redacted)

### Observability & Failure Handling

* Missing logging
* Silent failures

---

## 📏 Detection Coverage

GauntletCI includes **35 built-in detection rules** across:

* Behavior & Contracts
* Security
* Data Integrity
* Async & Concurrency
* Observability
* Architecture
* Test Quality

Rule IDs range from GCI0001-GCI0050. Rule IDs are non-contiguous because the rule set evolved over time: some early rules were retired, merged, or replaced as the engine matured. The gaps reflect that history. Existing rule IDs are never renumbered so that baseline fingerprints and suppression annotations remain stable across upgrades.

---

## Add GauntletCI to GitHub Actions

Start in advisory mode first so your team can review findings before blocking merges.

Create `.github/workflows/gauntletci.yml`:

```yaml
name: GauntletCI

on:
  pull_request:

permissions:
  contents: read
  pull-requests: write

jobs:
  risk-analysis:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0

      - uses: EricCogen/GauntletCI@main
        with:
          fail-on-findings: "false"
          inline-comments: "true"
```

Once the signal quality is tuned for your repo, change `fail-on-findings` to `"true"` to block risky changes.

## GitHub Action inputs

| Input | Default | Description |
| --- | --- | --- |
| `commit` | PR head commit | Commit SHA to analyze |
| `no-llm` | `true` | Run deterministic rules only |
| `fail-on-findings` | `true` | Fail the check when findings are produced |
| `inline-comments` | `false` | Post findings as inline PR comments |
| `ascii` | `true` | Use ASCII-only output |
| `dotnet-version` | `8.0.x` | .NET SDK version |
| `gauntletci-version` | `2.0.0` | NuGet tool version to install |

---

## ⚡ Most common usage

```bash
gauntletci analyze --staged
gauntletci analyze --commit <sha>
```

---

## ❌ What it is not

* Not a linter
* Not a static analysis replacement
* Not a test runner
* Not a formatter

GauntletCI focuses only on **change-risk**, not general code quality.

---

## ⚠️ When no findings are detected

* No change-risk signals were identified
* This does not guarantee correctness
* It indicates no high-confidence risks were found

---

## What to do with a finding

A GauntletCI finding is not a claim that the code is definitely broken.

Treat it as a review prompt:

1. Confirm whether the behavior changed.
2. Check whether tests or validation cover the changed path.
3. Add validation, update tests, or document why the change is intentional.
4. Suppress only when the risk is understood and accepted.

---

## 🤖 Local LLM Integration (Optional)

LLM integration enhances explanation only.

* All detection logic is deterministic
* Runs locally via Ollama
* No data leaves your machine

---

## 🔒 Privacy

* All analysis runs locally
* No code leaves your machine
* Auto-redaction prevents sensitive data exposure
* Telemetry is optional and anonymous

---

## 📄 License

Elastic License 2.0
