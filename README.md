# GauntletCI

**Your tests passed. Your PR was approved. Your change still broke production.**

Tests confirm existing behavior. Code review confirms intent. **Neither validates what your change actually does.**

GauntletCI detects **Behavioral Change Risk** in pull request diffs: logic shifts, missing validations, and hidden regressions that compile cleanly, pass every test, and survive code review — before the commit is created.

---

## Table of Contents

- [The Missing Layer](#the-missing-layer)
- [Quick Start](#quick-start)
- [What GauntletCI Detects](#what-gauntletci-detects)
- [How It Compares](#how-it-compares)
- [See It Live](#see-it-live)
- [GitHub Actions](#github-actions)
- [Documentation](#documentation)
- [Community](#community)
- [License](#license)

---

## The Missing Layer

Modern pipelines answer different questions:

| Layer | Question answered |
|---|---|
| Static analysis | Is this code well-formed? |
| Security scanning | Does this code contain known vulnerabilities? |
| Tests | Does this code match expected behavior? |
| Code review | Does this change match intended behavior? |
| **GauntletCI** | **Is the behavioral impact of this change verified?** |

GauntletCI doesn't replace any of these. It closes the gap none of them cover.

---

## Quick Start

```bash
dotnet tool install -g GauntletCI

# Run against staged changes before committing
gauntletci analyze --staged
```

Five minutes from install to first finding. No configuration required.

**→ [Full install guide](docs/install.md) | [CLI reference](docs/cli-reference.md)**

---

## What GauntletCI Detects

37 deterministic rules across production risk tiers:

| Tier | Category | Example |
|---|---|---|
| 1 | Structural & Scope Integrity | Visibility changes, signature drift |
| 2 | Behavioral & Correctness Risk | Control flow changes, removed guard clauses |
| 3 | Security & Compliance | Secrets in diffs, SQL injection exposure, PII logging |
| 4 | Resource & Concurrency | Async deadlocks, undisposed resources, shared state |
| 5 | Observability & Failure | Swallowed exceptions, removed logging from error paths |
| 6 | Evidence & Test Completeness | Behavior change with no corresponding test delta |
| 7 | Architecture & Structural Contracts | Interface violations, coupling changes |
| 8 | Dependency & Integration Safety | Version conflicts, breaking API surface changes |

Detection is fully deterministic. Same diff, same findings, every time. No LLM evaluates whether a rule fires.

**→ [Full rule catalog](docs/rules/README.md)**

---

## How It Compares

| | GauntletCI | CodeRabbit | Copilot Code Review | SonarQube |
|---|---|---|---|---|
| Deterministic findings | ✅ | ❌ | ❌ | ✅ |
| Behavioral change detection | ✅ | Partial | Partial | ❌ |
| Test coverage gap detection | ✅ | ❌ | ❌ | ❌ |
| Runs pre-commit (local) | ✅ | ❌ | ❌ | ❌ |
| 100% local / no data egress | ✅ | ❌ | ❌ | ✅ |
| .NET-native | ✅ | ❌ | ❌ | ✅ |
| AI explanations available | ✅ opt-in | ✅ | ✅ | ❌ |

LLM explanations are available as an opt-in layer. The detection logic itself never involves one.

---

## See It Live

The **[GauntletCI-Demo](https://github.com/EricCogen/GauntletCI-Demo)** repo contains 36 scenarios across 3 tiers — each compiling cleanly, passing all tests, and passing traditional SAST gates, while introducing behavioral risk only visible at the diff level.

**[→ Browse live demo PRs](https://github.com/EricCogen/GauntletCI-Demo/pulls)**

---

## GitHub Actions

Start in advisory mode. Inline comments surface findings without blocking merges:

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
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: EricCogen/GauntletCI@main
        with:
          fail-on-findings: "false"
          inline-comments: "true"
```

Once signal quality is tuned for your codebase, set `fail-on-findings: "true"` to block risky merges.

---

## Documentation

| | |
|---|---|
| [Documentation Hub](docs/) | Full documentation index |
| [CLI Reference](docs/cli-reference.md) | Complete command-line usage |
| [Architecture Guide](docs/architecture.md) | How detection works |
| [Technical FAQ](docs/FAQ.md) | Common questions |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common problems and solutions |
| [Contributing](CONTRIBUTING.md) | How to contribute |
| [Security Policy](SECURITY.md) | Vulnerability reporting |

---

## Community

Questions? Ideas? Found a false positive?

- **GitHub Issues**: [Report bugs or request features](https://github.com/EricCogen/GauntletCI/issues)
- **GitHub Discussions**: [Ask questions and share ideas](https://github.com/EricCogen/GauntletCI/discussions)
- **Twitter**: [@GauntletCI_BCRV](https://twitter.com/GauntletCI_BCRV)

---

## License

[Elastic License 2.0](LICENSE) — free for personal and internal use.
