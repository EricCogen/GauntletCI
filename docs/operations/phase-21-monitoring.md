# Phase 21 Production Monitoring Runbook

**Purpose:** Operational guide for monitoring Phase 21 coordinations in production  
**Versions Affected:** v2.4.0 (P0), v2.5.0 (P1), v2.6.0 (P2)  
**Audience:** DevOps, SRE, Platform Engineers  
**Update Frequency:** Post-production metrics available (1-2 week intervals)

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [What to Monitor](#what-to-monitor)
3. [Alert Thresholds](#alert-thresholds)
4. [Data Collection](#data-collection)
5. [Dashboards](#dashboards)
6. [Incidents](#incidents)
7. [Metrics Baseline](#metrics-baseline)

---

## Quick Start

**Phase 21 deployment: v2.6.0 (Latest)**

Three coordinations are now active in production:
- **P0 (v2.4.0)** — Async violations coordination (GCI0016 + GCI0039 + GCI0044)
- **P1 (v2.5.0)** — Exception handling coordination (GCI0032 + GCI0003 + GCI0016)
- **P2 (v2.6.0)** — Resource management coordination (GCI0024 + GCI0015)

**What changed:** Confidence scores on certain findings are now higher when both rules fire together.

**Expected behavior:** 20-30% reduction in false positives across all findings.

**Watch period:** 24-48 hours (real-time), then 1-2 weeks (metric validation).

---

## What to Monitor

### 1. Coordination Activation Frequency

**Metric Name:** `phase21_coordination_activations_per_day`

**What it measures:** How often coordination logic runs (both rules fire on same finding).

**Collection method:**
```csharp
// In SilverLabelEngine.cs, log when boosts are applied
if (gci0024?.Confidence >= 0.50 && gci0015?.Confidence >= 0.50)
{
    logger.LogInformation("P2-Coordination: Both GCI0024 and GCI0015 fired, applying boosts");
    // Boost logic...
}
```

**Expected baselines:**
- **P0:** 5-15 activations/day (async violations common)
- **P1:** 3-8 activations/day (exception patterns less common)
- **P2:** 2-5 activations/day (resource leak + data corruption = rare together)

**Alert if:** Any coordination activates > 50 times in a 24-hour period (suggests over-triggering)

---

### 2. False Positive Rate (FP %)

**Metric Name:** `gauntletci_false_positive_rate`

**What it measures:** % of findings that are not real risks (calibrated via customer feedback).

**Calculation:**
```
FP% = (False Positives / Total Findings) * 100

FP = findings that users marked as "not a risk" or "fixed in a way that doesn't match the warning"
```

**Baseline (pre-Phase 21):** 40-50%  
**Target (post-Phase 21):** 20-30%  
**Success:** Achieve 20-30% within 1-2 weeks of v2.6.0 deployment

**Collection method:**
- Survey users: "Was this finding accurate?" (yes/no)
- Track via GitHub issue feedback when users close as "not applicable"
- Aggregate daily in monitoring dashboard

**Alert if:** 
- FP% climbs above 35% (suggests coordination is under-triggering)
- FP% climbs above 50% (suggests regression; consider rollback)

---

### 3. Regression Detection

**Metric Name:** `phase21_regression_count`

**What it measures:** Real bugs that GauntletCI now misses (false negatives).

**Expected:** 0 regressions (coordination only raises confidence, never lowers)

**Collection method:**
- Track findings that users report as "GauntletCI missed this" in issues/feedback
- Compare 1-week pre/post v2.6.0 metrics
- Regression = a real risk that should have fired but didn't (confidence boosted so high it exceeded threshold in wrong scenario)

**Alert if:** > 3 regressions reported in first week

---

### 4. Coordination Confidence Boosts

**Metric Name:** `phase21_confidence_boost_delta`

**What it measures:** How much confidence actually increases when coordination activates.

**Tracked per coordination:**
- P0: GCI0039 boost (target: 0.65 → 0.80, delta = +0.15)
- P1a: GCI0032 + GCI0003 boosts (target: +0.23, +0.18)
- P1b: GCI0032 + GCI0016 boosts (target: +0.18, +0.28)
- P2: GCI0024 + GCI0015 boosts (target: +0.15, +0.15)

**Alert if:**
- Delta is < +0.10 (boosting isn't helping much)
- Delta is > +0.30 (boosting too aggressively, may cause false negatives)

---

### 5. Build & Test Health

**Metric Name:** `gauntletci_tests_passing`, `gauntletci_build_status`

**What it measures:** No regressions in core functionality from coordination code.

**Expected:** 
- 1,500/1,500 tests passing (100%)
- 0 build errors, 0 warnings
- All 21 coordination test fixtures passing

**Collection method:**
- CI/CD pipeline reports daily
- Alert if build fails, any test regresses

**Alert if:**
- Test pass rate drops below 99% (suggests coordination code has side effects)
- Build fails with errors (coordination code has syntax/runtime issues)

---

## Alert Thresholds

| Alert | Threshold | Severity | Action |
|-------|-----------|----------|--------|
| **FP% regression** | > 50% | 🔴 CRITICAL | Initiate rollback to v2.5.0 |
| **FP% above target** | > 35% | 🟡 WARNING | Investigate coordination patterns |
| **Over-triggering** | > 50 activations/day | 🟡 WARNING | Check confidence threshold gate |
| **Regressions** | > 3 real bugs missed | 🔴 CRITICAL | Investigate and rollback if needed |
| **Test failure** | < 99% pass rate | 🔴 CRITICAL | Block deployment until fixed |
| **Build failure** | Any error | 🔴 CRITICAL | Revert coordination commit |

---

## Data Collection

### Logs to Enable

In `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs`, ensure these are logged:

```csharp
// At each coordination invocation
logger.LogInformation("Applying {Coordination} to {FindingCount} findings", 
    coordinationName, findings.Count());

// When boost is applied
logger.LogInformation("Coordination boost: {RuleId} {OldConfidence} -> {NewConfidence}",
    ruleId, oldConfidence, newConfidence);

// When both rules fire
logger.LogInformation("Both {Rule1} and {Rule2} fired on same finding",
    ruleId1, ruleId2);
```

### Metrics Endpoints

Expose via Prometheus or similar:
- `gauntletci_coordination_activations{phase="p0|p1|p2"}`
- `gauntletci_confidence_boost_applied{rule_id}`
- `gauntletci_false_positive_rate` (updated daily)
- `gauntletci_regression_count` (updated daily)

### Dashboard

Create dashboard with:
- **Top left:** FP rate trend (target 20-30%)
- **Top right:** Regression count (target 0)
- **Bottom left:** Coordination activations per phase (P0/P1/P2)
- **Bottom right:** Confidence boost distributions

---

## Incidents

### Scenario 1: FP Rate Climbs Above 50%

**Action:**
1. Check coordination activation logs — is any coordination over-triggering?
2. If P0 > 50 activations/day: confidence threshold too low, increase minimum
3. If pattern persists: initiate rollback to v2.5.0
4. Post-mortem: what coordination pattern was wrong?

**Rollback:**
```bash
git revert dc75d38  # Phase 21.2 P2 commit
git push origin main
gh release delete v2.6.0
# Deploy v2.5.0 (P0+P1 still active, 14-22% FP reduction maintained)
```

**Rollback time:** ~2 minutes  
**Impact:** Phase 21.2 P2 disabled; P0 and P1 continue (still 14-22% improvement)

---

### Scenario 2: Real Bugs Being Missed (Regressions > 3)

**Action:**
1. Analyze each missed bug: why did coordination not catch it?
2. Check if false negative is in coordination logic or underlying rule
3. If coordination boosted confidence too high: lower threshold
4. If underlying rule had low confidence: improve rule heuristics (Phase 22 task)

**Example problem:** P2 coordination boosts GCI0024 from 0.65 → 0.80, but threshold for reporting is now 0.85 → finding dropped

**Fix:**
- Adjust boost ceiling: 0.65 → 0.78 (not quite to 0.80)
- OR lower reporting threshold for resource leaks
- Requires redeployment (minor version)

---

### Scenario 3: Coordination Not Triggering Enough (< 2 activations/day on P2)

**Action:**
1. Check if both rules are firing on same findings
2. If both fire but coordination doesn't activate: confidence threshold too high (need both ≥ 0.50, not possible)
3. Investigate: are GCI0024 and GCI0015 firing on different code paths?

**Fix:**
- Expand scope detection heuristics (method-level → cross-method)
- Requires code change + retest

---

## Metrics Baseline

### Baseline (Pre-Phase 21)

Captured from production pre-v2.4.0 release:

| Metric | Value |
|--------|-------|
| FP rate | 40-50% |
| GCI0016 alone | 35-40% FP |
| GCI0039 alone | 30-45% FP |
| GCI0032 alone | 25-35% FP |
| Total findings/day | ~500 |
| Regressions | ~2 per quarter |

### Target (Post-Phase 21.2)

Expected 1-2 weeks after v2.6.0 deployment:

| Metric | Target |
|--------|--------|
| FP rate | 20-30% |
| GCI0016 boosted | 15-20% FP |
| GCI0039 boosted | 10-20% FP |
| GCI0032 boosted | 12-18% FP |
| Regressions | 0 (monitored) |
| Findings/day | ~450-500 (lower FP, same signals) |

---

## Success Criteria

✅ **After 24-48 hours:**
- No critical alerts
- Build passing
- Tests passing
- No unexpected log errors

✅ **After 1 week:**
- FP rate trending toward 30-35% (on track for 20-30%)
- 0 regressions
- Coordination activations within expected ranges (P0: 5-15/day, P1: 3-8/day, P2: 2-5/day)

✅ **After 2 weeks:**
- FP rate stabilized at 20-30%
- 0 regressions
- Production stable, ready for Phase 21.3 P3 planning

---

## Rollback Procedure

If production issues require immediate rollback:

```bash
# 1. Revert main branch to v2.5.0
cd GauntletCI
git revert dc75d38  # Phase 21.2 P2 commit
git push origin main

# 2. Delete problematic release
gh release delete v2.6.0 --repo EricCogen/GauntletCI

# 3. Verify v2.5.0 is latest
gh release list --repo EricCogen/GauntletCI | head -3

# 4. Deploy v2.5.0 (P0+P1 remain, 14-22% FP reduction still active)
```

**Expected time to rollback:** < 2 minutes  
**Fallback position:** v2.5.0 with P0 and P1 active (14-22% improvement maintained)

---

## Contact & Escalation

- **Phase 21 Owner:** [Eric Cogen]
- **Coordination Questions:** See `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Tuning Issues:** See `docs/troubleshooting/phase-21-tuning.md`
- **Critical Alert:** Initiate rollback → post-mortem within 1 hour

---

## See Also

- **Architecture Decision:** `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Troubleshooting Guide:** `docs/troubleshooting/phase-21-tuning.md`
- **Release Notes:** `docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md`
- **Implementation:** `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs`
