# GauntletCI Rule Reference

GauntletCI is a deterministic change-risk detection engine. Before a pull request is merged, it reads the diff — the exact lines added and removed — and evaluates them against a structured rule set. When a rule detects a risk, it produces a **finding** that describes what was found, why it matters, and what the reviewer should do about it.

Rules do not modify code and do not block merges on their own. They surface information for human reviewers, automation pipelines, and auditors to act on. Every finding carries a **confidence level** that indicates how certain the rule is about the risk it has identified.

---

## How to Read This Document

**Rule ID** — A stable identifier in the format `GCIxxxx`. IDs are never reused, even if a rule is retired.

**Confidence levels:**
- **High** — The pattern detected is almost always a genuine problem. Findings at this level should be treated as blockers unless explicitly reviewed and accepted.
- **Medium** — The pattern is likely a problem but context matters. A reviewer should inspect and confirm before merging.
- **Low** — The pattern is a known risk indicator but has a meaningful false-positive rate. Treat as an advisory signal.

**What "findings" mean** — A finding does not mean the code is wrong. It means GauntletCI detected a pattern that warrants human attention. Reviewers decide whether to address it or acknowledge it as intentional.

**How rules are configured** — Rules run automatically against every diff. Per-repository configuration can disable individual rules or adjust their severity. See the Configuration Reference section at the end of this document.

---

## Reviewer Guide

This document is prepared for an independent third-party review of the GauntletCI rule set. The goal of this review is to help us answer three questions honestly:

1. **Should any rules be removed or merged?** Rules that fire too broadly, punish developers for legitimate patterns, or duplicate coverage already provided by another rule create alert fatigue and erode trust in the tool. If a rule is more noise than signal, we want to know.

2. **Should any rules be adjusted?** A rule may be detecting the right *category* of risk but using patterns that are too loose (causing false positives), too strict (missing real cases), or calibrated at the wrong confidence level. If the detection logic needs narrowing or the confidence level is wrong, tell us.

3. **Are there important risk categories we have missed?** We have 39 rules today. We do not believe this is exhaustive. If you can identify a class of risk that regularly causes production incidents, security vulnerabilities, or data loss — and that a diff-level static analysis could plausibly detect — we want to add it.

### What GauntletCI is (and is not)

Understanding these constraints will help you evaluate the rules fairly:

- **It analyzes diffs, not full codebases.** GauntletCI sees only the lines that were added or removed in a pull request. It cannot reason about the overall structure of the codebase, call graphs, or runtime state. A rule that would require reading the entire file or tracing a call chain is out of scope.
- **It is fully deterministic.** There is no machine learning, no network calls, and no external lookups. Every detection is a text pattern, a structural heuristic, or a count. This is intentional — deterministic rules are auditable, explainable, and fast.
- **It must complete in under one second per diff.** Rules need to be lightweight. Regex matching on added/removed lines is the primary tool. Rules that require parsing, compiling, or AST traversal are not feasible today.
- **It is not a replacement for a security scanner or linter.** GauntletCI is a change-risk signal, not a comprehensive static analysis platform. It is meant to surface things a human reviewer might miss at merge time — not to replace SAST tools, dependency auditors, or test coverage tools.
- **Findings are advisory by default.** No rule automatically blocks a merge. High-confidence findings are *intended* to block, but the enforcement mechanism is up to the team using the tool.
- **It is primarily focused on C#/.NET.** Some rules are language-agnostic (file naming, diff shape, logging patterns). Most rules target C# idioms. If you have expertise in another stack, note it — we may expand language support later.

### How to give useful feedback

For each rule you review, answer as many of these as you can:

- **Is the risk real?** Does this pattern actually cause incidents, bugs, or security issues in practice? Or is it theoretical?
- **Is the confidence level right?** High means "almost always a problem." Low means "maybe a problem, worth a look." Is the level we've assigned appropriate?
- **What would cause a false positive?** Describe a legitimate pattern that would trigger this rule incorrectly. Even one concrete scenario is valuable.
- **What would it miss?** If a developer tried to write the risky code in a slightly different way, would the rule still catch it?
- **Keep, adjust, merge, or remove?** For each rule, give a verdict: keep as-is, modify (describe how), merge into another rule, or remove.

For new rules you want to suggest:

- Describe the risk category in plain English.
- Give a concrete example of the code pattern that should trigger it.
- Suggest a confidence level and explain why.
- Note whether it would overlap with any existing rule.

### What we do not need from this review

- Style opinions or formatting preferences.
- Suggestions requiring full-program analysis or runtime information.
- Language-specific rules for languages other than C# (unless you have a strong case for prioritizing them).
- Feedback on the tool's UI, CLI, or configuration format — this review is focused solely on the rule logic.

---

## Tier 1 — Structural & Scope Integrity

These rules examine the shape of the diff itself rather than the code it contains. They ask whether the change is focused, well-described, and reviewable as a single unit. Large, unfocused, or misdescribed diffs are harder to review and increase the chance that unintended changes slip through.

---

### GCI0001 · Diff Integrity

**Confidence:** Medium / Low
**What it detects:** Two separate checks. First, it looks for diffs that mix source code files with non-code files such as images, configuration, or documentation — a signal that two unrelated concerns have been bundled into one change. Second, it scans each file for lines where the only change is whitespace or blank lines; if more than 40% of the changed lines in a file are whitespace-only, it flags excessive formatting churn.
**Why it matters:** Mixed-scope diffs force reviewers to context-switch and make it harder to spot logic changes hiding behind visual noise. Formatting churn obscures real intent.
**Suggested action:** Split code changes and non-code changes into separate pull requests. Run the project's formatter in a dedicated commit rather than mixing it with logic changes.

---

### GCI0002 · Goal Alignment

**Confidence:** Low
**What it detects:** Two checks. First, it compares keywords in the commit message against the paths of changed files. If the diff touches more than three files and no words from the commit message appear in any of the file paths, it flags a possible mismatch between the stated purpose and the actual change. Second, if the diff touches more than five files and spans at least three of the four categories — frontend, backend, configuration, and tests — it flags an overly broad scope.
**Why it matters:** Commits that do not match their description break `git blame` traceability and confuse future investigators. Cross-cutting changes are harder to review and harder to roll back.
**Suggested action:** Update the commit message to reflect what was actually changed, or split the change into focused commits grouped by concern.

---

### GCI0017 · Scope Discipline

**Confidence:** Low
**What it detects:** Two checks. First, it counts how many distinct top-level directories are affected by the diff across all changed files, including non-code files. If three or more top-level directories are touched, it flags the diff as spanning too many modules. Second, it checks whether production code files and non-production files — such as database migrations, seed data, or test fixtures — are changed in the same diff.
**Why it matters:** Changes that reach across many modules increase merge conflict risk and make rollback harder because a single revert undoes unrelated work. Mixing data migrations with production logic makes the diff harder to understand and audit.
**Suggested action:** Split changes by module. Commit data migrations and seed files separately from production code changes.

---

### GCI0019 · Confidence and Evidence

**Confidence:** Low
**What it detects:** Two checks. First, it identifies binary files — such as images, PDFs, compiled executables, or font files — included in the diff, since their contents cannot be inspected by static analysis. Second, it runs as a post-processor after all other rules: if the diff changes more than 200 lines but no other rules produced findings, it flags the possibility of hidden risks that deterministic rules did not catch.
**Why it matters:** Binary files cannot be scanned for credentials, logic errors, or security issues using text-based analysis. Large diffs with no findings may simply mean the code is clean, but they may also mean the risks are subtle enough to evade automated checks.
**Suggested action:** Review binary files manually and consider storing large binaries in Git LFS. For large diffs with no findings, consider a manual deep review or enabling LLM-assisted analysis.

---

### GCI0020 · Accountability Standard

**Confidence:** High / Medium
**What it detects:** Four checks. First, it scans added lines for credential patterns — variable names like `password`, `secret`, `apikey`, or `token` assigned a string literal. Second, it looks for blocks of five or more consecutive comment lines in newly added code, which often indicates commented-out code being left in place. Third, it detects authorization attributes with an empty roles string, which signals a misconfigured access control decorator that may grant access more broadly than intended. Fourth, it checks for code that appears after a `return` or `throw` statement — unreachable lines that indicate a logic error.
**Why it matters:** Credentials in source code are permanently captured in version control even after removal. Commented-out code creates confusion and suggests incomplete cleanup. An empty roles string in an authorization decorator may silently allow all authenticated users into a restricted endpoint. Unreachable code indicates broken logic.
**Suggested action:** Remove credential literals and use a secrets manager. Delete commented-out code — version control preserves history. Correct or remove the empty roles string. Fix or remove unreachable code.

---

## Tier 2 — Behavioral & Correctness Risk

These rules examine logic changes that could alter how the software behaves at runtime. They look for removed decision paths, changed interfaces, missing tests, unvalidated inputs, uncaught errors, and structural complexity that makes behavior hard to reason about.

---

### GCI0003 · Behavioral Change Detection

**Confidence:** Medium
**What it detects:** Two checks. First, if three or more lines containing control-flow keywords — such as conditional statements, return statements, or boolean operators — are removed from non-test files without any corresponding test file changes in the same diff, it flags the removal as potentially untested. Second, it compares the signatures of methods that exist in both the removed and added lines of the same file; if a method's parameter list changed, it reports the before-and-after signature.
**Why it matters:** Deleting conditional logic without updating tests can silently break behavior that was previously protected by test coverage. Signature changes can break other code that calls the method but was not included in this diff.
**Suggested action:** Update or add tests to verify that removed logic paths are intentionally gone. Confirm all callers of changed signatures are updated, and consider adding a backward-compatible overload.

---

### GCI0004 · Breaking Change Risk

**Confidence:** High / Medium
**What it detects:** Two checks. First, it identifies public members — methods, classes, interfaces, structs, records, enums, and properties — that appear in the removed lines of a file but not in the added lines. These are treated as deleted public APIs. If the member also appears in the added lines but with a different signature, it is treated as a changed public API instead of a deleted one. Second, it looks for removed deprecation markers that previously signaled a member was scheduled for removal.
**Why it matters:** Removing or changing a public member is a breaking change for any code that depends on it — including external consumers of a library who are not part of this repository. Removing a deprecation marker without completing the removal may leave consumers with no warning.
**Suggested action:** Mark deprecated members as obsolete before removing them, and coordinate removal with a major version increment. Verify the deprecation marker removal is intentional and all consumers have migrated.

---

### GCI0005 · Test Coverage Relevance

**Confidence:** Medium
**What it detects:** Two checks. First, if the diff modifies production code files but contains no changes to test files, it flags the absence of test changes. Second, if the diff modifies test files but contains no changes to production code files, it flags the test changes as potentially orphaned.
**Why it matters:** Code changes without test updates increase regression risk. Test changes without corresponding production code changes may indicate tests written for code not yet implemented, or that the production file was accidentally excluded from the diff.
**Suggested action:** Add or update tests alongside any production code changes. If only tests changed intentionally, explain why in the pull request description.

---

### GCI0006 · Edge Case Handling

**Confidence:** Medium
**What it detects:** Two checks, plus optional static analysis results. First, it scans new code for lines that access a `.Value` property — a pattern used to unwrap nullable types — without a preceding null check within five lines. Second, it checks whether new methods with string or object parameters include argument validation within the first few lines of the method body. It also incorporates findings from Roslyn's static analysis rule CA1062, which detects parameters used without null validation.
**Why it matters:** Accessing a nullable's value without first checking that a value exists throws an exception at runtime. Unvalidated parameters can cause null reference errors or incorrect behavior deeper in the call stack, far from where the bug was introduced.
**Suggested action:** Add null checks before accessing nullable values. Add argument guards at the top of new methods. Use built-in validation helpers where available.

---

### GCI0007 · Error Handling Integrity

**Confidence:** High
**What it detects:** Two checks, plus optional static analysis results. First, it looks for newly added catch blocks that contain no meaningful content — no rethrow, no logging, no error recording. These are "swallowed" exceptions where the error is silently discarded. Second, it checks whether error-level log calls were removed from within error-handling blocks without being replaced, reducing the number of error log calls in that block.
**Why it matters:** Empty catch blocks make failures invisible. When an exception is swallowed, the application continues running in a potentially invalid state with no evidence of what went wrong. Removing error logs from catch blocks eliminates critical context that would otherwise be available during incident triage.
**Suggested action:** Log the exception, rethrow it, or handle it explicitly in every catch block. Preserve error-level logging in exception handlers so that failures are always visible in production.

---

### GCI0008 · Complexity Control

**Confidence:** Low
**What it detects:** Three checks. First, it tracks brace nesting depth across added lines in each file; if any point in the added code reaches more than four levels of nesting, it flags the file. Second, it identifies method-like blocks in the added code that contain more than thirty lines. Third, it looks for lines that appear three or more times verbatim across all added lines in the diff, which suggests duplicated logic.
**Why it matters:** Deep nesting makes code harder to read, test, and change without introducing bugs. Long methods are difficult to understand in isolation and tend to accumulate responsibilities over time. Duplicated logic creates maintenance debt — a fix in one copy must be replicated in all others.
**Suggested action:** Extract nested logic into private helpers and use early-return guard clauses to reduce nesting. Break long methods into smaller, focused functions. Extract repeated logic into a shared method or constant.

---

## Tier 3 — Security & Compliance

These rules detect patterns with direct security or regulatory consequences. Findings at High confidence in this tier should be treated as blockers. They cover hardcoded secrets, vulnerable cryptographic choices, SQL injection risk, irreversible data operations, schema compatibility, and personal data appearing in log output.

---

### GCI0010 · Hardcoding and Configuration

**Confidence:** High / Medium
**What it detects:** Six checks. It scans added lines for: IP addresses embedded in code; URLs starting with `http://` or `https://` inside string literals; connection strings for databases or caches; variables named after secrets (such as `password`, `secret`, `apikey`, or `token`) assigned a string value; known infrastructure port numbers used as numeric literals; and environment names like `production` or `staging` embedded as string values.
**Why it matters:** Values hardcoded into source code cannot be changed without modifying and redeploying the application. Credentials end up in version control history permanently. Environment-specific values break deployments when the application moves between environments.
**Suggested action:** Move all environment-specific values, connection strings, and credentials into configuration files or environment variables. Use a secrets manager for anything sensitive.

---

### GCI0012 · Security Risk

**Confidence:** High
**What it detects:** Six checks. SQL strings built by concatenating or interpolating user-supplied values (a classic SQL injection pattern). Use of cryptographically broken hashing algorithms such as MD5 or SHA1. Use of deprecated encryption algorithms including DES, RC2, and Triple-DES. Calls to dangerous reflection and process-execution APIs that can be exploited if arguments are user-controlled. Variables named after credentials being assigned a string literal. JSON deserialization configured to allow arbitrary type instantiation, which enables remote code execution attacks. Additionally, it looks for authorization restrictions being replaced with open-access annotations on the same controller file.
**Why it matters:** These patterns represent well-known attack vectors catalogued by security industry standards. SQL injection, weak cryptography, and dangerous deserialization are consistently among the most exploited vulnerabilities in production software.
**Suggested action:** Use parameterized queries or an ORM. Replace MD5 and SHA1 with SHA-256 or stronger. Use AES for symmetric encryption. Avoid dynamic type loading with untrusted input. Store credentials in a secrets manager. Disable arbitrary type name handling in JSON deserializers.

---

### GCI0014 · Rollback Safety

**Confidence:** High / Medium
**What it detects:** Three checks. Database Data Definition Language (DDL) statements — such as dropping tables, truncating data, dropping columns, or dropping indexes — in added lines. File deletion and process-termination API calls. Database migration files that define an upgrade path without a corresponding rollback path.
**Why it matters:** DDL statements that destroy or restructure data cannot be undone once executed in most database engines. File deletion is permanent unless backups exist. A migration with no rollback method means that reverting a bad deployment requires manual intervention rather than a single automated command.
**Suggested action:** Test DDL changes in staging before production and ensure backups exist. Use soft-delete patterns instead of physical deletion where possible. Implement the rollback method in every migration file.

---

### GCI0021 · Data & Schema Compatibility

**Confidence:** High / Medium
**What it detects:** Two checks. First, it scans removed lines for serialization and schema attributes — such as JSON property names, database column mappings, validation decorators, and primary/foreign key declarations — that have been deleted from entity or model classes. Second, it parses the full diff context of each file to detect when a member is removed from an enumeration type.
**Why it matters:** Serialization attributes and column mappings define the contract between the application and its data stores, message queues, and API consumers. Removing them silently changes how data is read and written, breaking deserialization of records that were stored under the old schema. Removing enumeration members breaks deserialization of stored integer or string values that mapped to the removed entry.
**Suggested action:** Keep removed properties and mark them as obsolete rather than deleting them outright. Version schema changes explicitly with a migration. Use enumeration deprecation patterns rather than removal.

---

### GCI0029 · PII Entity Logging Leak

**Confidence:** High
**What it detects:** It scans every line added to source files for log calls that include terms associated with personally identifiable information. The monitored terms include email addresses, social security numbers, phone numbers, credit card numbers, dates of birth, passport numbers, national identifiers, tax identifiers, bank account numbers, physical addresses, usernames, postal codes, geographic location data, device identifiers, and authentication tokens.
**Why it matters:** Personal data written to application logs propagates to log aggregators, log storage systems, and third-party monitoring services. This constitutes a data breach under GDPR, CCPA, and HIPAA — even if the log is not publicly accessible. Log data is often retained for long periods and accessed by systems and personnel that should not have access to personal data.
**Suggested action:** Remove personal data from log calls. Log only anonymized identifiers such as user IDs, not names, email addresses, or other identifying attributes.

---

## Tier 4 — Resource & Concurrency Safety

These rules detect patterns that cause performance degradation, data corruption, race conditions, and resource leaks. They cover threading hazards, unmanaged resource disposal, data validation gaps, and operations that are unsafe to retry.

---

### GCI0011 · Performance Risk

**Confidence:** Medium
**What it detects:** Four checks, all targeting added code within loop constructs. It flags materializing a collection to a list or array inside a loop body, which causes repeated full-enumeration allocations. It flags using a count-based existence check where a short-circuit check would suffice, causing the entire collection to be enumerated even when only the first matching element is needed. It flags allocating a new list or dictionary inside a loop, which generates garbage collection pressure on every iteration. It flags building strings with concatenation inside a loop, which is quadratic in complexity due to the immutability of strings.
**Why it matters:** These patterns appear correct and produce correct output but perform poorly at scale. They are among the most common causes of performance regressions that are expensive to diagnose after deployment.
**Suggested action:** Materialize collections before entering loops. Use the appropriate short-circuit check for existence. Allocate collections outside loops and clear between iterations. Use a string builder for multi-step string construction.

---

### GCI0015 · Data Integrity Risk

**Confidence:** High / Medium / Low
**What it detects:** Four checks. First, it looks for sequences of three or more consecutive field assignments in files that also receive HTTP request data — a pattern associated with over-posting, where a client can set fields the developer did not intend to expose. Second, it checks for the same consecutive assignment pattern in general code without accompanying null guards, flagging potential mass assignment without validation. Third, it looks for explicit numeric type casts — such as converting a value to an integer or a decimal — on lines that may involve user-supplied data, without overflow checking. Fourth, it looks for SQL statements that silently swallow insert conflicts rather than surfacing them.
**Why it matters:** Over-posting allows attackers to write to internal fields of an entity by including extra properties in a request payload. Unchecked numeric casts can silently truncate values or throw overflow exceptions. Silent insert-conflict suppression hides data integrity violations that should trigger an error or a deliberate upsert decision.
**Suggested action:** Use a dedicated data transfer object to control which fields can be set from request data. Add input validation before assignment. Use checked arithmetic or safe parsing methods. Handle insert conflicts explicitly rather than suppressing them.

---

### GCI0016 · Async Concurrency Risk

**Confidence:** High / Medium
**What it detects:** Four checks on added lines. It flags methods declared as `async void` — except for legitimate event handler signatures with `(object sender, ...EventArgs e)` parameters. It flags calls that block the current thread waiting for an async operation: `.Wait()`, `.GetAwaiter().GetResult()`, and `.Result` when the expression clearly operates on a Task (method call chain or explicit Task/Async context). It flags locking on `this`, which exposes the lock object to external callers. It flags `Thread.Sleep()` in production code, which blocks a thread pool thread instead of yielding it.
**Why it matters:** `async void` methods cannot be awaited — exceptions they throw crash the process via `AppDomain.UnhandledException` with no way for the caller to recover. Blocking on async operations in a `SynchronizationContext` (ASP.NET, Blazor, WPF) deadlocks because the continuation needs the thread that is already blocked. `lock(this)` creates an external deadlock vector. `Thread.Sleep` wastes a thread pool thread and degrades throughput under load.
**Suggested action:** Change `async void` to `async Task`. Use `await` instead of `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`. Replace `lock(this)` with a dedicated `private readonly object _lock = new()`. Replace `Thread.Sleep()` with `await Task.Delay()`.

---

### GCI0022 · Idempotency & Retry Safety

**Confidence:** Medium / Low
**What it detects:** Three checks. First, it looks for HTTP POST endpoint declarations in controller files without any idempotency key handling in the surrounding code — such as a request identifier, deduplication key, or message ID. Second, it looks for raw SQL INSERT statements without a conflict-resolution clause that would make the insert safe to retry. Third, it looks for event handler subscriptions being added without a corresponding unsubscription or a guard that prevents the handler from being attached more than once.
**Why it matters:** Operations that are not idempotent produce duplicate side effects when retried — such as charging a customer twice, creating duplicate records, or firing a business event multiple times. Network errors, message queue redelivery, and browser double-submits are common retry scenarios that every public-facing endpoint must handle safely.
**Suggested action:** Add idempotency key support to POST endpoints and validate the key server-side. Use upsert semantics or unique constraints for database inserts. Unsubscribe before subscribing to events, or guard subscriptions with a flag.

---

### GCI0024 · Resource Lifecycle

**Confidence:** High / Medium
**What it detects:** It scans added lines for objects whose types implement the disposable pattern — including file streams, database connections, network clients, cryptographic objects, synchronization primitives, and any type whose name ends with a suffix commonly associated with disposable resources such as Stream, Reader, Writer, Connection, Client, Context, or Transaction. It checks whether the allocation is wrapped in a disposal guarantee. It also incorporates static analysis results for disposable resource management.
**Why it matters:** Types that manage operating system handles, database connections, or network sockets must be explicitly released when no longer needed. Without a disposal guarantee, resources leak under exceptions, exhausting connection pools or file handles over time.
**Suggested action:** Wrap every disposable allocation in a usage block that guarantees disposal even when exceptions occur. For types injected via the dependency injection container, rely on the container's lifetime management.

---

## Tier 5 — Observability & Maintainability

These rules protect the long-term health of the codebase and the team's ability to diagnose and operate the system in production. They cover logging quality, code consistency, production readiness markers, documentation, and test quality.

---

### GCI0009 · Consistency with Patterns

**Confidence:** Low
**What it detects:** Two checks. First, if the diff context shows the existing codebase uses the asynchronous programming model, it looks for new methods whose names or return types suggest they should be asynchronous but are not declared as such. Second, it incorporates static analysis results that flag inconsistent string comparison styles and naming convention violations.
**Why it matters:** Inconsistent use of asynchronous patterns makes code difficult to reason about and can introduce subtle deadlocks when mixing synchronous and asynchronous code paths. Inconsistent naming conventions reduce readability across a shared codebase.
**Suggested action:** Use the asynchronous model consistently, or explicitly return a completed result where synchronous behavior is intended. Follow the project's established naming conventions.

---

### GCI0013 · Observability/Debuggability

**Confidence:** Low
**What it detects:** It checks each file where twenty or more lines have been added. If no logging calls are detected anywhere in those added lines, it flags the file as potentially unobservable.
**Why it matters:** Code paths without any logging are opaque in production. When something goes wrong in a file with no log output, there is no trail to follow during incident investigation.
**Suggested action:** Add logging at entry and exit points of significant operations, and at all error paths, so that the behavior of the code is visible in production logs.

---

### GCI0018 · Production Readiness

**Confidence:** Medium
**What it detects:** Three checks. First, it counts added lines containing work-in-progress markers such as TODO, FIXME, HACK, or XXX. Second, it looks for statements that throw a placeholder exception indicating a method body has not been implemented. Third, it checks non-test, non-CLI files for diagnostic output calls — such as writing directly to the console — and for debug assertion calls that are silently stripped in release builds.
**Why it matters:** Work-in-progress markers indicate incomplete work being shipped. Placeholder exceptions crash callers at runtime. Console output bypasses the application's logging infrastructure and is lost in production. Debug assertions that are stripped in release builds provide no runtime protection.
**Suggested action:** Resolve or convert all markers to tracked issues before merging. Implement all placeholder methods. Replace console output and debug assertions with structured logging and proper runtime guards.

---

### GCI0023 · Structured Logging

**Confidence:** Medium / Low
**What it detects:** Two checks. First, it looks for log calls where the message is constructed using string interpolation — embedding values directly into the message string — rather than using named message template placeholders. Second, for files in critical business domains such as authentication, payment, billing, and order processing, it checks whether logging calls include a correlation or trace identifier.
**Why it matters:** String interpolation in log messages prevents log aggregation systems from indexing structured fields, making log data unsearchable and unqueryable. Without correlation identifiers, tracing a single request across multiple services during an incident is extremely difficult.
**Suggested action:** Use named message template placeholders and pass values as separate arguments so that log aggregators can index them as queryable fields. Include a correlation, request, or trace identifier in all log calls on critical business paths.

---

### GCI0025 · Feature Flag Readiness

**Confidence:** Medium
**What it detects:** For files in critical business domains — including authentication, payment, billing, order processing, subscriptions, tokens, credentials, and passwords — it checks whether large changes (fifty or more added lines) include a reference to a feature flag or toggle mechanism. Recognized flag systems include common feature management libraries and interfaces.
**Why it matters:** Large changes to high-value code paths that are shipped without a feature flag cannot be rolled back without a full redeployment. A feature flag allows the new behavior to be disabled instantly in production if it causes incidents, without touching the code.
**Suggested action:** Wrap significant new behavior behind a feature flag so that it can be disabled in production without a code change or redeployment.

---

### GCI0026 · Documentation Adequacy

**Confidence:** Low
**What it detects:** For each public method added to a source file, it checks whether an XML documentation comment block appears immediately above the method declaration, allowing for attribute decorators between the comment and the method signature.
**Why it matters:** Public methods are part of a shared API surface. Without documentation comments, callers must read the implementation to understand parameters, return values, and expected behavior — especially costly in shared libraries and services.
**Suggested action:** Add a summary documentation block above every new public method describing its purpose, parameters, and return value.

---

### GCI0027 · Test Quality

**Confidence:** High / Medium
**What it detects:** For each test method added in test files, it checks whether the method body contains any assertion — a statement that verifies an expected outcome. If the method has no assertion at all, it is flagged at High confidence. If the method's only assertions are null-check assertions — checking that a value exists without verifying what the value is — it is flagged at Medium confidence.
**Why it matters:** A test with no assertions always passes regardless of what the code does, providing false confidence and zero protection against regressions. Assertions that only check non-null confirm that something was returned but not whether it was the correct thing.
**Suggested action:** Add value-level assertions that verify the actual output of the code under test matches the expected output for the given inputs.

---

## Tier 6 — Evidence & Test Completeness

These rules require that behavioral changes in production code are accompanied by corresponding test evidence in the same diff. They detect untested exception paths, untested boundary conditions, untested null-handling behavior, and unvalidated object mapping configurations.

---

### GCI0031 · Boundary Drift

**Confidence:** Medium
**What it detects:** It collects every numeric literal used in a comparison operation — less than, greater than, or their inclusive equivalents — in non-test files. For each such literal, it checks whether a test file in the same diff contains that same value in a test data row or assertion. If the boundary value appears in production logic but has no corresponding test coverage in the diff, it is flagged.
**Why it matters:** Off-by-one errors at boundary conditions are one of the most common categories of software bugs. Without a test that exercises the exact boundary value, the correctness of the boundary check cannot be verified.
**Suggested action:** Add parameterized test cases that exercise the exact numeric boundary values introduced in the production code change.

---

### GCI0032 · Uncaught Exception Path

**Confidence:** Medium
**What it detects:** It counts the number of newly introduced statements that throw an exception in non-test files. If any such statements exist, it checks whether any test file in the same diff contains assertions that verify an exception is thrown under specific conditions. If new exception-throwing statements exist without corresponding throw-assertion tests, it flags the gap.
**Why it matters:** Every new exception-throwing path represents a code path that callers must handle. Without a test that exercises and validates that exception path, the behavior when the exception is raised in production cannot be confirmed to be correct.
**Suggested action:** Add tests that exercise each new exception-throwing path and verify that the correct exception type and message are produced.

---

### GCI0034 · Null-Coalescing Expansion

**Confidence:** Low
**What it detects:** It counts the number of newly added null-conditional or null-coalescing operators — patterns used to provide default behavior when a value is absent — in non-test files. If any such operators exist, it checks whether any test file in the same diff passes a null value to the code being tested. If null guards are added without null-input test coverage, it flags the gap.
**Why it matters:** A null guard that is never tested with null input may be masking a null reference exception source rather than intentionally handling a valid null case. The fallback behavior needs test coverage to confirm it is correct.
**Suggested action:** Add a test case that passes null for the value being guarded and verifies that the fallback behavior produces the expected result.

---

### GCI0037 · Mapping Profile Integrity

**Confidence:** Medium
**What it detects:** Four independent checks, one for each of the major object mapping libraries in use in the .NET ecosystem: AutoMapper, Mapster, AgileMapper, and TinyMapper. For each library, it detects whether mapping configuration was added or changed in the diff, and if so, whether the diff also contains the corresponding compile-time validation call specific to that library. For TinyMapper, which has no built-in compile-time validation, it always flags when a binding change is detected.
**Why it matters:** Object mapping configurations that are incorrect or incomplete fail at runtime when the mapping is first exercised, not at compile time. Without a test that validates the mapping configuration, broken mappings reach production.
**Suggested action:** Call the appropriate compile-time validation method for the mapping library in a unit test after every mapping configuration change. For TinyMapper, add a test that exercises the binding with representative data.

---

## Tier 7 — Architecture & Structural Contracts

These rules enforce structural invariants that preserve the long-term integrity of the codebase. They check that architectural layer boundaries are respected and that code which is expected to be free of side effects does not introduce mutations.

---

### GCI0035 · Architecture Layer Guard

**Confidence:** High / Low
**What it detects:** It checks every newly added import statement against a configured list of forbidden import pairs. Each pair defines a source layer — identified by a fragment of its namespace or directory path — and a set of namespaces that layer must not import. If an import statement in a file belonging to the source layer references a forbidden namespace, it is flagged. If no forbidden import configuration has been provided, it emits a low-confidence advisory reminding the reviewer that the rule is not yet configured.
**Why it matters:** Cross-layer imports break the separation of concerns that makes a layered architecture testable and maintainable. For example, a domain model importing from an infrastructure namespace creates a coupling that prevents the domain from being tested in isolation.
**Suggested action:** Move the import to an appropriate layer, or introduce an abstraction that inverts the dependency. Configure the forbidden import rules in the repository's GauntletCI configuration file.

---

### GCI0036 · Pure Context Mutation

**Confidence:** High
**What it detects:** It tracks property getter blocks and methods annotated with the purity marker. Within those contexts, it flags any added line that contains an assignment statement — writing a value to a field or variable.
**Why it matters:** Property getters and methods marked as pure are expected to return a value without modifying any state. Frameworks, serializers, and caching layers commonly assume this contract. A mutation inside a getter or pure method can cause silent bugs with lazy initialization, incorrect caching behavior, or unexpected side effects during reflection.
**Suggested action:** Move state mutations to the property setter, the constructor, or a dedicated mutating method. If lazy initialization is genuinely needed, use a thread-safe lazy initialization pattern rather than direct assignment in a getter.

---

## Tier 8 — Dependency & Integration Safety

These rules examine how the application integrates with external systems, manages its own dependencies, and controls access. They cover the dependency injection container, HTTP client usage, authorization configuration, test quality in test files, and changes to package dependency declarations.

---

### GCI0038 · Dependency Injection Safety

**Confidence:** High / Medium / Low
**What it detects:** Three checks. First, it looks for calls that resolve a service from the dependency injection container at runtime inside application code — a pattern known as the service locator anti-pattern — excluding infrastructure registration files where such calls are legitimate. Second, it looks for direct instantiation of types whose names end with common injectable-service suffixes (Service, Repository, Manager, Handler, Client), excluding test files where direct instantiation is expected. Third, it checks whether a file that registers both singleton-lifetime and scoped-or-transient-lifetime services does so in a way that could result in a singleton capturing a shorter-lived dependency.
**Why it matters:** The service locator pattern hides dependencies and makes code difficult to test in isolation. Directly instantiating injectable types bypasses the container and prevents dependency substitution. Singletons that hold references to scoped services capture a stale instance — a subtle bug that is difficult to reproduce and diagnose.
**Suggested action:** Inject all dependencies through the constructor. Register types with the container and let the container manage their lifetimes. When a singleton genuinely needs access to a scoped service, use a scope factory to create and dispose of a scope explicitly.

---

### GCI0039 · External Service Safety

**Confidence:** High / Medium / Low
**What it detects:** Three checks on non-test files. First, it flags direct instantiation of an HTTP client object — a pattern that bypasses the connection pool managed by the factory. Second, for files that use an HTTP client, it checks whether an explicit timeout is configured anywhere in the added lines. Third, for individual HTTP call statements — GET, POST, PUT, DELETE, and generic send — it checks whether a cancellation token is being passed along with the request.
**Why it matters:** Directly instantiated HTTP clients create a new socket on each request, exhausting available sockets under load. The default HTTP client timeout is very long, meaning that a slow or unresponsive external service can hold thread pool threads for an extended period. Without cancellation token propagation, requests that the caller has already abandoned continue consuming server resources.
**Suggested action:** Use the HTTP client factory pattern and register typed clients with the dependency injection container. Configure an explicit timeout on all HTTP clients. Pass the cancellation token from the calling method through to all asynchronous HTTP operations.

---

### GCI0040 · Authorization Coverage

**Confidence:** High / Medium
**What it detects:** Three checks. First, it looks for controller files where a new public action method is added without any authorization attribute on the method or in the surrounding added code. Second, it looks for authorization attributes that specify role names as inline string literals rather than as references to a constant. Third, it looks for JWT token validation settings that weaken security — such as disabling issuer validation, audience validation, token lifetime checking, or signature key validation.
**Why it matters:** Controller actions without explicit authorization attributes may be accessible to unauthenticated users depending on the application's global configuration. Inline role name strings scattered across the codebase are error-prone and make access control auditing difficult. Weakening JWT validation settings exposes the application to token forgery, replay attacks, and man-in-the-middle attacks.
**Suggested action:** Add an explicit authorization attribute to every new controller action. Define role names as named constants in a central location and reference them. Enable all JWT validation checks and use environment-specific configuration for development relaxations rather than disabling checks globally.

---

### GCI0041 · Test Quality Gaps

**Confidence:** Medium / Low
**What it detects:** Applies only to test files. Three checks. First, it scans for newly added test skip or ignore decorators — markers that cause a test to be excluded from the test run. Second, for newly added test methods, it extracts the method name and checks whether it matches a set of known low-signal names such as Test1, TestMethod, or Method1. Third, it checks whether any test file that adds a new test attribute also adds at least one assertion keyword somewhere in the added lines.
**Why it matters:** Skipped tests give a misleading green status to a test suite while masking real failures. Tests named Test1 or TestMethod provide no documentation value and make failures hard to diagnose. Tests without assertions always pass and provide no protection against regressions.
**Suggested action:** Fix the underlying issue and re-enable skipped tests, or delete obsolete tests. Use descriptive test method names that describe the scenario being tested and the expected behavior. Ensure every test method contains at least one assertion.

---

### GCI0042 · Package Dependency Changes

**Confidence:** High / Medium / Low
**What it detects:** Applies only to project files with a `.csproj` extension. Three checks. First, it flags every newly added package reference as a low-confidence advisory, since any new dependency introduces supply chain risk. Second, it checks the name of each newly added package against a list of suspicious name patterns associated with typosquatting attacks — deliberate misspellings or variations of legitimate package names. Third, it compares the version of each package before and after the change; if a package's version number was reduced, it flags the downgrade.
**Why it matters:** New package dependencies introduce supply chain risk, licensing obligations, and the potential for transitive dependency conflicts. Typosquatted packages are a well-documented attack vector that can execute malicious code during the build process or at runtime. Downgrading a package version may reintroduce security vulnerabilities that were fixed in the higher version.
**Suggested action:** Verify every new package against its publisher on the public package repository. Review the package license. Scan for known vulnerabilities before accepting a new dependency. Confirm that any version downgrade is intentional and that the lower version is not affected by vulnerabilities fixed in the higher version.

---

## Non-Active Rules

### Reserved IDs

The following IDs exist in the codebase as placeholder files but do not participate in rule discovery and produce no findings.

| ID | Reason |
|----|--------|
| GCI0028 | Unassigned ID gap — reserved for future use |
| GCI0030 | Reserved — functionality consolidated into GCI0024 (Resource Lifecycle) |
| GCI0033 | Reserved — thread-safety checks originally merged into GCI0016; static mutable field detection removed in rewrite |

---

## Configuration Reference

GauntletCI reads a configuration file named `.gauntletci.json` from the repository root. All settings are optional.

**Enabling and disabling individual rules**

Each rule runs by default. To turn off a rule for your repository, add its ID to the Rules section of the configuration file and set Enabled to false. To make a rule's findings treated as a more or less severe signal, set its Severity to High, Medium, or Low.

**Configuring architectural layer boundaries (GCI0035)**

The Architecture Layer Guard rule requires explicit configuration to do anything useful. You define the boundaries by providing a map of layer names to the namespaces each layer must not import. The layer name is matched against the file path, and the forbidden entries are matched against the namespace in each import statement. For example, you might say that files in the Domain layer must not import from Infrastructure or from web framework namespaces, and that files in the Application layer must not import from Infrastructure.

**Configuration file structure**

The configuration file has two top-level sections:

- Rules — an object where each key is a rule ID and the value is an object with optional Enabled and Severity fields.
- ForbiddenImports — an object where each key is a layer name fragment matched against file paths, and each value is a list of namespace fragments that layer must not import.

---

## Rule Counts

| Status | Count |
|--------|-------|
| Active | 39 |
| Reserved / Consolidated | 2 (GCI0030, GCI0033) |
| Unassigned | 1 (GCI0028) |
| **Total IDs used** | **42** |

---

*Last updated: 2026-04-13*
