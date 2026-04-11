# Change Risk Thesis

[GauntletCI](../README.md) | [View Research](./change-risk-research.md)

## Summary

GauntletCI is built around a single observation:

> You changed what the code does, but you did not change anything that proves it still works.

---

## The Problem

Software engineering already acknowledges:

- Code changes introduce regression risk
- Tests are required to validate behavior
- Code review is imperfect

Yet in practice:

- Behavioral changes are often made without corresponding validation updates
- Tests continue to pass, creating false confidence
- Edge cases remain unverified

---

## The Gap

Modern tooling answers:

- Does this code compile?
- Do tests pass?
- Is coverage sufficient?

It does not answer:

> Did this specific change alter behavior without validation?

---

## Evidence (Condensed)

Analysis of public pull requests shows:

- Behavioral changes frequently occur without matching test updates
- Validation is often limited to happy-path scenarios
- Risky edge cases are discovered during review or after merge

---

## The Shift

From:

- Code quality
- Test coverage
- Static analysis

To:

> Behavioral accountability at the moment of change

---

## The Core Question

> Did this change introduce behavior that is not properly validated?

---

## Final Thought

The industry understands regression risk.

What is missing is:

> Immediate, diff-level visibility into unvalidated behavioral change.

---

Generated: 04/05/2026

[GauntletCI](../README.md) | [View Research](./change-risk-research.md)
