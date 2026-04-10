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
