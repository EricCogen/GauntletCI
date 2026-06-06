# Curated benchmark fixtures

In-repo regression diffs for `GauntletCI.Benchmarks`. These are **not** the Silver corpus (618 OSS PRs in `~/.gauntletci/corpus.db`); they are small, checked-in diffs with explicit expected outcomes for CI.

## Coverage (6 manifest groups, 21 diffs, 22 CI tests)

| Directory | Rules | CI assertion | Notes |
|-----------|-------|--------------|-------|
| `gci0003/` | GCI0003 | Observation (`edge-case`) | Real PR diff |
| `gci0016/` | GCI0016 | 3 of 4 | Sync-over-async / blocking patterns |
| `gci0017/` | GCI0003, GCI0004, GCI0016 | 1 | Multi-rule real PR |
| `p21-p0-coordination/` | GCI0016, GCI0039, GCI0044 | 3 | Phase 21 P0 synthetic coordinations |
| `p21-p1-coordination/` | GCI0003, GCI0016, GCI0032 | 5 of 6 | Exception/async coordinations |
| `p21-p2-coordination/` | GCI0015, GCI0020, GCI0024, GCI0038 | 5 of 6 | Resource/cast coordinations |

**17 diffs** assert `expected_outcome: fire`. **4 diffs** are observation-only (`edge-case`) where the engine output is not enforced yet.

Run: `dotnet test tests/GauntletCI.Benchmarks --filter Category=Benchmark`

Cross-repo competitive benchmarks (ground truth, CodeQL/Sonar harvests) live under `eval/` and are documented in `eval/README.md`.

## Adding fixtures

Each subdirectory contains one `manifest.json` and one or more `.diff` files. Do **not** commit manifests with `"fixtures": []` — empty shells imply coverage that CI does not enforce. `CuratedFixtureCoverageTests` fails if any manifest has zero entries.
