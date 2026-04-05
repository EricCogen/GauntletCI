# Benchmark Corpus

This corpus is intentionally **real-only** (`origin: real`) and designed to measure whether GauntletCI catches subtle production-risk changes that standard review and test checks can miss.

## Example 1: Contract Change Without Signature Change

- Fixture: `Fixtures/curated/gci0017/07_real_true_positive_blocking_plus_missing_error_handling.diff`
- Rule: `GCI017`
- Source PR: <https://github.com/danielaase1337/shoppinglist/pull/36>

The diff removes `await` and introduces `.Result` in request body handling, drops the catch/log error path in one controller, and changes a non-GET fallback response from `NotFound` to `NoContent`.

**Why this is high-signal:** method signatures do not change, but runtime contract and failure semantics do.

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
