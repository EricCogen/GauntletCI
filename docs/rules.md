# GauntletCI Rule Reference

GauntletCI evaluates every commit diff against a deterministic rule set. Rules are organized into **tiers** based on the type of risk they detect. Each rule reports findings with a confidence level (`High`, `Medium`, or `Low`) and a suggested action.

Rules with **Hard Fail** severity are intended to block the commit when triggered at `High` confidence. Rules marked **Warn** surface advisory findings that require human judgment.

---

## Tier 1 — Structural & Scope Integrity

Rules that validate the shape and intent of the diff itself.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0001 | Diff Integrity | Medium | Detects unrelated changes, formatting churn, and mixed scope within a single diff. |
| GCI0002 | Goal Alignment | Low | Flags diffs unrelated to the commit message or spanning too many unrelated areas. |
| GCI0017 | Scope Discipline | Low | Flags diffs that touch too many distinct modules or mix production and non-production files. |
| GCI0019 | Confidence and Evidence | Low | Self-audit rule: flags large diffs with few findings, binary files, and tiny diffs. |
| GCI0020 | Accountability Standard | High | Checks for swallowed exceptions, secrets, commented-out code, and empty role assignments. |

---

## Tier 2 — Behavioral & Correctness Risk

Rules that detect logic changes likely to alter runtime behavior.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0003 | Behavioral Change Detection | Medium | Detects removed logic lines and changed method signatures. |
| GCI0004 | Breaking Change Risk | High | Detects removed public APIs and changed public method signatures. |
| GCI0005 | Test Coverage Relevance | Medium | Flags code changes without test changes, and orphaned test changes. |
| GCI0006 | Edge Case Handling | Medium | Detects potential null dereferences and missing validation in added code. |
| GCI0007 | Error Handling Integrity | High | Detects swallowed exceptions and empty catch blocks. |
| GCI0008 | Complexity Control | Low | Detects excessive nesting, long methods, and duplicate logic. |

---

## Tier 3 — Security & Compliance

Rules that detect patterns with direct security or regulatory impact. These are **Hard Fail** at `High` confidence.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0010 | Hardcoding and Configuration | Medium | Detects hardcoded IPs, URLs, connection strings, secrets, and environment names. |
| GCI0012 | Security Risk | High | Detects SQL injection, weak crypto, dangerous APIs, and credential exposure. |
| GCI0014 | Rollback Safety | High | Detects irreversible operations: DDL, file deletion, migrations without `Down()`. |
| GCI0021 | Data & Schema Compatibility | High | Detects removed serialization attributes and enum member removals that break wire formats. |
| GCI0029 | PII Entity Logging Leak | High | Detects PII terms (Email, SSN, Phone, etc.) passed to log calls. Violates GDPR/CCPA/HIPAA. |

---

## Tier 4 — Resource & Concurrency Safety

Rules that detect resource mismanagement and threading hazards.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0011 | Performance Risk | Medium | Detects common performance anti-patterns: N+1 queries, large allocations, blocking I/O. |
| GCI0015 | Data Integrity Risk | Medium | Detects unchecked casts, mass assignment without validation, and SQL IGNORE patterns. |
| GCI0016 | Concurrency and State Risk | High | Detects `async void`, blocking async calls, static mutable state, and deadlock risks. |
| GCI0022 | Idempotency & Retry Safety | Medium | Detects HTTP POST endpoints without idempotency keys and INSERT without upsert guards. |
| GCI0024 | Resource Lifecycle | High | Detects disposable resources allocated without a `using` statement or `try/finally` disposal (Roslyn CA2000/CA1001). |
| GCI0030 | IDisposable Resource Safety | High | Generalizes GCI0024 via suffix-based heuristic — catches any type ending in `Stream`, `Connection`, `Client`, `Context`, etc. without a `using` guard. |
| GCI0033 | Async Sinkhole | High | Detects `.Result` or `.Wait()` blocking calls on Tasks — causes deadlocks in sync-context environments. |

---

## Tier 5 — Observability & Maintainability

Rules that protect long-term operational health.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0009 | Consistency with Patterns | Low | Detects deviations from project-wide async/await and guard clause patterns. |
| GCI0013 | Observability/Debuggability | Low | Flags missing logging, missing XML docs, and unlogged exception re-throws. |
| GCI0018 | Production Readiness | Medium | Checks for TODO/FIXME markers, `NotImplementedException`, and debug artifacts. Also fires aggregate warning when >3 rules trigger simultaneously. |
| GCI0023 | Structured Logging | Medium | Detects log calls using string interpolation instead of structured message templates. |
| GCI0025 | Feature Flag Readiness | Medium | Detects large changes to critical-path files with no feature flag or toggle reference. |
| GCI0026 | Documentation Adequacy | Low | Detects added public methods and interfaces without XML doc comments. |
| GCI0027 | Test Quality | High | Detects test methods with no meaningful assertion, asserting only non-null, or apparent copy-paste duplicates. |

---

## Tier 6 — Evidence & Test Completeness

Rules that require test evidence to accompany behavioral changes.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0031 | Boundary Drift | Medium | Fires when comparison operators against numeric literals are added without test `InlineData`/assertion coverage at those boundary values. |
| GCI0032 | Uncaught Exception Path | Medium | Fires when `throw new` is added without `Assert.Throws` or `.Should().Throw` evidence in the diff's test files. |
| GCI0034 | Null-Coalescing Expansion | Low | Fires when `?.` or `??` operators are added without null-injection test evidence in the diff. |
| GCI0037 | AutoMapper Integrity | Medium | Fires when `CreateMap<` or AutoMapper profile changes are detected without `AssertConfigurationIsValid` in test files. |

---

## Tier 7 — Architecture & Structural Contracts

Rules that enforce structural invariants and architectural boundaries.

| ID | Rule | Confidence | Description |
|----|------|-----------|-------------|
| GCI0035 | Architecture Layer Guard | High | Checks added `using` directives against `ForbiddenImports` pairs configured in `.gauntletci.json`. Prevents layer boundary violations (e.g. Domain importing Infrastructure). |
| GCI0036 | Pure Context Mutation | High | Detects assignment operators inside `get { }` accessor blocks or methods annotated `[Pure]`. Mutations in pure contexts break caching, reflection, and framework contracts. |

---

## Backburner (Not Yet Implemented)

| ID | Rule | Confidence | Notes |
|----|------|-----------|-------|
| GCI0028 | Entropy-Based Secret Detection | High | Detects string literals >12 chars with Shannon entropy >4.5. Orthogonal to GCI0012's name-pattern approach. Deferred pending tuning of entropy threshold to minimize false positives. |
| GCI0038 | Hardened Execution (Anti-Tamper) | High | Engine-level hardening with three sub-features: (1) **Config Lockdown** — `gauntlet-allowlist.json` found in a PR is ignored unless it matches a known-good administrative hash, preventing allowlist tampering; (2) **No-Echo Logs** — flagged secrets and PII are masked in all output, reporting only FilePath and LineNumber to prevent the tool itself from leaking what it detects; (3) **Timeout Guard** — per-file analysis capped at 30 seconds to prevent Roslyn Bomb DoS attacks on the engine. |

---

## Configuration

Rules can be configured per-repo via `.gauntletci.json`:

```json
{
  "Rules": {
    "GCI0002": { "Enabled": false },
    "GCI0012": { "Severity": "High" }
  },
  "ForbiddenImports": {
    "Domain": ["Infrastructure", "AspNetCore"],
    "Application": ["Infrastructure"]
  }
}
```

### `Rules`
Per-rule overrides. Each key is a rule ID (e.g. `"GCI0012"`).
- `Enabled` — set to `false` to disable a rule entirely (default: `true`)
- `Severity` — override the default confidence level: `"High"`, `"Medium"`, or `"Low"`

### `ForbiddenImports` *(GCI0035 only)*
Defines architectural layer boundaries. Each key is a namespace fragment identifying a source layer. Values are a list of namespace fragments that layer must not import.

---

## Rule Counts

| Status | Count |
|--------|-------|
| Implemented | 36 |
| Backburner | 2 (GCI0028, GCI0038) |
| **Total** | **38** |

---

*Last updated: 2026-04-10 — GCI0029–GCI0037 added; GCI0038 Anti-Tamper added to backburner.*
