# GauntletCI Rule Reference

GauntletCI is a deterministic change-risk detection engine. Before a pull request is merged, it reads the diff: the exact lines added and removed: and evaluates them against a structured rule set. When a rule detects a risk, it produces a **finding** that describes what was found, why it matters, and what the reviewer should do about it.

Rules do not modify code and do not block merges on their own. They surface information for human reviewers, automation pipelines, and auditors to act on. Every finding carries a **confidence level** that indicates how certain the rule is about the risk it has identified.

---

## How to Read This Document

**Rule ID**: A stable identifier in the format `GCIxxxx`. IDs are never reused, even if a rule is retired.

**Confidence levels:**
- **High**: The pattern detected is almost always a genuine problem. Findings at this level should be treated as blockers unless explicitly reviewed and accepted.
- **Medium**: The pattern is likely a problem but context matters. A reviewer should inspect and confirm before merging.
- **Low**: The pattern is a known risk indicator but has a meaningful false-positive rate. Treat as an advisory signal.

**What "findings" mean**: A finding does not mean the code is wrong. It means GauntletCI detected a pattern that warrants human attention. Reviewers decide whether to address it or acknowledge it as intentional.

**How rules are configured**: Rules run automatically against every diff. Per-repository configuration can disable individual rules or adjust their severity. See the Configuration Reference section at the end of this document.

**Default severity:** Each active rule has a built-in Block, Warn, or Info default in `DefaultSeverities.cs`. Some rules (GCI0003, GCI0004) also set per-finding `SeverityOverride` for finer calibration. Rules with default **None** (GCI0054, GCI0055) are not executed unless you raise their severity in config.

---

## Implementation status

| Category | Count | Rule IDs |
|----------|------:|----------|
| **Implemented** (`IRule` classes in Core) | 39 | All classes under `Rules/Implementations/` |
| **Active** (emit findings by default) | 37 | GCI0001, GCI0003–GCI0007, GCI0010, GCI0012, GCI0015–GCI0016, GCI0019, GCI0020–GCI0022, GCI0024, GCI0029, GCI0032, GCI0035–GCI0036, GCI0038–GCI0039, GCI0041–GCI0049, GCI0050–GCI0053, GCI0056–GCI0059 |
| **Implemented, disabled by default** | 2 | GCI0054 (async void — use GCI0016), GCI0055 (regex signatures — use GCI0003) |
| **Reserved / consolidated** | 3 | GCI0028 (unassigned), GCI0030 (→ GCI0024), GCI0033 (→ GCI0016) |
| **Documented below, not yet implemented** | 17 | GCI0002, GCI0005, GCI0008–GCI0009, GCI0011, GCI0013–GCI0014, GCI0017–GCI0018, GCI0023, GCI0025–GCI0027, GCI0031, GCI0034, GCI0037, GCI0040 |

Sections marked **Status: Not yet implemented** describe planned behavior only. The engine discovers rules via reflection on `IRule` classes under `Rules/Implementations/`; IDs without a class never run, regardless of `.gauntletci.json`.

---

## Corpus validation (agent / internal)

Ground-truth labels live in the agent corpus at `%USERPROFILE%\.gauntletci\corpus.db` (not the empty `./data/gauntletci-corpus.db` shell).

**Last queried:** 2026-06-13 (`python scripts/corpus-validation-summary.py` after discovery `run-all` + `corpus score`).

| Metric | Value |
|--------|------:|
| Fixtures | 608 (606 Discovery, 2 Gold) |
| Human gold `expected_findings` rows | 414 |
| Distinct rules with gold labels | 41 |
| Rule runs stored | 6,162 |
| `aggregates` table last updated | 2026-06-13 |
| Audit snapshot last taken | 2026-06-13 |

**Audit snapshot (labeled subset):** tp/fp/fn from `expected_findings` vs latest completed run (`audit_snapshot_rows` snapshot #17).

| Rule | Labeled | TP | FP | FN | Precision |
|------|--------:|---:|---:|---:|----------:|
| GCI0004 | 53 | 31 | 11 | 3 | 0.74 |
| GCI0003 | 44 | 27 | 9 | 0 | 0.75 |
| GCI0006 | 29 | 17 | 4 | 0 | 0.81 |
| GCI0015 | 26 | 11 | 5 | 0 | 0.69 |
| GCI0024 | 19 | 2 | 7 | 0 | 0.22 |
| GCI0010 | 19 | 1 | 6 | 1 | 0.14 |

**Gold labels vs latest run** (414 `expected_findings` rows in DB; min 3 labeled pairs per rule):

| Rule | TP | FP | FN | Precision |
|------|---:|---:|---:|----------:|
| GCI0004 | 31 | 11 | 0 | 0.74 |
| GCI0003 | 27 | 9 | 0 | 0.75 |
| GCI0006 | 17 | 4 | 0 | 0.81 |
| GCI0015 | 11 | 5 | 0 | 0.69 |
| GCI0010 | 1 | 6 | 0 | 0.14 |
| GCI0024 | 2 | 7 | 0 | 0.22 |
| GCI0016 | 6 | 0 | 0 | 1.00 |

**Discovery-tier trigger rates** (606-fixture sweep; scored 2026-06-13). Per-fixture `expected.json` exists on only ~24 fixtures, so `aggregates.precision_score` for Discovery is mostly 0 — use the gold-label table above for labeled precision.

| Rule | Trigger rate |
|------|-------------:|
| GCI0019 | 0.22 |
| GCI0003 | 0.18 |
| GCI0006 | 0.12 |
| GCI0024 | 0.09 |
| GCI0001 | 0.08 |
| GCI0045 | 0.06 |
| GCI0043 | 0.06 |
| GCI0032 | 0.06 |

**High trigger, low gold precision (noise candidates):** GCI0019 (22% trigger, unlabeled discovery), GCI0010 (0.14 gold precision), GCI0024 (0.22). GCI0001 trigger dropped to 8% post-#264 companion-file fix (was ~48% in April aggregates).

**Refresh after rule changes:**

1. `.\scripts\refresh-agent-corpus.ps1` — or manually:
2. `gauntletci corpus run-all --tier discovery --db %USERPROFILE%\.gauntletci\corpus.db`
3. `gauntletci corpus score --db %USERPROFILE%\.gauntletci\corpus.db`
4. `python scripts/corpus-validation-summary.py` — update this section from the JSON output
5. `python scripts/corpus-audit-snapshot.py` — refresh `audit_snapshots`
6. `python scripts/build-rule-audit.py --full-corpus` — regenerate `eval/rule-audit.json` (uses `corpus_db_read` indexes; ~10s on agent DB)
7. `python scripts/corpus-benchmark-discovery-drift.py` — benchmark page discovery table vs agent DB (skip in CI with `--skip-if-missing-db`)

---

## Reviewer Guide

This document is prepared for an independent third-party review of the GauntletCI rule set. The goal of this review is to help us answer three questions honestly:

1. **Should any rules be removed or merged?** Rules that fire too broadly, punish developers for legitimate patterns, or duplicate coverage already provided by another rule create alert fatigue and erode trust in the tool. If a rule is more noise than signal, we want to know.

2. **Should any rules be adjusted?** A rule may be detecting the right *category* of risk but using patterns that are too loose (causing false positives), too strict (missing real cases), or calibrated at the wrong confidence level. If the detection logic needs narrowing or the confidence level is wrong, tell us.

3. **Are there important risk categories we have missed?** We ship 39 rule implementations today (37 active by default), and the catalog continues to evolve. We do not believe this is exhaustive. If you can identify a class of risk that regularly causes production incidents, security vulnerabilities, or data loss: and that a diff-level static analysis could plausibly detect: we want to add it.

### What GauntletCI is (and is not)

Understanding these constraints will help you evaluate the rules fairly:

- **It analyzes diffs, not full codebases.** GauntletCI sees only the lines that were added or removed in a pull request. It cannot reason about the overall structure of the codebase, call graphs, or runtime state. A rule that would require reading the entire file or tracing a call chain is out of scope.
- **Core detection is fully deterministic.** There is no machine learning in the rule engine. Optional LLM enrichment and paid integrations (GitHub, webhooks, license check, Shared telemetry) may use the network when you enable them. Every core detection is a text pattern, a structural heuristic, Roslyn diagnostic, or count.
- **It is designed for fast pre-commit feedback.** Typical small staged diffs complete in seconds. Large diffs or full codebase scans take longer. Rules use diff-line heuristics plus targeted Roslyn parsing on changed C# files (no full solution build).
- **It is not a replacement for a security scanner or linter.** GauntletCI is a change-risk signal, not a comprehensive static analysis platform. It is meant to surface things a human reviewer might miss at merge time: not to replace SAST tools, dependency auditors, or test coverage tools.
- **Findings are advisory by default.** No rule automatically blocks a merge. High-confidence findings are *intended* to block, but the enforcement mechanism is up to the team using the tool.
- **It is primarily focused on C#/.NET.** Some rules are language-agnostic (file naming, diff shape, logging patterns). Most rules target C# idioms. If you have expertise in another stack, note it: we may expand language support later.

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
- Feedback on the tool's UI, CLI, or configuration format: this review is focused solely on the rule logic.

---

## Tier 1: Structural & Scope Integrity

These rules examine the shape of the diff itself rather than the code it contains. They ask whether the change is focused, well-described, and reviewable as a single unit. Large, unfocused, or misdescribed diffs are harder to review and increase the chance that unintended changes slip through.

---

### GCI0001 · Diff Integrity

**Default severity:** Warn  
**Confidence:** Medium / Low
**What it detects:** Two separate checks. First, it looks for diffs that mix source code files with non-code files such as images, configuration, or documentation: a signal that two unrelated concerns have been bundled into one change. Second, it scans each file for lines where the only change is whitespace or blank lines; if more than 40% of the changed lines in a file are whitespace-only, it flags excessive formatting churn.
**Why it matters:** Mixed-scope diffs force reviewers to context-switch and make it harder to spot logic changes hiding behind visual noise. Formatting churn obscures real intent.
**Suggested action:** Split code changes and non-code changes into separate pull requests. Run the project's formatter in a dedicated commit rather than mixing it with logic changes.

---

### GCI0002 · Goal Alignment

**Status:** Not yet implemented (spec only)

**Confidence:** Low
**What it detects:** Two checks. First, it compares keywords in the commit message against the paths of changed files. If the diff touches more than three files and no words from the commit message appear in any of the file paths, it flags a possible mismatch between the stated purpose and the actual change. Second, if the diff touches more than five files and spans at least three of the four categories: frontend, backend, configuration, and tests: it flags an overly broad scope.
**Why it matters:** Commits that do not match their description break `git blame` traceability and confuse future investigators. Cross-cutting changes are harder to review and harder to roll back.
**Suggested action:** Update the commit message to reflect what was actually changed, or split the change into focused commits grouped by concern.

---

### GCI0017 · Scope Discipline

**Status:** Not yet implemented (spec only)

**Confidence:** Low
**What it detects:** Two checks. First, it counts how many distinct top-level directories are affected by the diff across all changed files, including non-code files. If three or more top-level directories are touched, it flags the diff as spanning too many modules. Second, it checks whether production code files and non-production files: such as database migrations, seed data, or test fixtures: are changed in the same diff.
**Why it matters:** Changes that reach across many modules increase merge conflict risk and make rollback harder because a single revert undoes unrelated work. Mixing data migrations with production logic makes the diff harder to understand and audit.
**Suggested action:** Split changes by module. Commit data migrations and seed files separately from production code changes.

---

### GCI0019 · Confidence and Evidence

**Default severity:** Info
**Confidence:** Low
**What it detects:** Two checks. First, it identifies binary files: such as images, PDFs, compiled executables, or font files: included in the diff, since their contents cannot be inspected by static analysis. Second, it runs as a post-processor after all other rules: if the diff changes more than 200 lines but no other rules produced findings, it flags the possibility of hidden risks that deterministic rules did not catch.
**Why it matters:** Binary files cannot be scanned for credentials, logic errors, or security issues using text-based analysis. Large diffs with no findings may simply mean the code is clean, but they may also mean the risks are subtle enough to evade automated checks.
**Suggested action:** Review binary files manually and consider storing large binaries in Git LFS. For large diffs with no findings, consider a manual deep review or enabling LLM-assisted analysis.

---

### GCI0020 · Resource Exhaustion Pattern Detection

**Default severity:** Block
**Confidence:** High / Medium
**What it detects:** Timeout or iteration-limit removal, unbounded resource limit increases, cleanup/disposal removal, and async operations without bounds.
**Why it matters:** Removing timeouts and iteration guards enables denial-of-service and resource exhaustion under adversarial or accidental load.
**Suggested action:** Restore timeout/deadline protection or document why the operation is bounded by other means.

---

### GCI0020 (legacy doc note)

The former "Accountability Standard" checks (credentials in literals, commented-out code blocks, empty `[Authorize(Roles = "")]`, unreachable code) are not part of the current GCI0020 implementation. Security credential patterns are primarily covered by **GCI0010** and **GCI0012**.

---

## Tier 2: Behavioral & Correctness Risk

These rules examine logic changes that could alter how the software behaves at runtime. They look for removed decision paths, changed interfaces, missing tests, unvalidated inputs, uncaught errors, and structural complexity that makes behavior hard to reason about.

---

### GCI0003 · Behavioral Change Detection

**Default severity:** Block for incompatible signatures; Warn for logic removal without tests; Info for backward-compatible extensions
**Confidence:** Medium / Low / High (cryptographic boundary changes)
**What it detects:** Logic removal without test changes (15+ control-flow lines), incompatible method signature changes (with cross-file dedup), backward-compatible signature extensions, and cryptographic boundary argument changes.
**Why it matters:** Deleting conditional logic without updating tests can silently break behavior. Incompatible signature changes break callers. Compatible extensions still warrant review for positional-argument callers.
**Suggested action:** Update or add tests for removed logic. Confirm callers of changed signatures are updated; prefer overloads for backward compatibility.

---

### GCI0004 · Breaking Change Risk

**Default severity:** Warn; Block when `[Obsolete]` is removed
**Confidence:** Medium
**What it detects:** `[Obsolete]` attribute added to or removed from members in production C# files.
**Why it matters:** Adding `[Obsolete]` locks in a deprecation contract. Removing `[Obsolete]` may strip a deprecation guard before downstream consumers have migrated.
**Suggested action:** Ensure `[Obsolete]` messages name a successor. Confirm removal is intentional and external callers have migrated.

---

### GCI0005 · Test Coverage Relevance

**Status:** Not yet implemented (spec only)

**Confidence:** Medium
**What it detects:** Two checks. First, if the diff modifies production code files but contains no changes to test files, it flags the absence of test changes. Second, if the diff modifies test files but contains no changes to production code files, it flags the test changes as potentially orphaned.
**Why it matters:** Code changes without test updates increase regression risk. Test changes without corresponding production code changes may indicate tests written for code not yet implemented, or that the production file was accidentally excluded from the diff.
**Suggested action:** Add or update tests alongside any production code changes. If only tests changed intentionally, explain why in the pull request description.

---

### GCI0006 · Edge Case Handling

**Confidence:** Medium
**What it detects:** Two checks, plus optional static analysis results. First, it scans new code for lines that access a `.Value` property: a pattern used to unwrap nullable types: without a preceding null check within five lines. Second, it checks whether new methods with string or object parameters include argument validation within the first few lines of the method body. It also incorporates findings from Roslyn's static analysis rule CA1062, which detects parameters used without null validation.
**Why it matters:** Accessing a nullable's value without first checking that a value exists throws an exception at runtime. Unvalidated parameters can cause null reference errors or incorrect behavior deeper in the call stack, far from where the bug was introduced.
**Suggested action:** Add null checks before accessing nullable values. Add argument guards at the top of new methods. Use built-in validation helpers where available.

---

### GCI0007 · Error Handling Integrity

**Confidence:** High
**What it detects:** Two checks, plus optional static analysis results. First, it looks for newly added catch blocks that contain no meaningful content: no rethrow, no logging, no error recording. These are "swallowed" exceptions where the error is silently discarded. Second, it checks whether error-level log calls were removed from within error-handling blocks without being replaced, reducing the number of error log calls in that block.
**Why it matters:** Empty catch blocks make failures invisible. When an exception is swallowed, the application continues running in a potentially invalid state with no evidence of what went wrong. Removing error logs from catch blocks eliminates critical context that would otherwise be available during incident triage.
**Suggested action:** Log the exception, rethrow it, or handle it explicitly in every catch block. Preserve error-level logging in exception handlers so that failures are always visible in production.

---

### GCI0008 · Complexity Control

**Status:** Not yet implemented (spec only). Overlapping concerns are partially covered by **GCI0045**.

**Confidence:** Low
**What it detects:** Three checks. First, it tracks brace nesting depth across added lines in each file; if any point in the added code reaches more than four levels of nesting, it flags the file. Second, it identifies method-like blocks in the added code that contain more than thirty lines. Third, it looks for lines that appear three or more times verbatim across all added lines in the diff, which suggests duplicated logic.
**Why it matters:** Deep nesting makes code harder to read, test, and change without introducing bugs. Long methods are difficult to understand in isolation and tend to accumulate responsibilities over time. Duplicated logic creates maintenance debt - a fix in one copy must be replicated in all others.
**Suggested action:** Extract nested logic into private helpers and use early-return guard clauses to reduce nesting. Break long methods into smaller, focused functions. Extract repeated logic into a shared method or constant.

---

### GCI0047 · Naming/Contract Alignment

**Confidence:** Medium
**What it detects:** Two checks. First, it scans method signatures in non-test .cs files for renames where the new CRUD verb is a semantic contradiction of the old verb on the same base name. It extracts the verb prefix (Get, Add, Create, Find, Fetch, Load, Save, Insert) and the base suffix from removed and added method signatures in the same file, then reports any pair where the same base name now carries a destructive verb (Delete, Remove) in place of a constructive or read verb (or vice versa). Second, it checks whether a boolean property or field name was renamed to its semantic inverse in the same file - for example IsEnabled changed to IsDisabled, or IsActive changed to IsInactive.
**Why it matters:** A method renamed from AddUser to RemoveUser has the same parameter list but the opposite effect on the system. Callers written against the old contract will silently do the wrong thing. The compiler cannot catch this because the signature is otherwise valid. Boolean inversions are equally dangerous: every conditional that checked IsEnabled is now logically negated without any change to the condition itself.
**Suggested action:** Confirm the rename is intentional and that the behavior changed to match the new name. If the behavior did not change, revert the rename. If the behavior did change, audit every call site to verify it was also updated. For boolean inversions, prefer keeping the positive-form name and changing the stored value rather than inverting the name.
**Known limitations:** Fires only when both the old and new method definitions appear in the same file. If the rename spans files, or if only the definition was updated without updating the callers, this rule will not fire. Test files are excluded.

---

## Tier 3: Security & Compliance

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

**Status:** Not yet implemented (spec only)

**Confidence:** High / Medium
**What it detects:** Three checks. Database Data Definition Language (DDL) statements: such as dropping tables, truncating data, dropping columns, or dropping indexes: in added lines. File deletion and process-termination API calls. Database migration files that define an upgrade path without a corresponding rollback path.
**Why it matters:** DDL statements that destroy or restructure data cannot be undone once executed in most database engines. File deletion is permanent unless backups exist. A migration with no rollback method means that reverting a bad deployment requires manual intervention rather than a single automated command.
**Suggested action:** Test DDL changes in staging before production and ensure backups exist. Use soft-delete patterns instead of physical deletion where possible. Implement the rollback method in every migration file.

---

### GCI0021 · Data & Schema Compatibility

**Confidence:** High / Medium
**What it detects:** Two checks. First, it scans removed lines for serialization and schema attributes: such as JSON property names, database column mappings, validation decorators, and primary/foreign key declarations: that have been deleted from entity or model classes. Second, it parses the full diff context of each file to detect when a member is removed from an enumeration type.
**Why it matters:** Serialization attributes and column mappings define the contract between the application and its data stores, message queues, and API consumers. Removing them silently changes how data is read and written, breaking deserialization of records that were stored under the old schema. Removing enumeration members breaks deserialization of stored integer or string values that mapped to the removed entry.
**Suggested action:** Keep removed properties and mark them as obsolete rather than deleting them outright. Version schema changes explicitly with a migration. Use enumeration deprecation patterns rather than removal.

---

### GCI0029 · PII Entity Logging Leak

**Confidence:** High
**What it detects:** It scans every line added to source files for log calls that include terms associated with personally identifiable information. The monitored terms include email addresses, social security numbers, phone numbers, credit card numbers, dates of birth, passport numbers, national identifiers, tax identifiers, bank account numbers, physical addresses, usernames, postal codes, geographic location data, device identifiers, and authentication tokens.
**Why it matters:** Personal data written to application logs propagates to log aggregators, log storage systems, and third-party monitoring services. This constitutes a data breach under GDPR, CCPA, and HIPAA: even if the log is not publicly accessible. Log data is often retained for long periods and accessed by systems and personnel that should not have access to personal data.
**Suggested action:** Remove personal data from log calls. Log only anonymized identifiers such as user IDs, not names, email addresses, or other identifying attributes.

---

## Tier 4: Resource & Concurrency Safety

These rules detect patterns that cause performance degradation, data corruption, race conditions, and resource leaks. They cover threading hazards, unmanaged resource disposal, data validation gaps, and operations that are unsafe to retry.

---

### GCI0011 · Performance Risk

**Status:** Not yet implemented (spec only). Overlapping concerns are partially covered by **GCI0044**.

**Confidence:** Medium
**What it detects:** Four checks, all targeting added code within loop constructs. It flags materializing a collection to a list or array inside a loop body, which causes repeated full-enumeration allocations. It flags using a count-based existence check where a short-circuit check would suffice, causing the entire collection to be enumerated even when only the first matching element is needed. It flags allocating a new list or dictionary inside a loop, which generates garbage collection pressure on every iteration. It flags building strings with concatenation inside a loop, which is quadratic in complexity due to the immutability of strings.
**Why it matters:** These patterns appear correct and produce correct output but perform poorly at scale. They are among the most common causes of performance regressions that are expensive to diagnose after deployment.
**Suggested action:** Materialize collections before entering loops. Use the appropriate short-circuit check for existence. Allocate collections outside loops and clear between iterations. Use a string builder for multi-step string construction.

---

### GCI0015 · Data Integrity Risk

**Confidence:** High / Medium / Low
**What it detects:** Four checks. First, it looks for sequences of three or more consecutive field assignments in files that also receive HTTP request data: a pattern associated with over-posting, where a client can set fields the developer did not intend to expose. Second, it checks for the same consecutive assignment pattern in general code without accompanying null guards, flagging potential mass assignment without validation. Third, it looks for explicit numeric type casts: such as converting a value to an integer or a decimal: on lines that may involve user-supplied data, without overflow checking. Fourth, it looks for SQL statements that silently swallow insert conflicts rather than surfacing them.
**Why it matters:** Over-posting allows attackers to write to internal fields of an entity by including extra properties in a request payload. Unchecked numeric casts can silently truncate values or throw overflow exceptions. Silent insert-conflict suppression hides data integrity violations that should trigger an error or a deliberate upsert decision.
**Suggested action:** Use a dedicated data transfer object to control which fields can be set from request data. Add input validation before assignment. Use checked arithmetic or safe parsing methods. Handle insert conflicts explicitly rather than suppressing them.

---

### GCI0016 · Async Concurrency Risk

**Confidence:** High / Medium
**What it detects:** Four checks on added lines (production code unless noted). Before pattern matching, each line is normalized with **`ForPatternScan`**: inline `//` comments are stripped and double-quoted string literal contents are blanked so mentions in comments or strings (for example `// async void` or `"foo.Wait()"`) do not produce findings. Whole-line `//` comments are skipped. **`async void`** is detected with a method-shape regex (`async void` + identifier + `(`), not a bare substring match — except legitimate event handler signatures with `(object sender, …EventArgs e)` parameters. It flags blocking async calls: `.Wait()`, `.GetAwaiter().GetResult()`, and `.Result` when the expression clearly operates on a Task. It flags `lock(this)`. It flags `Thread.Sleep()` in production code (test files exempt).
**Scope note:** Classic thread-safety patterns (static mutable fields, monitor misuse beyond `lock(this)`) were removed from this rule in the rewrite; use static analysis tools with full type information for those concerns. **GCI0054** (public-only async void) is disabled by default — prefer GCI0016.
**Why it matters:** `async void` methods cannot be awaited: exceptions they throw crash the process via `AppDomain.UnhandledException` with no way for the caller to recover. Blocking on async operations in a `SynchronizationContext` (ASP.NET, Blazor, WPF) deadlocks because the continuation needs the thread that is already blocked. `lock(this)` creates an external deadlock vector. `Thread.Sleep` wastes a thread pool thread and degrades throughput under load.
**Suggested action:** Change `async void` to `async Task`. Use `await` instead of `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`. Replace `lock(this)` with a dedicated `private readonly object _lock = new()`. Replace `Thread.Sleep()` with `await Task.Delay()`. In tests that must embed risky patterns in diff fixtures, use indirection (for example `const string` fragments) so added diff lines do not trigger self-analysis.

---

### GCI0022 · Idempotency & Retry Safety

**Confidence:** Medium / Low
**What it detects:** Three checks. First, it looks for HTTP POST endpoint declarations in controller files without any idempotency key handling in the surrounding code: such as a request identifier, deduplication key, or message ID. Second, it looks for raw SQL INSERT statements without a conflict-resolution clause that would make the insert safe to retry. Third, it looks for event handler subscriptions being added without a corresponding unsubscription or a guard that prevents the handler from being attached more than once.
**Why it matters:** Operations that are not idempotent produce duplicate side effects when retried: such as charging a customer twice, creating duplicate records, or firing a business event multiple times. Network errors, message queue redelivery, and browser double-submits are common retry scenarios that every public-facing endpoint must handle safely.
**Suggested action:** Add idempotency key support to POST endpoints and validate the key server-side. Use upsert semantics or unique constraints for database inserts. Unsubscribe before subscribing to events, or guard subscriptions with a flag.

---

### GCI0024 · Resource Lifecycle

**Confidence:** High / Medium
**What it detects:** It scans added lines for objects whose types implement the disposable pattern: including file streams, database connections, network clients, cryptographic objects, synchronization primitives, and any type whose name ends with a suffix commonly associated with disposable resources such as Stream, Reader, Writer, Connection, Client, Context, or Transaction. It checks whether the allocation is wrapped in a disposal guarantee. It also incorporates static analysis results for disposable resource management.
**Why it matters:** Types that manage operating system handles, database connections, or network sockets must be explicitly released when no longer needed. Without a disposal guarantee, resources leak under exceptions, exhausting connection pools or file handles over time.
**Suggested action:** Wrap every disposable allocation in a usage block that guarantees disposal even when exceptions occur. For types injected via the dependency injection container, rely on the container's lifetime management.

---

## Tier 5: Observability & Maintainability

These rules protect the long-term health of the codebase and the team's ability to diagnose and operate the system in production. They cover logging quality, code consistency, production readiness markers, documentation, and test quality.

---

### GCI0009 · Consistency with Patterns

**Status:** Not yet implemented (spec only). Overlapping concerns are partially covered by **GCI0046**.

**Confidence:** Low
**What it detects:** Two checks. First, if the diff context shows the existing codebase uses the asynchronous programming model, it looks for new methods whose names or return types suggest they should be asynchronous but are not declared as such. Second, it incorporates static analysis results that flag inconsistent string comparison styles and naming convention violations.
**Why it matters:** Inconsistent use of asynchronous patterns makes code difficult to reason about and can introduce subtle deadlocks when mixing synchronous and asynchronous code paths. Inconsistent naming conventions reduce readability across a shared codebase.
**Suggested action:** Use the asynchronous model consistently, or explicitly return a completed result where synchronous behavior is intended. Follow the project's established naming conventions.

---

### GCI0013 · Observability/Debuggability

**Status:** Not yet implemented (spec only)

**Confidence:** Low
**What it detects:** It checks each file where twenty or more lines have been added. If no logging calls are detected anywhere in those added lines, it flags the file as potentially unobservable.
**Why it matters:** Code paths without any logging are opaque in production. When something goes wrong in a file with no log output, there is no trail to follow during incident investigation.
**Suggested action:** Add logging at entry and exit points of significant operations, and at all error paths, so that the behavior of the code is visible in production logs.

---

### GCI0018 · Production Readiness

**Status:** Not yet implemented (spec only)

**Confidence:** Medium
**What it detects:** Three checks. First, it counts added lines containing work-in-progress markers such as TODO, FIXME, HACK, or XXX. Second, it looks for statements that throw a placeholder exception indicating a method body has not been implemented. Third, it checks non-test, non-CLI files for diagnostic output calls: such as writing directly to the console: and for debug assertion calls that are silently stripped in release builds.
**Why it matters:** Work-in-progress markers indicate incomplete work being shipped. Placeholder exceptions crash callers at runtime. Console output bypasses the application's logging infrastructure and is lost in production. Debug assertions that are stripped in release builds provide no runtime protection.
**Suggested action:** Resolve or convert all markers to tracked issues before merging. Implement all placeholder methods. Replace console output and debug assertions with structured logging and proper runtime guards.

---

### GCI0023 · Structured Logging

**Status:** Not yet implemented (spec only). PII-in-logs is covered by **GCI0029**.

**Confidence:** Medium / Low
**What it detects:** Two checks. First, it looks for log calls where the message is constructed using string interpolation: embedding values directly into the message string: rather than using named message template placeholders. Second, for files in critical business domains such as authentication, payment, billing, and order processing, it checks whether logging calls include a correlation or trace identifier.
**Why it matters:** String interpolation in log messages prevents log aggregation systems from indexing structured fields, making log data unsearchable and unqueryable. Without correlation identifiers, tracing a single request across multiple services during an incident is extremely difficult.
**Suggested action:** Use named message template placeholders and pass values as separate arguments so that log aggregators can index them as queryable fields. Include a correlation, request, or trace identifier in all log calls on critical business paths.

---

### GCI0025 · Feature Flag Readiness

**Status:** Not yet implemented (spec only)

**Confidence:** Medium
**What it detects:** For files in critical business domains: including authentication, payment, billing, order processing, subscriptions, tokens, credentials, and passwords: it checks whether large changes (fifty or more added lines) include a reference to a feature flag or toggle mechanism. Recognized flag systems include common feature management libraries and interfaces.
**Why it matters:** Large changes to high-value code paths that are shipped without a feature flag cannot be rolled back without a full redeployment. A feature flag allows the new behavior to be disabled instantly in production if it causes incidents, without touching the code.
**Suggested action:** Wrap significant new behavior behind a feature flag so that it can be disabled in production without a code change or redeployment.

---

### GCI0026 · Documentation Adequacy

**Status:** Not yet implemented (spec only)

**Confidence:** Low
**What it detects:** For each public method added to a source file, it checks whether an XML documentation comment block appears immediately above the method declaration, allowing for attribute decorators between the comment and the method signature.
**Why it matters:** Public methods are part of a shared API surface. Without documentation comments, callers must read the implementation to understand parameters, return values, and expected behavior: especially costly in shared libraries and services.
**Suggested action:** Add a summary documentation block above every new public method describing its purpose, parameters, and return value.

---

### GCI0027 · Test Quality

**Status:** Not yet implemented (spec only). Overlapping concerns are partially covered by **GCI0041**.

**Confidence:** High / Medium
**What it detects:** For each test method added in test files, it checks whether the method body contains any assertion: a statement that verifies an expected outcome. If the method has no assertion at all, it is flagged at High confidence. If the method's only assertions are null-check assertions: checking that a value exists without verifying what the value is: it is flagged at Medium confidence.
**Why it matters:** A test with no assertions always passes regardless of what the code does, providing false confidence and zero protection against regressions. Assertions that only check non-null confirm that something was returned but not whether it was the correct thing.
**Suggested action:** Add value-level assertions that verify the actual output of the code under test matches the expected output for the given inputs.

---

## Tier 6: Evidence & Test Completeness

These rules require that behavioral changes in production code are accompanied by corresponding test evidence in the same diff. They detect untested exception paths, untested boundary conditions, untested null-handling behavior, and unvalidated object mapping configurations.

---

### GCI0031 · Boundary Drift

**Status:** Not yet implemented (spec only)

**Confidence:** Medium
**What it detects:** It collects every numeric literal used in a comparison operation: less than, greater than, or their inclusive equivalents: in non-test files. For each such literal, it checks whether a test file in the same diff contains that same value in a test data row or assertion. If the boundary value appears in production logic but has no corresponding test coverage in the diff, it is flagged.
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

**Status:** Not yet implemented (spec only)

**Confidence:** Low
**What it detects:** It counts the number of newly added null-conditional or null-coalescing operators: patterns used to provide default behavior when a value is absent: in non-test files. If any such operators exist, it checks whether any test file in the same diff passes a null value to the code being tested. If null guards are added without null-input test coverage, it flags the gap.
**Why it matters:** A null guard that is never tested with null input may be masking a null reference exception source rather than intentionally handling a valid null case. The fallback behavior needs test coverage to confirm it is correct.
**Suggested action:** Add a test case that passes null for the value being guarded and verifies that the fallback behavior produces the expected result.

---

### GCI0037 · Mapping Profile Integrity

**Status:** Not yet implemented (spec only)

**Confidence:** Medium
**What it detects:** Four independent checks, one for each of the major object mapping libraries in use in the .NET ecosystem: AutoMapper, Mapster, AgileMapper, and TinyMapper. For each library, it detects whether mapping configuration was added or changed in the diff, and if so, whether the diff also contains the corresponding compile-time validation call specific to that library. For TinyMapper, which has no built-in compile-time validation, it always flags when a binding change is detected.
**Why it matters:** Object mapping configurations that are incorrect or incomplete fail at runtime when the mapping is first exercised, not at compile time. Without a test that validates the mapping configuration, broken mappings reach production.
**Suggested action:** Call the appropriate compile-time validation method for the mapping library in a unit test after every mapping configuration change. For TinyMapper, add a test that exercises the binding with representative data.

---

## Tier 7: Architecture & Structural Contracts

These rules enforce structural invariants that preserve the long-term integrity of the codebase. They check that architectural layer boundaries are respected and that code which is expected to be free of side effects does not introduce mutations.

---

### GCI0035 · Architecture Layer Guard

**Confidence:** High / Low
**What it detects:** It checks every newly added import statement against a configured list of forbidden import pairs. Each pair defines a source layer: identified by a fragment of its namespace or directory path: and a set of namespaces that layer must not import. If an import statement in a file belonging to the source layer references a forbidden namespace, it is flagged. If no forbidden import configuration has been provided, it emits a low-confidence advisory reminding the reviewer that the rule is not yet configured.
**Why it matters:** Cross-layer imports break the separation of concerns that makes a layered architecture testable and maintainable. For example, a domain model importing from an infrastructure namespace creates a coupling that prevents the domain from being tested in isolation.
**Suggested action:** Move the import to an appropriate layer, or introduce an abstraction that inverts the dependency. Configure the forbidden import rules in the repository's GauntletCI configuration file.

---

### GCI0036 · Pure Context Mutation

**Confidence:** High
**What it detects:** It tracks property getter blocks and methods annotated with the purity marker. Within those contexts, it flags any added line that contains an assignment statement: writing a value to a field or variable.
**Why it matters:** Property getters and methods marked as pure are expected to return a value without modifying any state. Frameworks, serializers, and caching layers commonly assume this contract. A mutation inside a getter or pure method can cause silent bugs with lazy initialization, incorrect caching behavior, or unexpected side effects during reflection.
**Suggested action:** Move state mutations to the property setter, the constructor, or a dedicated mutating method. If lazy initialization is genuinely needed, use a thread-safe lazy initialization pattern rather than direct assignment in a getter.

---

## Tier 8: Dependency & Integration Safety

These rules examine how the application integrates with external systems, manages its own dependencies, and controls access. They cover the dependency injection container, HTTP client usage, authorization configuration, test quality in test files, and changes to package dependency declarations.

---

### GCI0038 · Dependency Injection Safety

**Confidence:** High / Medium / Low
**What it detects:** Three checks. First, it looks for calls that resolve a service from the dependency injection container at runtime inside application code: a pattern known as the service locator anti-pattern: excluding infrastructure registration files where such calls are legitimate. Second, it looks for direct instantiation of types whose names end with common injectable-service suffixes (Service, Repository, Manager, Handler, Client), excluding test files where direct instantiation is expected. Third, it checks whether a file that registers both singleton-lifetime and scoped-or-transient-lifetime services does so in a way that could result in a singleton capturing a shorter-lived dependency.
**Why it matters:** The service locator pattern hides dependencies and makes code difficult to test in isolation. Directly instantiating injectable types bypasses the container and prevents dependency substitution. Singletons that hold references to scoped services capture a stale instance: a subtle bug that is difficult to reproduce and diagnose.
**Suggested action:** Inject all dependencies through the constructor. Register types with the container and let the container manage their lifetimes. When a singleton genuinely needs access to a scoped service, use a scope factory to create and dispose of a scope explicitly.

---

### GCI0039 · External Service Safety

**Confidence:** High / Medium / Low
**What it detects:** Three checks on non-test files. First, it flags direct instantiation of an HTTP client object: a pattern that bypasses the connection pool managed by the factory. Second, for files that use an HTTP client, it checks whether an explicit timeout is configured anywhere in the added lines. Third, for individual HTTP call statements: GET, POST, PUT, DELETE, and generic send: it checks whether a cancellation token is being passed along with the request.
**Why it matters:** Directly instantiated HTTP clients create a new socket on each request, exhausting available sockets under load. The default HTTP client timeout is very long, meaning that a slow or unresponsive external service can hold thread pool threads for an extended period. Without cancellation token propagation, requests that the caller has already abandoned continue consuming server resources.
**Suggested action:** Use the HTTP client factory pattern and register typed clients with the dependency injection container. Configure an explicit timeout on all HTTP clients. Pass the cancellation token from the calling method through to all asynchronous HTTP operations.

---

### GCI0040 · Authorization Coverage

**Status:** Not yet implemented (spec only). Partial overlap with **GCI0012** (`[AllowAnonymous]`).

**Confidence:** High / Medium
**What it detects:** Three checks. First, it looks for controller files where a new public action method is added without any authorization attribute on the method or in the surrounding added code. Second, it looks for authorization attributes that specify role names as inline string literals rather than as references to a constant. Third, it looks for JWT token validation settings that weaken security: such as disabling issuer validation, audience validation, token lifetime checking, or signature key validation.
**Why it matters:** Controller actions without explicit authorization attributes may be accessible to unauthenticated users depending on the application's global configuration. Inline role name strings scattered across the codebase are error-prone and make access control auditing difficult. Weakening JWT validation settings exposes the application to token forgery, replay attacks, and man-in-the-middle attacks.
**Suggested action:** Add an explicit authorization attribute to every new controller action. Define role names as named constants in a central location and reference them. Enable all JWT validation checks and use environment-specific configuration for development relaxations rather than disabling checks globally.

---

### GCI0041 · Test Quality Gaps

**Confidence:** Medium / Low
**What it detects:** Applies only to test files. Three checks. First, it scans for newly added test skip or ignore decorators: markers that cause a test to be excluded from the test run. Second, for newly added test methods, it extracts the method name and checks whether it matches a set of known low-signal names such as Test1, TestMethod, or Method1. Third, it checks whether any test file that adds a new test attribute also adds at least one assertion keyword somewhere in the added lines.
**Why it matters:** Skipped tests give a misleading green status to a test suite while masking real failures. Tests named Test1 or TestMethod provide no documentation value and make failures hard to diagnose. Tests without assertions always pass and provide no protection against regressions.
**Suggested action:** Fix the underlying issue and re-enable skipped tests, or delete obsolete tests. Use descriptive test method names that describe the scenario being tested and the expected behavior. Ensure every test method contains at least one assertion.

---

### GCI0042 · TODO and Stub Detection

**Default severity:** Info
**Confidence:** Medium
**What it detects:** Added lines in non-test production files containing TODO, FIXME, or HACK markers (comment lines require the marker as the first token after `//`), or `throw new NotImplementedException`.
**Why it matters:** TODOs that ship to main rarely get done. `NotImplementedException` in production code is a deferred crash.
**Suggested action:** Resolve markers and replace stubs with real implementations before merging.

---

### GCI0056 · Missing Test Framework Detection

**Confidence:** Medium
**What it detects:** Scans the changed files to determine if the repository has production code without a corresponding test infrastructure. It counts the number of production source files, checks for the presence of test files (matching known test file patterns), and searches for test framework package references (xunit, NUnit, MSTest, Jest, pytest, etc.). If the repository contains 3+ production source files, has a project file (.csproj, package.json, pyproject.toml), but no test files and no test framework packages detected, it flags the gap. Exempt directories: samples/, examples/, docs/, tools/.
**Why it matters:** Production code without any test infrastructure has zero protection against regressions. Every code change carries unknown risk. Automated tests are the primary defense against introducing bugs, and their absence signals a project that may be unmaintained or immature.
**Suggested action:** Add a test project to your repository. Reference a testing framework appropriate to your language: xunit or NUnit for C#, Jest or Mocha for JavaScript, pytest for Python. Write tests for the critical paths of your application. Aim for high coverage of business logic.

---

### GCI0057 · Synchronous File I/O

**Default severity:** Warn
**Confidence:** High (inside async methods) / Medium
**What it detects:** Synchronous `File.ReadAllText`, `File.WriteAllText`, `File.ReadAllLines`, `File.WriteAllLines`, `File.Copy`, `File.ReadAllBytes`, and `File.WriteAllBytes` in added production lines. Exempt: `Program.cs`, `Startup.cs`, `AssemblyInfo.cs`, test files. Blocking async patterns (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) are handled by **GCI0016**, not this rule.
**Why it matters:** Sync file I/O blocks the calling thread during disk latency. In server apps under load, that reduces throughput compared to `File.*Async` variants.
**Suggested action:** Prefer `await File.ReadAllTextAsync(...)` and related async APIs; use `Stream.CopyToAsync` for large copies.

---

## Non-Active Rules

### Reserved IDs

The following IDs exist in the codebase as placeholder files but do not participate in rule discovery and produce no findings.

| ID | Reason |
|----|--------|
| GCI0028 | Unassigned ID gap: reserved for future use |
| GCI0030 | Reserved: functionality consolidated into GCI0024 (Resource Lifecycle) |
| GCI0033 | Reserved: thread-safety checks originally merged into GCI0016; static mutable field detection removed in rewrite |

---

## Configuration Reference

GauntletCI reads a configuration file named `.gauntletci.json` from the repository root. All settings are optional.

**Enabling and disabling individual rules**

Each rule runs by default. To turn off a rule for your repository, add its ID to the Rules section of the configuration file and set Enabled to false. To make a rule's findings treated as a more or less severe signal, set its Severity to High, Medium, or Low.

**Configuring architectural layer boundaries (GCI0035)**

The Architecture Layer Guard rule requires explicit configuration to do anything useful. You define the boundaries by providing a map of layer names to the namespaces each layer must not import. The layer name is matched against the file path, and the forbidden entries are matched against the namespace in each import statement. For example, you might say that files in the Domain layer must not import from Infrastructure or from web framework namespaces, and that files in the Application layer must not import from Infrastructure.

**Configuration file structure**

The configuration file has two top-level sections:

- Rules: an object where each key is a rule ID and the value is an object with optional Enabled and Severity fields.
- ForbiddenImports: an object where each key is a layer name fragment matched against file paths, and each value is a list of namespace fragments that layer must not import.

---

## Rule Counts

| Status | Count |
|--------|-------|
| Active | 36 |
| Disabled by default | 2 (GCI0054, GCI0055 — duplicate coverage) |
| Reserved / Consolidated | 2 (GCI0030, GCI0033) |
| Unassigned | 1 (GCI0028) |
| **Total IDs used** | **41** |

---

*Last updated: 2026-05-25*
