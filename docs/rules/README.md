# GauntletCI Rule Catalog

GauntletCI rules detect risk introduced by code changes.

A finding is not a claim that the code is definitely broken. A finding is evidence that the diff introduced behavior worth validating.

## Rule status

- **Stable**: suitable for normal use
- **Beta**: useful but still being tuned
- **Experimental**: may produce more false positives

## Active rules (37)

GauntletCI ships **37 active deterministic rules** today. The canonical reference with confidence levels, detection logic, and configuration notes is **[docs/rules.md](../rules.md)**.

| Category | Count | Rule IDs |
|----------|------:|----------|
| **Active** (emit findings by default) | 37 | GCI0001, GCI0003–GCI0007, GCI0010, GCI0012, GCI0015–GCI0016, GCI0019, GCI0020–GCI0022, GCI0024, GCI0029, GCI0032, GCI0035–GCI0036, GCI0038–GCI0039, GCI0041–GCI0049, GCI0050–GCI0053, GCI0056–GCI0059 |
| **Implemented, disabled by default** | 2 | GCI0054 (async void — use GCI0016), GCI0055 (regex signatures — use GCI0003) |
| **Reserved / consolidated** | 3 | GCI0028 (unassigned), GCI0030 (→ GCI0024), GCI0033 (→ GCI0016) |

## Deep-dive rule pages

These pages expand on a subset of high-traffic rules. See [docs/rules.md](../rules.md) for the full catalog.

| Rule | Name | Category | Status |
| --- | --- | --- | --- |
| [GCI0003](GCI0003-behavioral-change-detection.md) | Behavioral Change Detection | Behavior and Contracts | Stable |
| [GCI0004](GCI0004-breaking-change-risk.md) | Breaking Change Risk | Behavior and Contracts | Stable |
| [GCI0006](GCI0006-edge-case-handling.md) | Edge Case Handling | Behavior and Contracts | Stable |
| [GCI0007](GCI0007-error-handling-integrity.md) | Error Handling Integrity | Observability and Failure Handling | Stable |
| [GCI0010](GCI0010-hardcoding-and-configuration.md) | Hardcoding and Configuration | Security and Configuration | Stable |

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
