# GauntletCI
<div><img src="assets/images/GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

[![GauntletCI](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/EricCogen/db3979f1a5d69ce37d425b73bdcf4ada/raw/gauntletci-badge.json)](https://github.com/EricCogen/GauntletCI)
[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![NuGet downloads](https://img.shields.io/nuget/dt/GauntletCI?label=NuGet)](https://www.nuget.org/packages/GauntletCI)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

---

**Your tests passed. Your PR was approved.
Your change still broke production.**

Tests confirm existing behavior.
Code review confirms intent.

**Neither validates what your change actually does.**

GauntletCI detects **Behavioral Change Risk** in pull request diffs: logic shifts, missing validations, and hidden regressions that compile cleanly, pass every test, and survive code review.

---

## What is GauntletCI?

GauntletCI is a diff-first, merge-time **Behavioral Change Risk** detector for .NET.

It analyzes what changed, not the full codebase, and flags changes whose behavioral impact is unverified before they reach production.

- Sub-second analysis: no compilation, no network calls
- Runs locally: no code leaves your machine
- Fully deterministic: every finding is reproducible, every time
- High-signal output: designed to surface up to 3 findings per run

It answers one question:

> Did this change introduce behavior that is not properly validated?

---

## The Missing Layer

Modern pipelines are strong. But each layer answers a different question:

| Layer | Question it answers |
| --- | --- |
| Static analysis | Is this code well-formed? |
| Security scanning | Does this code contain known vulnerabilities? |
| Tests | Does this code match expected behavior? |
| Code review | Does this change match intended behavior? |

**None of them ask: is the behavioral impact of this change verified?**

GauntletCI is that layer.

It does not replace static analysis, security scanning, tests, or code review. It closes the gap none of them cover.

---

## Technical Architecture: GauntletCI vs. Traditional CI

To understand why GauntletCI is fast and private, it helps to see how it works fundamentally differently from post-commit pipelines:

| Vector | Traditional CI Pipelines (Post-Commit) | GauntletCI (Pre-Commit Inner Loop) |
| --- | --- | --- |
| **Execution Trigger** | Push to remote branch / PR creation. | Local `git commit` hook or manual CLI invocation. |
| **Analysis Scope** | Whole-project compilation and full test-suite execution. | Staged Git diff isolation via localized Roslyn AST parsing. |
| **Feedback Loop** | 5 to 20+ minutes (Context switch required). | **Sub-second (<0.5s)** (Immediate local feedback). |
| **Data Privacy** | Code is transmitted to third-party cloud runners. | **100% Local.** No external API calls, zero data exfiltration. |
| **Target Risk** | Functional regressions (via unit/integration tests). | **Behavioral Change Risk (BCR)** (Silent exception paths, unverified logic). |

### How It Works: The Diff-Isolation Engine

Instead of executing a heavy build-and-scan pass, GauntletCI intercepts the inner loop at the syntax level:

```
[Staged Changes] ──> [Git Diff Extraction] ──> [Targeted Roslyn AST Parse] ──> [Deterministic Rule Evaluation]
                                                                                       │
  ┌─────────────────────────────────── COLD STOP ────────────────────────────────────────┤
  ▼                                                                                      ▼
[Risk Detected: Block Commit]                                                  [Clean: Pass to Git Engine]
```

1. **Extraction:** The engine queries the local Git index to isolate modified lines and files.
2. **Targeted Parsing:** Only the affected source files are loaded into syntax trees, omitting unchanged projects or assemblies.
3. **Rule Application:** 30+ deterministic rules (e.g., `GCI0003` for guard clause removal, `GCI0007` for swallowed exceptions) evaluate the structural delta between the pre-image and post-image of the code.

This approach means GauntletCI runs before you push, gives instant feedback, and never sees your code outside your machine.

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

Callers relying on the early `ArgumentNullException` now receive a `NullReferenceException` deeper in the call stack with no context. The change shipped.

GauntletCI flagged it before the commit was created:

```
[High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
longer thrown on null input. Callers relying on this contract will see
NullReferenceException deeper in the call stack.
```

This is Behavioral Change Risk: a change that compiles, passes tests, and passes review, but alters runtime behavior in a way none of those checks can see.

---

## Quick Start

```bash
dotnet tool install -g GauntletCI

# Run before committing
gauntletci analyze --staged
```

Five minutes from install to first finding. No configuration required.

---

## See It Live

Want to see GauntletCI's approach in action? Check out the live demo.

The **[GauntletCI-Demo](https://github.com/EricCogen/GauntletCI-Demo)** repo is a realistic ASP.NET Core OrderService with **36 scenarios across 3 tiers**:

- **Tier 1**: 6 headline scenarios covering core rules
- **Tier 2**: 12 single-rule scenarios (one rule isolated per scenario)  
- **Tier 3**: 18 behavioral regression scenarios showing GauntletCI's diff-based approach

### Understanding GauntletCI's Approach

GauntletCI detects **behavioral change risks** by analyzing what changed in your git diff, not by scanning the whole codebase.

**Traditional tools** (CodeQL, Semgrep, SonarQube, Snyk, StyleCop) scan the entire codebase during CI, looking for known vulnerability signatures and code quality patterns. They excel at finding explicit anti-patterns.

**GauntletCI** analyzes the specific diff during pre-commit in sub-seconds, detecting:
- Structural mutations (removed boundaries, changed sequences)
- Execution order changes that compile cleanly
- Behavioral regressions that pass all tests
- Context propagation losses in async code
- API contract drifts

These are the gaps that whole-project snapshot analysis can't efficiently detect during CI.

### The 18 Tier 3 Scenarios

These demonstrate the **class of issues GauntletCI detects that whole-project snapshot tools cannot see**:

| Category | Scenarios | What It Shows |
| --- | --- | --- |
| Architectural Access Control | S19, S23, S24 | Removal of boundary enforcement in the diff |
| Execution Sequence Changes | S20, S28-S30 | State mutation/external call reordering |
| Async Propagation Drops | S21, S25-S27 | CancellationToken context loss in call stacks |
| Public Contract Drift | S22, S31-S32 | Method signature/default parameter changes in diffs |
| Performance & Resource | S33-S34 | Configuration changes, pooling disablement |
| Dependency Injection Scope | S35-S36 | Scope boundary mismatches in DI config |

Each compiles cleanly, passes all tests, and would pass both SAST gates and linting checks—but introduces behavioral risk only visible in diff-level analysis.

**[→ Browse live demo PRs](https://github.com/EricCogen/GauntletCI-Demo/pulls)** | **[View detailed analysis](https://github.com/EricCogen/GauntletCI-Demo/blob/main/DEMO_FINDINGS.md)**

Sample Tier 1 scenarios:

| PR | Scenario | Expected verdict |
| --- | --- | --- |
| 01 | Safe typo fix | clean: no findings |
| 02 | Silent `catch { }` around payment call | GCI0007 Error Handling Integrity |
| 03 | Hardcoded API key in `Program.cs` | GCI0012 Secret Hygiene |
| 04 | `CancellationToken` dropped from `IPaymentClient` | GCI0004 Public API Contract |
| 05 | Customer email logged in `LogInformation` | GCI0029 PII Logging Leak |
| 06 | Static counter mutated without sync | GCI0016 Concurrency Safety |

---

## What it detects

GauntletCI ships with detection rules organized across 8 production risk tiers. The rule set is actively maintained: rules are added, refined, and occasionally retired as the engine matures.

### Tier 1: Structural & Scope Integrity
Changes that contaminate a diff with unrelated concerns, making behavioral review unreliable.

### Tier 2: Behavioral & Correctness Risk
Control-flow removals without test coverage. Method signature changes without contract updates. These are the changes most likely to produce silent regressions.

### Tier 3: Security & Compliance
Hardcoded secrets and infrastructure values. SQL injection patterns. Deprecated cryptography. PII written to log output.

### Tier 4: Resource & Concurrency Safety
Async deadlocks. Disposable leaks. Missing idempotency guarantees on retry-eligible endpoints. Unsafe shared state.

### Tier 5: Observability & Failure Handling
Swallowed exceptions. Removed error-level logging from error-handling paths. Silent failures that remove production visibility.

### Tier 6: Evidence & Test Completeness
New exception-throwing paths with no corresponding throw-assertion test coverage. Changes where the risk exists but no test evidence supports it.

### Tier 7: Architecture & Structural Contracts
Forbidden layer dependency violations. State mutation inside property getters, silent bugs in caching layers and serializers.

### Tier 8: Dependency & Integration Safety
Service locator anti-patterns. Direct HttpClient instantiation bypassing the connection pool. HTTP calls missing cancellation tokens. Test methods without assertions.

Rule IDs are non-contiguous (GCI0001-GCI0050). The gaps reflect rules that were retired, merged, or replaced as the engine matured. Existing rule IDs are never renumbered so that baseline fingerprints and suppression annotations remain stable across upgrades.

---

## Add GauntletCI to GitHub Actions

Start in advisory mode first so your team can review findings before blocking merges:

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

Once signal quality is tuned for your repo, change `fail-on-findings` to `"true"` to block risky merges.

### GitHub Action inputs

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

## Baseline Delta Mode

Introducing GauntletCI into an existing codebase with legacy issues? Create a baseline first:

```bash
gauntletci baseline create
gauntletci analyze --staged
```

Only **new risks introduced by the current change** are shown. Legacy findings are suppressed until you choose to address them.

---

## On Determinism

Every GauntletCI finding is produced by a deterministic rule. The same diff produces the same findings every time. There is no model inference in the detection path.

This is a deliberate architectural choice. A gate that blocks merges must be predictable. Non-deterministic blocking destroys CI trust.

Optional LLM integration (via Ollama, runs locally) adds plain-English explanation to findings after detection. It does not change what is flagged or why. All analysis runs locally: no code leaves your machine.

---

## What to do with a finding

A GauntletCI finding is not a claim that code is definitely broken. It is a signal that behavioral impact is unverified.

Treat it as a review prompt:

1. Confirm whether the behavior actually changed.
2. Check whether tests or validation cover the changed path.
3. Add validation, update tests, or document why the change is intentional.
4. Suppress only when the risk is understood and accepted.

---

## What GauntletCI is not

- Not a linter
- Not a static analysis replacement
- Not a test runner
- Not a formatter
- Not a general code quality tool

GauntletCI has one job: detect unverified **Behavioral Change Risk** in the diff.

---

## When no findings are detected

No Behavioral Change Risk signals were identified in the diff. This does not guarantee correctness: it means no high-confidence risks were found by the current rule set.

---

## Privacy

- All analysis runs locally
- No code leaves your machine
- Auto-redaction prevents sensitive data in finding output
- Telemetry is opt-in

---

## Documentation & Links

- **[Documentation Hub](docs/)** - Full documentation index
- **[Contributing Guide](docs/contributing.md)** - How to contribute
- **[Project Information](docs/project/)** - Charter, history, governance
- **[Security Policy](docs/security.md)** - Vulnerability reporting
- **[Support](docs/support.md)** - Getting help
- **[Release Notes](RELEASE_NOTES_v2.4.0-phase21-coordinations.md)** - Current version
- **[Deployment Guide](DEPLOYMENT_CHECKLIST_v2.4.0.md)** - Deployment instructions

---

## Community

Questions? Ideas? Found a false positive?

- **Twitter**: [@GauntletCI_BCRV](https://twitter.com/GauntletCI_BCRV) - announcements and updates
- **GitHub Issues**: [Report bugs or request features](https://github.com/EricCogen/GauntletCI/issues)
- **GitHub Discussions**: [Ask questions and share ideas](https://github.com/EricCogen/GauntletCI/discussions)

---

## FAQ & Architectural Trade-offs

### How can a diff-only tool have full context? Doesn't it miss the surrounding code?

GauntletCI uses Git to isolate *which* files have staged modifications, but it does not evaluate those files as raw text snippets. The engine loads the **entire source file** into a local Roslyn `SyntaxTree`.

This means rules have full structural context within the modified files. For example, if a rule flags a potential unhandled exception path introduced inside a modified code block, it automatically walks up the syntax tree node ancestors to check if an enclosing `try/catch` block further up the file already mitigates the risk.

### What about cross-project or semantic breaking changes?

Because GauntletCI optimizes for sub-second execution in the local pre-commit inner loop, it skips full project compilation and assembly linking.

* **What it catches:** Behavioral Change Risk (BCR) within the logical units you are actively modifying—such as silent exception leaks, algorithmic shifts, or structural regressions lacking test coverage.
* **What it leaves to your CI/Compiler:** Cross-assembly breaking changes (e.g., changing a method signature in Project A that breaks an uncompiled consumer in Project B).

GauntletCI is designed to act as a fast, localized safety net *before* your compiler or remote CI pipeline handles whole-project verification.

### We already use IDE Analyzers (SonarLint/Roslynators). Why do we need this?

Traditional analyzers evaluate the **state** of an entire codebase, which frequently leads to massive warning fatigue on legacy systems—developers end up suppressing or ignoring them.

GauntletCI evaluates the **delta**. It introduces a temporal constraint: it only executes rules against code that is actively being staged for a commit. It doesn't nag you about historical technical debt; it strictly prevents the introduction of *new, unvalidated behavioral risk paths*.

---

## Deep-Dive Technical FAQ

**For senior tech leads and architects:** These FAQs address technical objections based on validated GauntletCI behavior.

### How do you handle Roslyn `Compilation` dependencies without running a full project build? If you don't build, how do you resolve external symbols?

**The Hard Truth:** If you don't compile, you do not have a full `SemanticModel`. If a rule relies heavily on cross-project symbol resolution or binding external types, a pure syntax-isolated pass will face limitations.

**The Architecture:** GauntletCI is explicitly optimized for **structural and local semantic analysis**.

* For pure syntactic rules (e.g., catching structural changes without corresponding test alterations), the engine operates instantly on the `SyntaxTree`.
* For rules requiring basic type resolution, GauntletCI reads the target project file (`.csproj`) to resolve direct internal references and caches basic metadata without spinning up a heavy MSBuild workspace instance.
* If a behavioral rule *strictly* requires a fully bound, cross-assembly semantic graph to execute without false positives, that rule is intentionally excluded from the pre-commit engine and deferred to traditional post-commit compilation suites. We choose execution speed over multi-project type linking.

### Pre-commit hooks are notoriously hated because they slow down quick, iterative commits. How does GauntletCI compare?

**The Friction:** Tools like `dotnet format` or full-suite hooks are notorious inner-loop killers, often taking 5–15 seconds even on modest changes because they spin up the full .NET runtime context or scan the entire solution directory.

**The Performance:** GauntletCI executes in **<1.5 seconds** on typical staged changes.

* It bypasses the MSBuild workspace loader entirely.
* It reads the Git index directly, isolating *only* the specific files containing staged blocks.
* If you stage a 10-line change in a solution containing 150 projects and 10,000 files, Roslyn only parses the syntax trees for the *exact files* changed.

### How do you prevent "Hook Fatigue"? If the tool blocks a legitimate, high-velocity commit, developers will just run `git commit --no-verify`.

**The Friction:** The moment a pre-commit utility throws a false positive or interrupts a developer during an active debugging/hotfix flow, they will bypass it permanently.

**The Governance:** GauntletCI is engineered around a **Pessimistic, Non-Intrusive Model**.

* **Zero Style/Linting Rules:** The engine does not block commits for tabs vs. spaces, naming conventions, or formatting issues. It *only* blocks on explicit **Behavioral Change Risk (BCR)** indicators—such as adding complex conditional logic or exposing unhandled exception paths without adjusting associated verification files.
* **Granular Rule Suppression:** Teams can explicitly suppress a rule directly within the codebase using standard, granular comments (e.g., `// gauntlet-disable GCI0032`) or via a localized `.gauntletci` configuration file, allowing valid architectural exceptions without bypassing the entire gate.

### We have massive codebases with complex solution structures (multi-targeting, generated code, shared source files). How does GauntletCI parse these without choking?

**The Architecture:**

* **Generated Code Exclusion:** By default, the engine uses strict pattern-matching to automatically ignore files containing standard autogenerated headers (e.g., Protobuf outputs, EF Core Migrations, Source Generators, `*.designer.cs`). This ensures your change-risk metrics are never skewed by machine-written infrastructure.
* **Shared/Multi-Targeted Files:** Because GauntletCI evaluates code at the syntactic AST layer rather than loading a compiled assembly binary, it parses conditional compilation symbols (`#if NET8_0`, `#if WINDOWS`) structurally. A rule evaluates the syntax paths within all defined blocks, ensuring coverage regardless of the underlying compilation target configuration.

### How does GauntletCI recognize refactorings like Extract Method? Won't it flag legitimate code movements as "new unverified logic"?

**The Pattern Recognition:** When a developer uses IDE refactoring tools (like "Extract Method"), code blocks are moved but not behaviorally altered.

**The Engine Behavior:** GauntletCI evaluates **structural signatures and syntactic equivalence**. When it detects code movement without semantic alteration—such as extracting 50 lines of logic into a new private method—the engine recognizes the refactoring pattern and avoids throwing false-positive behavioral change alerts.

This means developers can refactor freely without fighting false positives.

### How does GauntletCI treat equivalent logic written in different C# syntax styles?

**The Challenge:** A rule states that complex branching logic requires corresponding test updates. One developer writes nested `if/else` statements; another writes a compact C# switch expression using relational patterns. To a basic analyzer, these look completely different.

**The Engine Resolution:** GauntletCI does not evaluate raw token patterns. It analyzes **logical execution paths**.

* Whether the branching logic is written as an elegant C# switch expression, a traditional switch block, or a chain of ternary operators, the engine maps the syntax nodes to their underlying logical execution paths.
* If the number of discrete execution paths increases within the modified code, the engine registers a behavioral change signal, regardless of syntactic style.

### What happens if the code inside the staged Git diff contains syntax errors?

**The Edge Case:** A developer is mid-refactor and attempts to make a partial commit of a file that currently has missing braces, unclosed parentheses, or broken expressions.

**The Resolution:** GauntletCI is engineered to be **Fault-Tolerant and Non-Crashing**.

* Roslyn's parser can build a full syntax tree even from broken source code by introducing "Diagnostic Nodes" to patch holes.
* When GauntletCI encounters severe syntax diagnostics, it evaluates severity. If code is too broken to reliably determine structural behavior, the engine gracefully aborts analysis for that file and permits the commit with a warning that validation was skipped.
* The tool never crashes or blocks a developer due to an unexpected parsing error.

### Does GauntletCI analyze only the modified lines, or does it load entire files?

**The Approach:** GauntletCI loads the **entire source file** into a local Roslyn `SyntaxTree`, but uses Git diff boundaries (`TextSpan`) to focus rule evaluation.

**Why:** This gives rules full structural context. For example, if a rule flags a potential unhandled exception path introduced inside a modified code block, it automatically walks up the syntax tree node ancestors to check if an enclosing `try/catch` block further up the file already mitigates the risk.

The Git index optimization ensures that only files with staged modifications are processed, keeping execution fast even on monorepos.

---

## License

Elastic License 2.0
