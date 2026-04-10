# GauntletCI

Pre-commit change-risk detection for pull request and pre-commit diffs.

---

## What is GauntletCI?

GauntletCI is a .NET CLI tool that analyzes pull request diffs and detects **behavioral change-risk before code is merged**.

It answers one question:

> Did this change introduce behavior that is not properly validated?

---

## The problem

Code review focuses on *what looks wrong*.

But many production issues come from something else:

**Behavior changes that look correct — but were never validated.**

Examples:

- A null check changes execution flow
- A guard clause introduces new exceptions
- A method signature changes without test updates
- A conditional branch subtly shifts logic

These changes often:
- pass tests
- pass linting
- pass review

…and still cause problems.

---

## What GauntletCI does

GauntletCI analyzes diffs and detects:

- Unvalidated behavior changes
- Missing or weak test coverage
- Execution flow changes (guards, exceptions, branching)
- API and contract changes

It focuses only on:

> What changed — and what risk did that introduce?

---

## Example Output
 
 ```
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

How it fits into your workflow

GauntletCI runs:

- As a pre-commit hook
- In CI pipelines (GitHub Actions, etc.)
- Locally during development

It complements:

- Tests
- Code review
- Static analysis tools

Why this is different

Most tools answer:

- Is the code correct?

GauntletCI answers:

- What changed — and what risk did that introduce?

That difference is where bugs hide.

Getting started

Install:

- dotnet tool install -g GauntletCI

Run:

- gauntletci analyze --diff pr.diff

Project status

GauntletCI is actively being developed.

Current focus:

Expanding rule coverage (GCI00xx series)
Improving detection accuracy
Building a robust rule engine
Source code

GitHub repository:

https://github.com/EricCogen/GauntletCI

Final note

GauntletCI was built to solve a simple but persistent problem:

Even experienced developers miss things in diffs.

This tool exists to catch them early.
