# Noise and False Positives

GauntletCI is designed to produce a small number of high-signal findings.

The goal is not to flag every possible issue. The goal is to identify behavior-changing diffs that deserve validation before commit or merge.

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
