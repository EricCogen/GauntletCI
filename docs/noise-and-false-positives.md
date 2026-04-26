# Noise and False Positives

GauntletCI is designed to produce a small number of high-signal findings.

The goal is not to flag every possible issue. The goal is to identify behavior-changing diffs that deserve validation before commit or merge.

## Tuning the sensitivity threshold

The `--sensitivity` flag is the primary noise-control knob. It filters findings by both severity and confidence using a priority grid:

| | High confidence | Medium confidence | Low confidence |
|---|---|---|---|
| **Block** | strict, balanced, permissive | strict, balanced, permissive | balanced, permissive |
| **Warn** | balanced, permissive | balanced, permissive | permissive only |
| **Info** | verbose only | verbose only | verbose only |

- `strict` - Only Block-severity findings with High or Medium confidence. Minimizes noise; best for CI gates on pull requests.
- `balanced` - All Block findings plus Warn findings with Medium or High confidence. Default; suitable for most pre-commit hooks.
- `permissive` - All Block and Warn findings regardless of confidence. Closest to the raw rule output; use when tuning a new integration.

```bash
# CI gate: only the most critical issues
gauntletci analyze --staged --sensitivity strict

# Default behavior
gauntletci analyze --staged

# See everything for diagnostic or tuning purposes
gauntletci analyze --staged --sensitivity permissive
```

When findings are hidden by the active threshold, the output shows a count: `(3 hidden at balanced sensitivity)`. Use `--sensitivity permissive` to see them.

When 4 or more distinct rules fire in the same diff, the output prepends a compound-risk note. This is a display aid, not a separate finding, and does not affect the exit code on its own.

## What a finding means

A finding means:

- The diff contains a structural risk signal.
- The changed behavior may deserve review.
- Additional validation may be needed.

A finding does not mean:

- The code is definitely broken.
- The change must be rejected.
- The developer made a mistake.

## Why advisory mode is recommended first

For new repositories, start with advisory mode:

```yaml
fail-on-findings: "false"
```

Review the findings, tune suppressions, and only enable blocking once the signal quality is acceptable for the team.

## Reporting noisy findings

Use the [false positive issue template](https://github.com/EricCogen/GauntletCI/issues/new?template=false_positive.yml) and include:

- Rule ID
- GauntletCI version
- Finding output
- Minimal sanitized diff
- Why the finding was not useful
