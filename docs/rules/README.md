# GauntletCI Rule Catalog

GauntletCI rules detect risk introduced by code changes.

A finding is not a claim that the code is definitely broken. A finding is evidence that the diff introduced behavior worth validating.

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

For the full rule reference including all 35 rules, see [docs/rules.md](../rules.md).
