# GauntletCI Rule Catalog

GauntletCI rules detect risk introduced by code changes.

A finding is not a claim that the code is definitely broken. A finding is evidence that the diff introduced behavior worth validating.

---

## Rules by Production Risk Tier

GauntletCI organizes detection rules across 8 production risk tiers. Each tier groups rules by the category of behavioral change risk they address.

### Tier 1: Structural & Scope Integrity {#tier-1}

**What it detects**: Changes that contaminate a diff with unrelated concerns, making behavioral review unreliable.

**Why it matters**: A diff should represent a single logical change. Mixed concerns hide intent and make code review ineffective.

### Tier 2: Behavioral & Correctness Risk {#tier-2}

**What it detects**: Control-flow removals without test coverage. Method signature changes without contract updates. These are the changes most likely to produce silent regressions.

**Why it matters**: Changes that alter control flow silently break calling code without raising exceptions.

### Tier 3: Security & Compliance {#tier-3}

**What it detects**: Hardcoded secrets and infrastructure values. SQL injection patterns. Deprecated cryptography. PII written to log output.

**Why it matters**: Security issues propagate silently and may not be caught by static analysis alone.

### Tier 4: Resource & Concurrency Safety {#tier-4}

**What it detects**: Async deadlocks. Disposable leaks. Missing idempotency guarantees on retry-eligible endpoints. Unsafe shared state.

**Why it matters**: Resource and concurrency bugs are difficult to reproduce and manifest intermittently in production.

### Tier 5: Observability & Failure Handling {#tier-5}

**What it detects**: Swallowed exceptions. Removed error-level logging from error-handling paths. Silent failures that remove production visibility.

**Why it matters**: When errors are silent, teams cannot debug or resolve production issues.

### Tier 6: Evidence & Test Completeness {#tier-6}

**What it detects**: New exception-throwing paths with no corresponding throw-assertion test coverage. Changes where the risk exists but no test evidence supports it.

**Why it matters**: Code that throws exceptions must be tested for those exceptions, not just for happy-path behavior.

### Tier 7: Architecture & Structural Contracts {#tier-7}

**What it detects**: Forbidden layer dependency violations. State mutation inside property getters. Silent bugs in caching layers and serializers.

**Why it matters**: Architectural violations compound over time and make systems fragile and difficult to modify.

### Tier 8: Dependency & Integration Safety {#tier-8}

**What it detects**: Service locator anti-patterns. Direct HttpClient instantiation bypassing the connection pool. HTTP calls missing cancellation tokens. Test methods without assertions.

**Why it matters**: Integration and dependency issues cause cascading failures under load.

---

## Rule status

- **Stable**: suitable for normal use
- **Beta**: useful but still being tuned
- **Experimental**: may produce more false positives

## Rules

| Rule | Name | Category | Status |
| --- | --- | --- | --- |
| [GCI0003](GCI0003-behavioral-change-detection.md) | Behavioral Change Detection | Behavior and Contracts | Stable |
| [GCI0004](GCI0004-breaking-change-risk.md) | Breaking Change Risk | Behavior and Contracts | Stable |
| [GCI0006](GCI0006-edge-case-handling.md) | Edge Case Handling | Behavior and Contracts | Stable |
| [GCI0007](GCI0007-error-handling-integrity.md) | Error Handling Integrity | Observability and Failure Handling | Stable |
| [GCI0010](GCI0010-hardcoding-and-configuration.md) | Hardcoding and Configuration | Security and Configuration | Stable |
| [GCI0012](GCI0012-secret-hygiene.md) | Secret Hygiene | Security and Compliance | Stable |
| [GCI0016](GCI0016-concurrency-safety.md) | Concurrency Safety | Resource and Concurrency Safety | Stable |
| [GCI0029](GCI0029-pii-logging-leak.md) | PII Logging Leak | Security and Compliance | Stable |

For the full rule reference including all 30 rules, see [docs/rules.md](../rules.md).

## Best Practices Guide

Beyond deterministic risk detection, GauntletCI includes a **[Best Practices Guide](../best-practices.md)** covering 30 patterns for C# code quality across 14 categories:

- **Naming** (clarity and convention)
- **Control Flow** (readability)
- **Exception Handling** (reliability)
- **Async Patterns** (safety)
- **Collections** (performance and correctness)
- **Security** (protection against known vectors)
- **API Design** (usability and encapsulation)
- **Testing** (regression prevention)
- **And 6 more...**

Best practices are *advisory by default* and complement the deterministic GCI rules, which focus on behavioral safety and correctness.
