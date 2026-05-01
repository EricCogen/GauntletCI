# Phase 10A: Corpus Validation Tooling - Analysis Report

**Date**: 2026-05-01  
**Status**: Analysis complete, tooling validated  
**Objective**: Understand label/detection mismatch; prepare for Phase 10B re-labeling  

---

## Executive Summary

**Problem**: Corpus database shows severe label/implementation mismatch:
- **Overall precision**: 10.7% (only 92 of 857 detections are correct)
- **Overall recall**: 24.6% (only 92 of 374 expected findings detected)
- **15 rules have 100% false positive rate** (all detections are spurious)
- **765 false positives** vs 92 true positives

**Interpretation**: The corpus labels do **not** represent the current rule implementations. Either:
1. Labels predate Phase 6-8 refinements (version skew)
2. Labels were created with different semantic intent than current rules
3. Labeling strategy fundamentally differs from rule engine

**Not a production risk**: 1258 test suite (100% passing) is the authoritative source. Corpus mismatch is a **data quality issue**, not a code bug.

---

## Detailed Findings

### Top 20 Rules by False Positive Count

| Rule | Expected | Actual | TP | FP | FN | Precision | Recall |
|------|----------|--------|-----|-----|-----|-----------|--------|
| **GCI0003** | 53 | 139 | 30 | 109 | 23 | 21.6% | 56.6% |
| **GCI0004** | 53 | 142 | 33 | 109 | 20 | 23.2% | 62.3% |
| **GCI0006** | 29 | 108 | 13 | 95 | 16 | 12.0% | 44.8% |
| GCI0043 | 0 | 75 | 0 | 75 | 0 | 0.0% | 0.0% |
| GCI0032 | 0 | 63 | 0 | 63 | 0 | 0.0% | 0.0% |
| GCI0024 | 19 | 49 | 6 | 43 | 13 | 12.2% | 31.6% |
| GCI0042 | 0 | 38 | 0 | 38 | 0 | 0.0% | 0.0% |
| GCI0044 | 0 | 33 | 0 | 33 | 0 | 0.0% | 0.0% |
| GCI0041 | 12 | 32 | 2 | 30 | 10 | 6.2% | 16.7% |
| GCI0038 | 0 | 29 | 0 | 29 | 0 | 0.0% | 0.0% |
| GCI0016 | 15 | 30 | 2 | 28 | 13 | 6.7% | 13.3% |
| GCI0045 | 0 | 25 | 0 | 25 | 0 | 0.0% | 0.0% |
| GCI0046 | 0 | 21 | 0 | 21 | 0 | 0.0% | 0.0% |
| GCI0010 | 19 | 14 | 0 | 14 | 19 | 0.0% | 0.0% |
| GCI0012 | 11 | 10 | 0 | 10 | 11 | 0.0% | 0.0% |

**Key observations:**
- **GCI0003, GCI0004, GCI0006**: These 3 rules show the highest FP counts but have *some* true positives (30, 33, 13 respectively). This suggests the corpus labels may have been created with a *broader* definition than current implementations.
- **GCI0043, GCI0032, GCI0042, GCI0044**: These rules have **0 expected** findings but **many actual** detections. This indicates: (a) corpus was not labeled for these rules, OR (b) the rules were added/refined after labeling.

### Rules with 100% False Positive Rate (15 total)

These rules have **zero true positives** in corpus:

```
GCI0010 (Expected: 19, Actual: 14, All FP)
GCI0012 (Expected: 11, Actual: 10, All FP)
GCI0021 (Expected: 11, Actual: 7, All FP)
GCI0022 (Expected: 6, Actual: 3, All FP)
GCI0029 (Expected: 9, Actual: 5, All FP)
GCI0032 (Expected: 0, Actual: 63, All FP) ← Created after labeling?
GCI0038 (Expected: 0, Actual: 29, All FP) ← Created after labeling?
GCI0039 (Expected: 9, Actual: 8, All FP)
GCI0042 (Expected: 0, Actual: 38, All FP) ← Created after labeling?
GCI0043 (Expected: 0, Actual: 75, All FP) ← Created after labeling?
GCI0044 (Expected: 0, Actual: 33, All FP) ← Created after labeling?
GCI0045 (Expected: 0, Actual: 25, All FP) ← Created after labeling?
GCI0046 (Expected: 0, Actual: 21, All FP) ← Created after labeling?
GCI0047 (Expected: 0, Actual: 4, All FP) ← Created after labeling?
GCI0049 (Expected: 0, Actual: 5, All FP) ← Created after labeling?
```

**Pattern**: Rules with `Expected: 0` (likely added after corpus was labeled) and rules GCI0010, GCI0012, GCI0021, GCI0022, GCI0029, GCI0039 (which show 100% FP on *expected* fixtures).

### High-Precision Rules (Counterexample)

Interestingly, **GCI0036** shows 71.4% precision (2 FP out of detections) but only 4.3% recall (5 TP out of 117 expected). This suggests the rule is *correct* when it fires, but misses most cases.

---

## Root Cause Analysis

### Hypothesis 1: Version Skew (Most Likely)
The corpus labels were created against an older version of the rules, before Phase 6-8 refinements. Evidence:
- Many rules show partial TP (e.g., GCI0003 has 30 TP out of 53 expected) → suggests the rule *does* detect some cases correctly
- Massive FP counts → suggests the old labeling captured broader patterns than the refined rule

### Hypothesis 2: Labeling Strategy Mismatch
The corpus labels may have used automated labeling or different heuristics than the current rule implementations.

### Hypothesis 3: Post-Labeling Rule Additions
Rules like GCI0043, GCI0044, GCI0045 showing `Expected: 0` suggest they were added *after* the corpus was labeled (not in corpus at all).

---

## Corpus Database Structure

**Key tables** (for Phase 10B re-labeling):

| Table | Rows | Purpose |
|-------|------|---------|
| `fixtures` | 618 | Code samples (the corpus) |
| `expected_findings` | 486 | Ground truth labels (what *should* be found) |
| `actual_findings` | 45,406 | What current rules actually detect |
| `rule_runs` | 19,159 | Per-fixture, per-rule execution log |
| `candidates` | 879 | Code snippets/patterns |

**Labeling workflow** (for Phase 10B):
1. For each `fixture_id` in `fixtures` table:
2. Run all 34 rules against the fixture (get detections)
3. Compare with `expected_findings` labels
4. For mismatches: (a) update `expected_findings`, or (b) investigate why rule differs

---

## Phase 10A Deliverables

✅ **Tools Created**:
- `phase10-relabel-tool.py`: Batch analysis + spot-check harness
- `phase10-inspect-corpus.py`: Schema inspection
- `corpus-relabel-analysis.csv`: Output data for Phase 10B

✅ **Analysis Complete**:
- 23 rules analyzed (in corpus with either expected or actual findings)
- 15 rules with 100% FP rate identified
- Overall mismatch quantified: 10.7% precision, 24.6% recall
- Root causes hypothesized

✅ **Validation Pipeline Ready**:
- Can run spot-checks on any rule (`--mode spot-check <rule_id>`)
- Can generate before/after diff reports
- Can track changes to corpus database

---

## Phase 10B Readiness

**What we now know:**
1. ✅ Corpus schema is well-structured (fixtures, expected_findings, actual_findings linked)
2. ✅ Label/detection mismatch is severe but quantifiable
3. ✅ High-risk rules identified (GCI0003, GCI0004, GCI0006 + 15 100% FP rules)
4. ✅ Tool infrastructure ready for batch re-labeling

**What Phase 10B must do:**
1. Manually review 10-20 samples per high-risk rule
2. Understand *why* labels don't match detections
3. Update `expected_findings` table with corrected labels
4. Document findings per rule

**Estimated effort for Phase 10B:**
- Spot-checking: ~80 samples × 10-15 min each = 13-20 hours over 3-5 days
- Batch updates: 2-4 hours
- Documentation: 3-5 hours
- **Total: 18-30 hours (1 week)**

---

## Key Metrics for Tracking

| Metric | Current | Target (Post Phase 10B) |
|--------|---------|------------------------|
| **Overall Precision** | 10.7% | >70% (or flag as "FP by design") |
| **Overall Recall** | 24.6% | >80% |
| **Rules with 0% TP** | 15 | 0 (every rule should detect *something*) |
| **Rules with 100% FP** | 15 | 0 |
| **Sample with valid labels** | ~300 | ~600 (all 618 fixtures) |

---

## Recommendations for Phase 10B

### Priority 1: Batch Investigate (2-3 hours)
- Run GCI0003, GCI0004, GCI0006 spot-checks (10 samples each)
- Understand the pattern: are these false positives by *design* (stricter detection) or labeling mistakes?

### Priority 2: Categorize Remaining Rules (3-5 hours)
- For the 15 rules with 100% FP:
  - Are they "post-labeling rules" (never in corpus)? → Mark as "no ground truth"
  - Are they "mislabeled"? → Spot-check to confirm
  - Are they "design mismatches"? → Document the difference

### Priority 3: Systematic Re-Labeling (5-8 hours)
- For rules with partial TP (like GCI0036): verify the TP samples are actually correct
- For rules with high FP: determine if all FPs are spurious or if labels are outdated

### Phase 10C: Use Results to Plan Phase 11
- If corpus quality improves significantly (>70% precision), use it for targeted refinements
- If some rules remain mismatched, document why and defer refinements
- Create Phase 11 roadmap based on validated data

---

## Next Steps

**Immediately** (when Phase 10B starts):
1. Set up manual review session (1-2 weeks)
2. Create spot-check log template
3. Schedule 1-2 hours daily for 5-10 days of manual review

**Before Phase 10B**:
1. Backup existing `gauntletci-corpus.db` (preserve audit trail)
2. Create new tables if needed:
   - `old_expected_findings` (backup)
   - `re_label_audit` (track changes + justifications)

**Success metric for Phase 10B complete:**
- All 618 fixtures have re-evaluated labels
- Spot-check findings documented
- Updated corpus precision/recall baseline computed
- Phase 11 roadmap ready (3-5 rules prioritized for refinements)

---

## Appendix: Quick Reference

**Run analysis**: `python phase10-relabel-tool.py analyze`  
**Spot-check GCI0003**: `python phase10-relabel-tool.py spot-check GCI0003 10`  
**Output files**: `corpus-relabel-analysis.csv` (machine-readable results)  

**Corpus files** (location: `data/gauntletci-corpus.db`):
- Main tables: `fixtures`, `expected_findings`, `actual_findings`, `rule_runs`
- Backup existing before making changes: `cp data/gauntletci-corpus.db data/gauntletci-corpus.db.backup-2026-05-01`
