# Phase 12: High-FP Rule Refinement Strategy

**Status**: Planning  
**Objective**: Reduce FP rate for 5 persistent high-FP rules (100% FP rate post-Phase 11)

---

## The 5 Rules Analyzed

| Rule | Name | Detections | Expected | FP Rate | Pattern |
|------|------|------------|----------|---------|---------|
| **GCI0012** | Security Risk | 201 | 11 | 100% | Hardcoded credentials, SQL injection, weak crypto, dangerous APIs |
| **GCI0021** | Data Schema Compatibility | 233 | 11 | 100% | Enum/schema changes that might break compatibility |
| **GCI0022** | Idempotency/Retry Safety | 131 | 6 | 100% | Event handlers, idempotent operations, retry logic |
| **GCI0029** | PII Logging Leak | 340 | 9 | 100% | PII terms in log statements (name, email, ssn, etc.) |
| **GCI0039** | External Service Safety | 617 | 9 | 100% | HTTP calls, external service interactions |

**Total detections**: 1,522 (17.8% of 8,579 corpus detections)

---

## Root Cause Analysis

Unlike post-label rules (which had no prior labels), these 5 rules **do have expected findings** (11, 11, 6, 9, 9). The problem: **no detections matched the expected findings**.

### Possible Causes (in priority order)

1. **Implementation drift**: Rule logic changed after corpus was labeled, but detections now match different patterns
2. **Overly broad detection**: Rule fires on many false positives that aren't reflected in corpus labels
3. **Labeling error**: Corpus expected_findings are incomplete or mislabeled
4. **Legitimate detections**: Rule works correctly; corpus labels are out of date (like post-label rules)

---

## Phase 12 Strategy (Two Tracks)

### Track A: Spot-Check Validation (Parallel to Track B)
Apply Phase 10C-B methodology: spot-check 20-30 samples per rule to determine TP/FP ratio.

**If TP% > 80%**: Assume detections are legitimate → re-label all unmapped fixtures (like Phase 11)
**If TP% < 50%**: Rule likely needs refinement → proceed to Track B
**If 50% ≤ TP% ≤ 80%**: Mixed results → review implementation + corpus labeling

### Track B: Implementation Review
If Track A suggests refinement needed:
1. Review rule implementation (compare to similar high-precision rules)
2. Identify overly broad patterns
3. Add context guards or exclusion filters
4. Update tests to cover guard conditions
5. Re-test corpus

---

## Detailed Plan Per Rule

### GCI0012 - Security Risk (201 detections, 11 expected)

**Current Detection**: Hardcoded credentials (token = "...", password = "...", etc.)

**Sample Analysis Needed**:
- 20-30 random samples from 201 detections
- Check if each is truly a hardcoded credential or false positive
- Look for patterns: config files? Example code? Test fixtures?

**Hypothesis**: Rule is too broad - matches test code, examples, configuration with intended hardcoded values
**Refinement Strategy**: Add guards to skip test files, config files, example code

---

### GCI0021 - Data Schema Compatibility (233 detections, 11 expected)

**Current Detection**: Enum/schema changes (removed enums, renamed fields, etc.)

**Sample Analysis Needed**:
- 20-30 random samples from 233 detections
- Check if each is an actual breaking change or expected evolution
- Look for patterns: major version changes? Deprecated APIs? Intentional refactoring?

**Hypothesis**: Rule fires on benign schema evolution; corpus labels only high-risk changes
**Refinement Strategy**: Add context checks (version bumps? deprecation markers?) to distinguish breaking from benign changes

---

### GCI0022 - Idempotency/Retry Safety (131 detections, 6 expected)

**Current Detection**: Event handlers, idempotent operations, retry patterns

**Sample Analysis Needed**:
- 20-30 random samples from 131 detections
- Check if each has real idempotency/retry safety implications
- Look for patterns: already has safety logic? Documented as intentional? Test code?

**Hypothesis**: Rule detects legitimate patterns; corpus labels are incomplete or use different assessment
**Refinement Strategy**: Likely re-label (similar to Phase 11 post-label rules)

---

### GCI0029 - PII Logging Leak (340 detections, 9 expected)

**Current Detection**: PII terms ('name', 'email', 'ssn', 'token', etc.) in log calls

**Sample Analysis Needed**:
- 20-30 random samples from 340 detections
- Check if each is actual PII exposure or generic term (e.g., "name" in name-agnostic logs)
- Look for patterns: tokenized data? Hashed? Intentionally logged?

**Hypothesis**: Rule too broad - catches generic terms like "name" that aren't actually PII
**Refinement Strategy**: Add guards: skip if data is hashed, tokenized, or in non-sensitive contexts

---

### GCI0039 - External Service Safety (617 detections, 9 expected)

**Current Detection**: HTTP calls, external service interactions without cancellation tokens

**Sample Analysis Needed**:
- 20-30 random samples from 617 detections
- Check if each lacks proper cancellation token handling
- Look for patterns: internal-only calls? Batch operations? Different async patterns?

**Hypothesis**: Rule too strict - many legitimate HTTP calls don't need cancellation tokens
**Refinement Strategy**: Add guards: skip internal calls, allow alternative patterns for cancellation

---

## Execution Plan

### Week 1: Track A (Validation) - 5-8 hours
1. **Day 1**: Spot-check GCI0012 (20-30 samples, ~2 hours)
2. **Day 2**: Spot-check GCI0021, GCI0022 (~2 hours each, parallel)
3. **Day 3**: Spot-check GCI0029, GCI0039 (~2 hours each, parallel)
4. **Day 4**: Analyze results → Decide: re-label or refine?

### Week 2: Track B (Implementation Refinement) - 8-12 hours (if needed)
1. **For each rule requiring refinement**:
   - Review implementation (2 hours)
   - Design guard clauses (1-2 hours)
   - Implement + test (2-3 hours)
   - Update corpus analysis (1 hour)

2. **Regression testing**: Run full corpus analysis (1 hour)

### Week 3: Documentation + Commit - 2 hours
1. Create `docs/PHASE_12_RULE_REFINEMENTS.md`
2. Update CHANGELOG
3. Commit all changes

**Total estimated effort**: 2-3 weeks

---

## Success Criteria

- [x] Phase 11 completed (46.6% precision baseline)
- [ ] All 5 rules analyzed via spot-checking
- [ ] Decision made per rule: re-label vs. refine
- [ ] High-confidence rules (TP% > 80%) re-labeled
- [ ] Rules requiring refinement: guard clauses added + tested
- [ ] Final corpus precision ≥ 50% (target: 55%+)
- [ ] All test count expectations maintained
- [ ] PHASE_12_RULE_REFINEMENTS.md completed
- [ ] Changes committed to main

---

## References

- Phase 11 results: `docs/PHASE_11_GROUND_TRUTH_BASELINE.md`
- Rule implementations: `src/GauntletCI.Core/Rules/Implementations/`
- Corpus tool: `phase10-relabel-tool.py`
- Sample script: `phase12-show-samples.py`
