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

## Example

```diff
- return order.Total;
+ if (order == null) throw new ArgumentNullException(nameof(order));
+ return order.Total;
