# GauntletCI
<div><img src="assets/images/GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

[![GauntletCI](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/EricCogen/db3979f1a5d69ce37d425b73bdcf4ada/raw/gauntletci-badge.json)](https://github.com/EricCogen/GauntletCI)
[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![NuGet downloads](https://img.shields.io/nuget/dt/GauntletCI?label=NuGet)](https://www.nuget.org/packages/GauntletCI)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

---

**Your tests passed. Your PR was approved. Your change still broke production.**

Tests confirm existing behavior. Code review confirms intent. **Neither validates what your change actually does.**

GauntletCI detects [**Behavioral Change Risk**](docs/change-risk-thesis.md) in pull request diffs: logic shifts, missing validations, and hidden regressions that compile cleanly, pass every test, and survive code review.

---

## What is GauntletCI?

GauntletCI is a diff-first **Behavioral Change Risk** detector for .NET that analyzes what changed, not your full codebase.

- **Sub-second analysis** - No compilation, no network
- **100% Local** - No code leaves your machine  
- **Deterministic** - Same diff, same findings, every time
- **High-signal** - Designed to surface 2–3 findings per run

---

## The Missing Layer

Modern pipelines answer different questions:

| Layer | Question |
| --- | --- |
| Static analysis | Is this code well-formed? |
| Security scanning | Does this code contain vulnerabilities? |
| Tests | Does this code match expected behavior? |
| Code review | Does this change match intended behavior? |

**None ask: is the behavioral impact of this change verified?**

GauntletCI is that layer. It doesn't replace the others—it closes the gap none of them cover.

**→ [Full Architecture Guide](docs/architecture.md) | [Technical FAQ](docs/FAQ.md)**

---

## The Change That Looked Safe

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

Callers relying on the early `ArgumentNullException` now receive a `NullReferenceException` deeper in the call stack. The change shipped.

**GauntletCI flagged it before the commit was created:**

```
[High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
longer thrown on null input. Callers relying on this contract will see
NullReferenceException deeper in the call stack.
```

This is Behavioral Change Risk: a change that compiles, passes tests, and passes review, but alters runtime behavior in a way none of those checks can see.

---

## See It Live

The **[GauntletCI-Demo](https://github.com/EricCogen/GauntletCI-Demo)** repo shows GauntletCI's approach with 36 scenarios across 3 tiers:

- **Tier 1**: 6 core rule scenarios
- **Tier 2**: 12 single-rule scenarios  
- **Tier 3**: 18 behavioral regression scenarios

Each compiles cleanly, passes all tests, and would pass traditional SAST gates—but introduces behavioral risk only visible in diff-level analysis.

**[→ Browse live demo PRs](https://github.com/EricCogen/GauntletCI-Demo/pulls)**

---

## Quick Start

```bash
dotnet tool install -g GauntletCI

# Run before committing
gauntletci analyze --staged
```

Five minutes from install to first finding. No configuration required.

---

## What GauntletCI Detects

GauntletCI ships with 50+ deterministic rules organized across 8 production risk tiers:

- **Tier 1**: Structural & Scope Integrity
- **Tier 2**: Behavioral & Correctness Risk (control flow, method signatures)
- **Tier 3**: Security & Compliance (secrets, SQL injection, PII logging)
- **Tier 4**: Resource & Concurrency (async deadlocks, disposables, shared state)
- **Tier 5**: Observability & Failure (swallowed exceptions, removed logging)
- **Tier 6**: Evidence & Test Completeness
- **Tier 7**: Architecture & Structural Contracts
- **Tier 8**: Dependency & Integration Safety

**[View all rules →](docs/rules/README.md)**

---

## Real-World Case Studies

Learn from real incidents how GauntletCI detects behavioral changes:

### Security & Data Integrity
- **[GCI0029 - PII Logging Leak](docs/case-studies/gci0029-pii-exposure.md)** - 18 months of plaintext passwords, $2.1M GDPR settlement
- **[GCI0050 - SQL Column Truncation](docs/case-studies/gci0050-sql-truncation.md)** - Silent data loss during schema migrations
- **[GCI0048 - Insecure Random](docs/case-studies/gci0048-insecure-random.md)** - Predictable tokens enable account takeover

### Reliability & Concurrency
- **[GCI0054 - Async Void Abuse](docs/case-studies/gci0054-async-void-abuse.md)** - Stack Overflow outages from unhandled exceptions
- **GCI0016 - Concurrency Safety** - Race conditions cause data corruption

### API Design & Idempotency
- **[GCI0022 - Duplicate Charge](docs/case-studies/gci0022-duplicate-charge.md)** - 147 duplicate charges, $23K in refunds (missing idempotency)

**[View all case studies →](docs/case-studies/README.md)**

---

## Real Code Examples

See how GauntletCI detects risky patterns in real, open-source projects:

- **[AngleSharp - Enum Removal](docs/risky-diffs/anglesharp-enum-removal.md)** - Breaking change in public API
- **[Dapper - Null Forgiving](docs/risky-diffs/dapper-null-forgiving.md)** - Nullable reference type handling
- **[EF Core - LINQ Loop](docs/risky-diffs/efcore-linq-loop.md)** - Query optimization pitfall

**[View all examples →](docs/risky-diffs/README.md)**

---

## Add GauntletCI to GitHub Actions

Start in advisory mode first:

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

Once signal quality is tuned, change `fail-on-findings` to `"true"` to block risky merges.

---

## Baseline Delta Mode

Introducing GauntletCI into an existing codebase with legacy issues?

```bash
gauntletci baseline create
gauntletci analyze --staged
```

Only new risks are shown. Legacy findings are suppressed until you address them.

---

## What to Do with a Finding

A GauntletCI finding signals that behavioral impact is unverified—not that code is definitely broken.

1. Confirm whether the behavior actually changed
2. Check whether tests or validation cover the changed path
3. Add validation, update tests, or document why the change is intentional
4. Suppress only when the risk is understood and accepted

---

## What GauntletCI is Not

- Not a linter
- Not a static analysis replacement
- Not a test runner
- Not a formatter
- Not a general code quality tool

GauntletCI has one job: detect unverified **Behavioral Change Risk** in the diff.

---

## When No Findings Are Detected

No Behavioral Change Risk signals were identified. This doesn't guarantee correctness—it means no high-confidence risks were found by the current rule set.

---

## Privacy

- All analysis runs locally
- No code leaves your machine
- Auto-redaction prevents sensitive data in output
- Telemetry is opt-in

---

## Help & Troubleshooting

**[Troubleshooting Guide](docs/TROUBLESHOOTING.md)** - Common problems and solutions

**[Advanced Tuning](docs/troubleshooting/phase-21-tuning.md)** - Rule sensitivity, performance optimization

---

## Documentation & Links

### Getting Started
- **[Documentation Hub](docs/)** - Full documentation index
- **[CLI Reference](docs/cli-reference.md)** - Complete command-line usage
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute

### Learn More
- **[Features & Benefits](docs/features-benefits.md)** - Complete feature matrix
- **[Architecture Guide](docs/architecture.md)** - Technical deep-dive
- **[Technical FAQ](docs/FAQ.md)** - Detailed Q&As
- **[Behavioral Change Risk Research](docs/change-risk-research.md)** - Academic foundations

### Deployment & Security
- **[Security Policy](SECURITY.md)** - Vulnerability reporting
- **[Support](SUPPORT.md)** - Getting help
- **[Release Notes](docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md)** - Current version

---

## Community

Questions? Ideas? Found a false positive?

- **Twitter**: [@GauntletCI_BCRV](https://twitter.com/GauntletCI_BCRV) - announcements and updates
- **GitHub Issues**: [Report bugs or request features](https://github.com/EricCogen/GauntletCI/issues)
- **GitHub Discussions**: [Ask questions and share ideas](https://github.com/EricCogen/GauntletCI/discussions)

---

## License

Elastic License 2.0
