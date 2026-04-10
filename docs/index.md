<div style="margin-bottom:50px"><img src="https://raw.githubusercontent.com/EricCogen/GauntletCI/refs/heads/main/GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

**Pre-commit change-risk detection for pull request diffs.**

GauntletCI analyzes what changed in a pull request or from a pre-commit and flags behavior that may no longer be properly validated.

> Did this change introduce behavior that is not properly tested, reviewed, or understood?

---

## Why this exists

Experienced developers still miss things in diffs.

Not because they are careless.
Because diffs are deceptive.

A change can look small and still introduce real risk:

- a null check changes execution flow
- a guard clause introduces a new exception path
- a method signature changes without test updates
- a conditional branch shifts business logic
- a dependency call changes without validation

These often pass:

- tests
- linting
- static analysis
- code review

And they still cause regressions.

GauntletCI exists to catch those changes earlier.

---

## What GauntletCI does

GauntletCI is a .NET CLI tool that analyzes pull request diffs and detects:

- unvalidated behavior changes
- missing or weak test updates
- execution flow changes
- API and contract changes
- risky modifications hidden inside otherwise normal diffs

It focuses on one thing:

> What changed, and what risk did that change introduce?

---

## What GauntletCI is not

GauntletCI is not:

- a linter
- a formatter
- a test runner
- a generic static analysis replacement

It complements those tools by focusing on change-risk in the diff itself.

---

## How it works

1. Parse the pull request diff
2. Identify meaningful change patterns
3. Apply deterministic rules to changed code
4. Correlate findings with test impact where possible
5. Output actionable findings with file paths, line numbers, and reasons

High level flow:

```text
PR Diff -> GauntletCI -> Risk Findings -> Developer Action
```

---

## Why this is different

Most tools answer questions like:

- Does the code compile?
- Do the tests pass?
- Does this violate a style rule?
- Is there a known vulnerability?

GauntletCI answers a different question:

> Did this diff change behavior in a way that deserves more scrutiny?

That is where a lot of costly mistakes hide.

---

## Examples

### Example 1: New exception path

```diff
- return order.Total;
+ if (order == null) throw new ArgumentNullException(nameof(order));
+ return order.Total;
```

**Potential finding:**

- New guard clause introduces exception behavior
- Existing callers may now fail earlier
- No corresponding test update validating the new path

---

### Example 2: Public contract change

```diff
- Task SaveAsync(Order order)
+ Task SaveAsync(Order order, CancellationToken cancellationToken)
```

**Potential finding:**

- Public method signature changed
- Callers and tests may need updates
- Potential contract or integration break

---

### Example 3: Logic branch change

```diff
- if (user.IsActive)
+ if (user.IsActive && user.EmailVerified)
```

**Potential finding:**

- Business rule changed
- Existing behavior narrowed
- Missing test coverage may hide downstream impact

---

### Example 4: Dependency behavior change

```diff
- await _paymentGateway.ChargeAsync(order);
+ await _paymentGateway.AuthorizeAsync(order);
```

**Potential finding:**

- External dependency behavior changed
- Semantics may differ significantly
- Validation and integration coverage should be reviewed

---

## Where it fits

GauntletCI can run:

- as a local pre-commit check
- in a pull request workflow
- in CI pipelines such as GitHub Actions
- as part of a developer review routine before merge

It works alongside:

- unit and integration tests
- code review
- existing static analysis tools

---

## Use cases

### Catch risky changes before commit
Find behavior changes before they even leave your machine.

### Strengthen pull request review
Highlight subtle risks that are easy to overlook in normal review.

### Detect missing test updates
Spot logic changes that deserve new or updated tests.

### Reduce avoidable regressions
Catch the kinds of "how did I miss that?" mistakes that damage confidence and reputation.

---

## Quick start

Install:

```bash
dotnet tool install -g GauntletCI
```

Run:

```bash
gauntletci analyze --diff pr.diff
```

Then review the findings and decide which changes need additional validation, tests, or scrutiny.

---

## Current focus

GauntletCI is actively being developed.

Current focus areas include:

- expanding rule coverage
- improving rule accuracy
- strengthening test correlation
- improving finding clarity and actionability

---

## Source code

GitHub repository:

[https://github.com/EricCogen/GauntletCI](https://github.com/EricCogen/GauntletCI)

---

## Get started

- Read the source on GitHub
- Run GauntletCI locally against a diff
- Add it to your pull request workflow
- Use it as a safety layer before merge

---

## Final note

GauntletCI was built around a very real developer problem:

Even seasoned engineers can miss obvious risk in code changes.

This tool exists to reduce those misses before they become production problems.
