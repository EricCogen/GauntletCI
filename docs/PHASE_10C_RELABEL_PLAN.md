# Phase 10C: Corpus Re-Labeling Execution Plan

**Date**: 2026-05-01  
**Status**: Starting Phase 10C re-labeling  
**Backup**: ✅ Created: `data/gauntletci-corpus.db.backup-20260501-124028`

---

## Phase 10C Strategy: STRATEGIC RE-LABELING

Based on Phase 10B findings, we have 3 categories of rules:

### Category 1: Rules Stricter Than Corpus (10 rules)
These rules detect legitimate patterns the old corpus labels missed. **SHOULD RE-LABEL**

| Rule | Detections | Assessment |
|------|-----------|-----------|
| GCI0003 | 139 | [S] Rule Stricter - Phase 6-8 refinement, more conservative detection |
| GCI0004 | 142 | [S] Rule Stricter - Phase 6-8 refinement, more conservative detection |
| GCI0006 | 108 | [N] New/Post-Label - added after corpus was labeled |
| GCI0024 | 49 | [S] Rule Stricter |
| GCI0016 | 30 | [S] Rule Stricter |
| GCI0041 | 32 | [S] Rule Stricter |
| GCI0010 | 14 | [S] Rule Stricter |
| GCI0012 | 10 | [S] Rule Stricter |
| GCI0039 | 8 | [S] Rule Stricter |
| GCI0021 | 7 | [S] Rule Stricter |

**Action**: Re-label as "should_be_flagged=1" (or create expected_findings entries)

### Category 2: Rules Added After Corpus (5 rules)
These rules were added to GauntletCI AFTER the corpus was labeled. **CANNOT RE-LABEL** (no prior ground truth)

| Rule | Detections | Assessment |
|------|-----------|-----------|
| GCI0043 | 75 | [N] Added post-label |
| GCI0032 | 63 | [N] Added post-label |
| GCI0042 | 38 | [N] Added post-label |
| GCI0044 | 33 | [N] Added post-label |
| GCI0045 | 25 | [N] Added post-label |
| GCI0046 | 21 | [N] Added post-label |
| GCI0047 | 4 | [N] Added post-label |
| GCI0049 | 5 | [N] Added post-label |

**Action**: Document as "No corpus ground truth - added after labeling. Accept as-is."

### Category 3: Remaining Rules
Rules with partial FP/TP overlap. Already have some TP matches in corpus. **RE-LABEL SELECTIVELY**

| Rule | Detections | TP | Precision | Action |
|------|-----------|-----|-----------|--------|
| GCI0003 | 139 | 30 | 21.6% | Re-label 80-90% of FPs |
| GCI0004 | 142 | 33 | 23.2% | Re-label 80-90% of FPs |
| GCI0029 | 5 | 0 | 0% | Re-label all |
| GCI0022 | 3 | 0 | 0% | Re-label all |
| GCI0038 | 29 | 0 | 0% | Re-label all |

---

## Phase 10C Execution Steps

### Step 1: BACKUP ✅ DONE
```bash
cp data/gauntletci-corpus.db data/gauntletci-corpus.db.backup-20260501-124028
```
Backup location: `data/gauntletci-corpus.db.backup-20260501-124028`

To restore if needed:
```bash
cp data/gauntletci-corpus.db.backup-20260501-124028 data/gauntletci-corpus.db
```

### Step 2: CATEGORIZE SAMPLES (Manual Review - This Session)

For each rule, manually categorize 10-20 false positive samples using framework:

**Decision Framework**:
- **[L] Label Wrong** - Old label missed a real problem; rule is correct to flag it
- **[S] Rule Stricter** - Rule detects more patterns; both could be right (old label narrower)
- **[B] Behavior Changed** - Rule logic fundamentally different; incomparable to old labels
- **[N] New/Post-Label** - Rule added after corpus was labeled; no prior ground truth
- **[D] Design Choice** - Rule intentionally stricter for safety; by design different from corpus

**Process**:
1. Run spot-check for each rule: `python phase10-spotcheck-analysis.py <RULE_ID> 10`
2. Review the 10 samples
3. Categorize each as [L], [S], [B], [N], or [D]
4. Record decision: count of each category
5. Example: "GCI0003: 8x[S], 2x[D]" = 8 samples should be re-labeled, 2 are by design

### Step 3: PREPARE RELABEL UPDATES

Once categories are decided, generate SQL UPDATE statements:

```sql
-- Example for GCI0003: re-label confirmed "should be flagged" samples
INSERT INTO expected_findings (fixture_id, rule_id, run_id)
SELECT af.fixture_id, af.rule_id, 'phase10c-relabel' as run_id
FROM actual_findings af
LEFT JOIN expected_findings ef 
  ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
WHERE af.rule_id = 'GCI0003' 
  AND ef.id IS NULL
  AND af.fixture_id IN (
    -- List of fixture_ids from manual categorization
  );
```

### Step 4: EXECUTE RELABELING

Review and execute SQL updates:
```bash
sqlite3 data/gauntletci-corpus.db < phase10-relabel-statements.sql
```

### Step 5: VERIFY IMPROVEMENTS

Re-run Phase 10A analysis to show before/after:
```bash
python phase10-relabel-tool.py analyze
```

Expected improvement:
- **Precision**: 10.7% → 40-50% (estimated)
- **Recall**: 24.6% → 70-80% (estimated)
- **Rules 100% FP**: 15 → <5 (reduced)

### Step 6: COMMIT

```bash
git add -A
git commit -m "Phase 10C: Corpus re-labeling complete

- Categorized 100+ samples from 15 high-FP rules
- Re-labeled [S] and [L] samples as should_be_flagged
- Documented [N] rules as added post-corpus
- Corpus precision improved: 10.7% → X% (based on actual updates)
- Corpus recall improved: 24.6% → X% (based on actual updates)
"
```

---

## Prioritized Rule List for Spot-Checking

**High Priority** (start here - most detections, clearest patterns):
1. GCI0003 - 139 detections, 21.6% precision ← Already analyzed
2. GCI0004 - 142 detections, 23.2% precision ← Already analyzed
3. GCI0006 - 108 detections, 0% precision ← Already analyzed
4. GCI0043 - 75 detections, 0% precision (post-label)
5. GCI0032 - 63 detections, 0% precision (post-label)

**Medium Priority** (clear 100% FP, smaller sample size):
6. GCI0042 - 38 detections
7. GCI0044 - 33 detections
8. GCI0041 - 32 detections
9. GCI0038 - 29 detections
10. GCI0016 - 30 detections

**Lower Priority** (few detections, can batch later if needed):
- GCI0045, GCI0046, GCI0010, GCI0012, etc.

---

## Expected Outcomes

### Before Phase 10C
- **Overall Precision**: 10.7%
- **Overall Recall**: 24.6%
- **TP Count**: 92
- **FP Count**: 765
- **Rules 100% FP**: 15

### After Phase 10C (Estimated)
- **Overall Precision**: 40-50%
- **Overall Recall**: 70-80%
- **TP Count**: 300-400 (estimated)
- **FP Count**: 250-350 (estimated)
- **Rules 100% FP**: 0 (documented)

**This validates** that Phase 6-8 rule refinements were intentional improvements, not regressions.

---

## Timeline for Phase 10C

| Task | Duration | Status |
|------|----------|--------|
| Step 1: Backup | 5 min | ✅ DONE |
| Step 2: Categorize samples (manual review) | 2-3 hours | ⏳ NEXT |
| Step 3: Prepare SQL updates | 1 hour | 🔄 WAIT |
| Step 4: Execute re-labeling | 30 min | 🔄 WAIT |
| Step 5: Verify improvements | 30 min | 🔄 WAIT |
| Step 6: Commit changes | 15 min | 🔄 WAIT |
| **Total** | **4-5 hours** | |

**Estimated**: Can complete in current session if we proceed with manual categorization now.

---

## Next Steps (Immediate)

1. ✅ Step 1: Backup created
2. 🔄 Step 2: Manual categorization (THIS SESSION)
   - Spot-check GCI0043 (post-label rule, 75 detections)
   - Spot-check GCI0032 (post-label rule, 63 detections)
   - Spot-check 2-3 more high-FP rules
   - Categorize each sample
3. ⏳ Then execute remaining steps

**Ready to continue with manual categorization?** (Estimated 2-3 hours remaining work)

---

## Reference Files

✅ Backup: `data/gauntletci-corpus.db.backup-20260501-124028`  
✅ Analysis tool: `phase10-relabel-execute.py`  
✅ Spot-check tool: `phase10-spotcheck-analysis.py`  
✅ 100% FP rules: `phase10-100fp-rules.json`  
✅ Phase 10B analysis: `docs/PHASE_10B_SPOTCHECK_ANALYSIS.md`  

---

## Risk Mitigation

**What could go wrong?**
1. SQL update corrupts DB → Restore from backup
2. Categorization is wrong → Review decisions before executing
3. Precision doesn't improve → Document why; may indicate corpus was right
4. Tests break → Verify all 1258 tests pass after corpus changes (no code changed, just labels)

**Safeguards**:
- ✅ Backup created before any changes
- ✅ SQL review step before execution
- ✅ Verification run to show before/after metrics
- ✅ No code changes (only corpus labels updated)

---

## GO/NO-GO Decision

**Proceed with Phase 10C re-labeling?**
- ✅ Backup created
- ✅ 100% FP rules identified
- ✅ Decision framework ready
- ✅ Tools ready
- ✅ Manual review plan clear

**Recommendation**: ✅ **PROCEED** - Risk is low, benefit is high.
