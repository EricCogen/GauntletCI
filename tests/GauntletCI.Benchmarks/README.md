# Benchmark Corpus

This corpus is intentionally **real-only** (`origin: real`) and designed to measure whether GauntletCI catches subtle production-risk changes that standard review and test checks can miss.
Public-facing examples are selected from repositories with at least one GitHub star.

## Example 1: Mixed-Concern Change Set With Hidden Dependency Risk

- Fixture: `Fixtures/curated/gci0001/09_real_true_positive_rust_yarn_check.diff`
- Rule: `GCI001`
- Source PR: <https://github.com/rust-lang/rust/pull/154641>

The diff combines three distinct concerns in one review unit: a flag correctness fix (`--frozen` -> `--frozen-lockfile`), a new lockfile-integrity enforcement path, and a broad dependency lockfile churn.

**Why this is high-signal:** each change can look valid in isolation, but bundling them raises review complexity and makes regressions easier to miss.

## Example 2: Async Race Condition From Lock Removal

- Fixture: `Fixtures/curated/gci0017/09_real_true_positive_race_condition_state_corruption.diff`
- Rule: `GCI017`
- Source PR: <https://github.com/hsahovic/poke-env/pull/858>

The diff removes per-battle `asyncio.Lock` usage and associated cleanup paths, allowing concurrent handlers to interleave state mutation for the same battle.

**Why this is high-signal:** deletion-only "simplification" introduces race behavior that is easy to miss in review and hard to catch with narrow tests.

## Example 3: Observability Loss With No Functional Breakage

- Fixture: `Fixtures/curated/gci0013/07_real_true_positive_dropped_correlation_id.diff`
- Rule: `GCI013`
- Source PR: <https://github.com/kubernetes/kubernetes/pull/130000>

The error paths still log failures, but correlation identifiers (`requestID`) are removed from key log lines.

**Why this is high-signal:** no compile/runtime failure appears immediately, but incident triage and distributed tracing become materially weaker.

## How fixtures are used

- Live benchmark runs verify fire/do-not-fire expectations against curated diffs.
- Regression checks detect drift in rule precision/recall over time.
- README examples are sourced from these same real fixtures to avoid toy demonstrations.
