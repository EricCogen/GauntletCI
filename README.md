[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

# GauntletCI - Pre-commit change-risk detection for pull request diffs

**GauntletCI** is a .NET CLI tool that analyzes pull request diffs or as a pre-commit audit that detects **behavioral change-risk before code is merged**.

It answers one question:

> Did this change introduce behavior that is not properly validated?

---

## Why this exists

Even experienced developers miss things in diffs.

Not because they lack skill — but because diffs are deceptive.

A small change can silently alter behavior:

- A new null check changes execution flow
- A guard clause introduces new exceptions
- A method signature changes without test updates
- A dependency call is modified without validation
- A conditional branch shifts logic in subtle ways

These are not syntax errors.  
They are **behavior changes** — and they regularly slip through code review.

GauntletCI exists to catch them **before they reach production**.

---

## What GauntletCI is (and is not)

### ✅ What it is
- Diff-aware change-risk detector
- Pre-commit / pre-merge safety layer
- Focused on behavior, not style

### ❌ What it is not
- Not a linter
- Not a test runner
- Not a static analysis replacement
- Not a code formatter

GauntletCI complements your existing tools — it does not replace them.

---

## What it does

- Analyzes only **what changed** in a diff
- Detects **unvalidated behavior changes**
- Flags **missing or weak test coverage**
- Identifies **execution flow changes** (guards, exceptions, branching)
- Surfaces **API and contract changes**
- Outputs actionable findings with file paths and line numbers

---

## Rule catalog (36 rules, GCI0001–GCI0037)

| ID | Name | What it detects |
|----|------|-----------------|
| GCI0001 | Diff Integrity | Unrelated changes, formatting churn, mixed scope in a single diff |
| GCI0002 | Goal Alignment | Diffs unrelated to the commit message or spanning too many areas |
| GCI0003 | Behavioral Change Detection | Removed logic lines, changed method signatures without tests |
| GCI0004 | Breaking Change Risk | Removed public APIs, changed public method signatures |
| GCI0005 | Test Coverage Relevance | Code changes with no test changes; orphaned test-only changes |
| GCI0006 | Edge Case Handling | Potential null dereferences, missing parameter validation |
| GCI0007 | Error Handling Integrity | Swallowed exceptions, empty catch blocks |
| GCI0008 | Complexity Control | Excessive nesting, long methods, duplicate logic |
| GCI0009 | Consistency with Patterns | Async/await deviations from the project's established patterns |
| GCI0010 | Hardcoding and Configuration | Hardcoded IPs, URLs, connection strings, secrets, env names |
| GCI0011 | Performance Risk | Blocking async calls, N+1 patterns, inefficient allocations |
| GCI0012 | Security Risk | General security anti-patterns (injection, auth bypass signals) |
| GCI0013 | Observability/Debuggability | Removed error context logging, reduced trace coverage |
| GCI0014 | Rollback Safety | Missing rollback scripts alongside migration additions |
| GCI0015 | Data Integrity Risk | Data loss paths, unsafe null propagation in data operations |
| GCI0016 | Concurrency and State Risk | `async void`, `.Result`/`.Wait()`, `lock(this)`, static mutable state |
| GCI0017 | Scope Discipline | Scope creep, unrelated file changes bundled into one diff |
| GCI0018 | Production Readiness | Aggregate risk signal when multiple rules fire on the same diff |
| GCI0019 | Confidence and Evidence | High-risk changes without sufficient supporting evidence in diff |
| GCI0020 | Accountability Standard | Missing audit trail and accountability logging |
| GCI0021 | Data & Schema Compatibility | Non-backward-compatible schema changes without migration guards |
| GCI0022 | Idempotency & Retry Safety | Non-idempotent operations, missing retry/duplicate guards |
| GCI0023 | Structured Logging | Unstructured log calls, PII in log message templates |
| GCI0024 | Resource Lifecycle | IDisposable lifecycle violations, resource leaks |
| GCI0025 | Feature Flag Readiness | Missing feature flag guards for high-risk or staged rollout changes |
| GCI0026 | Documentation Adequacy | Missing or insufficient docs for public APIs and exported types |
| GCI0027 | Test Quality | Test hygiene: no assertions, `Assert.True(false)`, magic literals |
| GCI0029 | PII Entity Logging Leak | PII data (email, SSN, card) detected in log call arguments |
| GCI0030 | IDisposable Resource Safety | Missing `using` / `Dispose()` on IDisposable instances |
| GCI0031 | Boundary Drift | Off-by-one boundary values added without matching test evidence |
| GCI0032 | Uncaught Exception Path | New `throw` statements without `Assert.Throws` test evidence |
| GCI0033 | Async Sinkhole | `.Result` or `.Wait()` blocking calls that risk thread deadlocks |
| GCI0034 | Null-Coalescing Expansion | Null-coalescing patterns that mask deeper null propagation bugs |
| GCI0035 | Architecture Layer Guard | Wrong-direction layer dependencies (e.g. Core → Infrastructure) |
| GCI0036 | Pure Context Mutation | Side-effect mutations in property getters or `[Pure]`-annotated methods |
| GCI0037 | AutoMapper Integrity | AutoMapper misuse: missing profiles, flattened nullable chains |

> **Note:** GCI0028 is reserved and intentionally unassigned.

---

## Example

```bash
 $ gauntletci analyze --staged
 
 =======================================================
   GauntletCI Risk Analysis Report
 =======================================================
   Rules  : 36 evaluated
   Findings: 21
 
 -- HIGH CONFIDENCE (12) --------------------------
   [GCI0007] Error Handling Integrity
   Summary  : Swallowed exception detected in src/Payments/PaymentProcessor.cs
   Evidence : catch (Exception)
   Why      : Empty or silent catch blocks hide failures, making bugs invisible and debugging nearly impossible.
   Action   : Log the exception, rethrow it, or handle it explicitly. Never swallow silently.
 
   [GCI0010] Hardcoding and Configuration
   Summary  : Hardcoded connection string detected.
   Evidence : Line 10: private const string ConnectionString = "Server=prod-db;...;Password=Xk9#mPqR2!vL";
   Why      : Connection strings in source code expose credentials and prevent per-environment configuration.
   Action   : Use IConfiguration, Secret Manager, or environment variables for connection strings.
 
   [GCI0012] Security Risk
   Summary  : Possible hardcoded credential ('password').
   Evidence : Line 10: private const string ConnectionString = "Server=prod-db;...;Password=Xk9#mPqR2!vL";
   Why      : Credentials in source code are exposed via version control and are easily compromised.
   Action   : Use a secrets manager or environment variables. Never hardcode credentials.
 
   [GCI0016] Concurrency and State Risk
   Summary  : Blocking async call (.Result / .Wait()) can cause deadlocks.
   Evidence : Line 22: var result = _gateway.ChargeAsync(request).Result;
   Why      : .Result and .Wait() block the calling thread, risking deadlock in ASP.NET or UI contexts.
   Action   : Use await instead. If you must block, use .ConfigureAwait(false) and be aware of the risk.
 
   [GCI0029] PII Entity Logging Leak
   Summary  : PII term 'email' found in log call — may expose sensitive data in src/Payments/PaymentProcessor.cs.
   Evidence : Line 19: _logger.LogInformation("Processing payment for {Email} with card {CreditCard}", ...);
   Why      : Logging PII violates GDPR, CCPA, and HIPAA. Once in logs, PII propagates to aggregators and storage.
   Action   : Redact or omit PII from log calls. Log only anonymized identifiers (e.g. UserId, not Email or SSN).
 
   [GCI0033] Async Sinkhole
   Summary  : Blocking .Result access on a Task in src/Payments/PaymentProcessor.cs — risk of deadlock.
   Evidence : Line 22: var result = _gateway.ChargeAsync(request).Result;
   Why      : Calling .Result blocks the calling thread and can cause deadlocks in ASP.NET or WPF contexts.
   Action   : Use `await` instead of `.Result` or `.Wait()`.
 
   [GCI0036] Pure Context Mutation
   Summary  : Assignment in getter or [Pure] method in src/Payments/PaymentProcessor.cs — mutation in a pure context.
   Evidence : Line 48: _lastValidated = amount;
   Why      : Property getters are expected to be side-effect free. Mutations break caching and framework contracts.
   Action   : Move state mutations to a setter, constructor, or dedicated method.
 
 -- MEDIUM CONFIDENCE (7) --------------------------
   [GCI0005] Test Coverage Relevance
   Summary  : Code files changed with no test file changes.
   Evidence : Changed code files: src/Payments/PaymentProcessor.cs, src/Payments/PaymentRequest.cs
   Why      : Untested changes increase regression risk. Reviewers cannot verify correctness without tests.
   Action   : Add or update tests for the changed code files.
 
   [GCI0031] Boundary Drift
   Summary  : Boundary value 0 added via comparison operator with no matching test evidence in diff.
   Evidence : Line 49: return amount > 0;
   Why      : Off-by-one errors at boundaries are the most common source of bugs.
   Action   : Add an xUnit [InlineData(0)] or equivalent test that exercises this boundary value.
 
   [GCI0032] Uncaught Exception Path
   Summary  : 1 'throw new' statement(s) added without Assert.Throws or Should().Throw evidence in this diff.
   Why      : New exception paths that are untested may crash callers silently in production.
   Action   : Add xUnit `Assert.Throws<T>` tests for each new exception path.
 
   [GCI0018] Production Readiness
   Summary  : 18 rules flagged issues — this diff has multiple risk dimensions.
   Evidence : Rules fired: GCI0003, GCI0005, GCI0006, GCI0007, GCI0010, GCI0011, GCI0012, GCI0016, GCI0029, GCI0033, ...
   Action   : Address High-confidence findings first, then revisit Medium/Low before merging.
 
 -- LOW CONFIDENCE (2) --------------------------
   [GCI0023] Structured Logging
   Summary  : Critical-path file src/Payments/PaymentProcessor.cs has logging but no correlation/request ID.
   Action   : Include CorrelationId or TraceId in log statements for end-to-end traceability.
```
