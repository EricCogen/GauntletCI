---
description: STALE Copilot-era LLM-only architecture (reference only — not runtime truth)
alwaysApply: false
---

# GauntletCI — Copilot Build Instructions (STALE)

> **Do not implement from this file.** It describes a planned LLM-only, 18-prompt architecture that was **never shipped**.

## Runtime truth (2026-06)

| Topic | Authoritative source |
|-------|---------------------|
| Product architecture | `docs/architecture.md`, `site/` |
| Rule definitions and corpus metrics | `docs/rules.md` |
| Build and validation gates | `.cursor/rules/engineering-rules-consolidated.mdc` |
| Engine implementation | 39+ heuristic `IRule` classes in `GauntletCI.Core`, Roslyn on changed C# files, optional LLM enrichment via `GauntletCI.Llm` |

## What actually ships

- **CLI:** `gauntletci analyze` with deterministic rules, pre-commit hook, corpus tooling
- **Rules:** Reflection-discovered `IRule` implementations (not a single LLM call with 18 prompt rules)
- **Optional LLM:** Explain/enrich findings when configured; default path is local deterministic analysis

## Historical note

The long Copilot-era spec (single LLM evaluation, 18 GCI001–GCI018 prompt rules, telemetry-first design) lives in git history if needed for archaeology. Cursor agents should use the table above instead of reading or extending the retired spec.
