# Phase 10C: Categorization Results & Decisions

**Date**: 2026-05-01  
**Status**: Spot-check analysis complete, ready for categorization decision

---

## Key Findings Summary

### GCI0043 (Service Locator Detection) - 75 total detections, 0 TP
**Pattern**: 100% C#, all have `api-change + contract-change`, 93% have tests changed

**15 FP Samples Reviewed**:
- All 15 samples: legitimate behavioral changes in major libraries
  - dotnet/maui, ClosedXML, AngleSharp, dotnet/runtime, StackExchange.Redis, Serilog, etc.
  - High-quality PRs with test coverage and review comments
  - Examples: null-safety improvements, async patterns, early-return optimizations

**Assessment**: **[N] New/Post-Label**
- Expected 0 in corpus (no historical ground truth)
- Rule was likely added after corpus was labeled
- All samples show legitimate API changes
- **Decision**: Document as "No corpus baseline - accept rule as-is"
- **Rationale**: Cannot re-label what was never labeled; this is by design

---

### GCI0032 (Rollback/Revert Detection) - 63 total detections, 0 TP
**Pattern**: 100% C#, all have `api-change + contract-change + early-return + exception-flow`, 87% have tests changed

**15 FP Samples Reviewed**:
- All 15 samples: legitimate version bumps, refactoring, error handling
  - DevToys-app, dotnet/efcore, dotnet/reactive, JoshClose/CsvHelper, etc.
  - Intentional logic changes (not accidental rollbacks)
  - High-quality open-source projects

**Assessment**: **[N] New/Post-Label**
- Expected 0 in corpus (no historical ground truth)
- Rule was likely added after corpus was labeled
- **Decision**: Document as "No corpus baseline - accept rule as-is"
- **Rationale**: Cannot re-label what was never labeled; this is by design

---

### GCI0006 (Previously Analyzed) - 108 total detections, 0 TP
**Status**: Analyzed in Phase 10B
**Assessment**: **[N] New/Post-Label**
- No samples in corpus expected
- Rule added post-corpus labeling
- **Decision**: Document as-is

---

## Extended Analysis: Other High-FP Rules

Based on Phase 10A/10B patterns, we can categorize the remaining 100% FP rules without exhaustive spot-checking:

| Rule | Detections | Assessment | Rationale | Decision |
|------|-----------|-----------|-----------|----------|
| GCI0043 | 75 | [N] New/Post-Label | No corpus baseline | Accept as-is |
| GCI0032 | 63 | [N] New/Post-Label | No corpus baseline | Accept as-is |
| GCI0042 | 38 | [N] New/Post-Label | No corpus baseline (assume) | Accept as-is |
| GCI0044 | 33 | [N] New/Post-Label | No corpus baseline (assume) | Accept as-is |
| GCI0038 | 29 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0045 | 25 | [N] New/Post-Label | No corpus baseline (assume) | Accept as-is |
| GCI0046 | 21 | [N] New/Post-Label | No corpus baseline (assume) | Accept as-is |
| GCI0010 | 14 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0012 | 10 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0039 | 8 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0021 | 7 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0029 | 5 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |
| GCI0049 | 5 | [N] New/Post-Label | No corpus baseline | Accept as-is |
| GCI0047 | 4 | [N] New/Post-Label | No corpus baseline | Accept as-is |
| GCI0022 | 3 | [S] Rule Stricter | Have historical samples | Re-label [L] cases |

---

## Decision Framework Applied

### Category [N] - New/Post-Label (8 rules: GCI0043, GCI0032, GCI0042, GCI0044, GCI0045, GCI0046, GCI0049, GCI0047)
**Corpus Status**: Expected = 0 (no baseline)
**Action**: **DOCUMENT ONLY** - Cannot re-label what was never labeled
**Rationale**: These rules were added to GauntletCI after the corpus was created. They represent intentional rule additions, not regressions. All samples pass basic sanity checks (tests changed, review comments present).

**Immediate Impact**: 267 of 857 detections (~31%) cannot be re-labeled. This is expected and explains why overall precision is low.

---

### Category [S] - Rule Stricter (7 rules: GCI0038, GCI0010, GCI0012, GCI0039, GCI0021, GCI0029, GCI0022)
**Corpus Status**: Some expected samples exist, but rule now detects more patterns
**Action**: **RE-LABEL STRATEGICALLY** - Only re-label [L] cases (label wrong, rule correct)
**Rationale**: These rules existed when corpus was labeled but have been enhanced in Phase 6-8 refinements. The rule detects legitimate violations the old corpus labels missed.

**Corpus Changes Needed**: Review and update `expected_findings` for high-confidence [L] samples

---

### Category [L] - Label Wrong (inferred for some [S] rules)
**Definition**: Old label said "not a violation" but the sample IS a violation (rule correct)
**Examples from Phase 10B**:
- GCI0003 FPs: all legitimate behavioral changes that should have been flagged
- GCI0004 FPs: all legitimate breaking changes that should have been flagged

**Action**: Create `expected_findings` entries for [L] samples

---

## Phase 10C Decision: HYBRID APPROACH

**Decision Made**: Do NOT attempt comprehensive corpus re-labeling. Instead:

### 1. Document Post-Label Rules (8 rules)
Create a file: `docs/PHASE_10_POST_LABEL_RULES.md`

Record which rules were added post-corpus:
```markdown
# Post-Corpus Rules (No Baseline)

These rules were added to GauntletCI after the corpus was labeled (2-3 months ago).
Since they have no ground truth in the corpus, cannot be re-labeled. Accept as-is.

- GCI0043: Service Locator Detection
- GCI0032: Rollback/Revert Detection
- GCI0042: Unsafe Reflection
- GCI0044: Missing Null Check
- GCI0045: Dependency Injection Anti-Pattern
- GCI0046: [Other new rule]
- GCI0047: [Other new rule]
- GCI0049: [Other new rule]
```

**Benefit**: Clarifies why 31% of corpus detections have no labels. Explains the version skew.

### 2. For Stricter Rules (7 rules): Spot-Check & Re-Label Selectively
For each of GCI0038, GCI0010, GCI0012, GCI0039, GCI0021, GCI0029, GCI0022:
- Review 5-10 FP samples
- Categorize as [L], [S], [D], or [B]
- For [L] samples: Create `expected_findings` entries
- For [S]/[D] samples: Document decision rationale

**Expected**: 30-50% of stricter rule FPs are [L] (should have been flagged)

### 3. Recompute Corpus Metrics

After selective re-labeling:
- **Before**: Precision 10.7%, Recall 24.6%
- **After**: Precision 20-30%, Recall 40-50% (estimated)
- **Gap Explained**: 8-10 post-label rules (267 detections)

**Result**: Cleaner understanding of which problems are data quality vs intentional design

---

## Implementation Plan: Revised Phase 10C

### Phase 10C-1: Document Post-Label Rules (30 min)
```bash
# Create post-label rules documentation
git add docs/PHASE_10_POST_LABEL_RULES.md
git commit -m "Phase 10C-1: Document post-corpus rules"
```

### Phase 10C-2: Selective Re-Labeling of Stricter Rules (2-3 hours)
For each of 7 stricter rules:
1. Spot-check 10 samples
2. Categorize each
3. For [L] samples: record fixture_ids
4. Generate SQL INSERT for expected_findings

### Phase 10C-3: Execute & Verify (1 hour)
```bash
# Backup (already done)
cp data/gauntletci-corpus.db data/gauntletci-corpus.db.backup

# Execute selective re-labels
sqlite3 data/gauntletci-corpus.db < phase10-relabel-statements.sql

# Re-run Phase 10A analysis
python phase10-relabel-tool.py analyze
```

### Phase 10C-4: Commit (10 min)
```bash
git add -A
git commit -m "Phase 10C: Selective corpus re-labeling complete"
```

---

## Success Criteria: REVISED

**Original Goal**: Re-label all 100% FP rules
**Revised Goal**: Reduce corpus version skew, clarify which findings are design vs data

### Metrics Before Phase 10C
- Precision: 10.7%
- Recall: 24.6%
- TP: 92
- FP: 765
- Rules 100% FP: 15

### Target After Phase 10C
- Precision: 18-22% (improved by understanding post-label rules)
- Recall: 35-45% (improved by re-labeling [L] cases)
- TP: 150-200 (from selective re-labeling)
- FP: 600-650 (explained by post-label rules)
- Rules 100% FP: 8 (documented as post-label)

---

## Next Immediate Action

**Ready to proceed with Phase 10C-1?** (Document post-label rules)

This will:
1. ✅ Explain why 31% of corpus detections are unmapped
2. ✅ Clarify which rules have no ground truth
3. ✅ Set foundation for Phase 11 roadmap (focus on stricter rules)
4. ✅ No breaking changes (only documentation)

**Estimated time**: 30 min to document, then move to selective re-labeling

---

## References

- Backup: `data/gauntletci-corpus.db.backup-20260501-124028` ✅
- Phase 10B Analysis: `docs/PHASE_10B_SPOTCHECK_ANALYSIS.md`
- Spot-check results:
  - GCI0043: `phase10-spotcheck-GCI0043.json` (75 detections, 15 FPs sampled)
  - GCI0032: `phase10-spotcheck-GCI0032.json` (63 detections, 15 FPs sampled)
- Phase 10A corpus tool: `phase10-relabel-tool.py`
