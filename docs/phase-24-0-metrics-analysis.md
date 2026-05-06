# Phase 24.0: Production Metrics Analysis & Decision Gate

**Timeline:** Week 1 post-Phase 23 deployment (7-14 days)  
**Goal:** Validate Phase 23 FP reduction targets before proceeding to Phase 24.1  
**Decision Gate:** Week 1 checkpoint - GO/NO-GO to Phase 24.1

---

## Overview

Phase 24.0 is a passive metrics collection and analysis phase. No code changes occur. Instead:

1. Deploy Phase 23 to production
2. Collect production metrics for 1-2 weeks
3. Analyze Phase 23 impact vs targets
4. Make GO/NO-GO decision at Gate 1

**Success Criteria:**
- Phase 23 FP reduction: 17-28% (vs Phase 21 baseline)
- Cumulative reduction stable: 39-60%
- Coordination activation frequency: Within expected ranges
- False negative rate: <5% increase

---

## Metrics to Collect

### 1. False Positive Counts (Daily)

Track false positive findings by category:

```
Date          P4_FP  P5_FP  P6_FP  Total_Phase23_FP  Cumulative_FP  Reduction_%
2026-05-06    12     18     22     52                150            45.3%
2026-05-07    11     17     23     51                148            44.8%
2026-05-08    13     19     21     53                152            46.2%
...
7-day avg:    12.3   18.1   22.0   52.4              150.2          45.5%
```

**Calculations:**
- Baseline (Phase 21): 100 FP per day (typical corpus)
- Target (Phase 23): 100 × (1 - 0.225) = 77.5 FP per day (22.5% reduction)
- Expected range: 72-83 FP per day (17-28% reduction)

**Data Collection:**
```bash
# Extract from logs/metrics.csv (if available)
grep "false_positive_count" logs/metrics.log | \
  grep "2026-05-" | \
  awk '{print $1, $3}' > phase23_fp_by_day.csv

# Manual tracking if logs unavailable
# Use dashboard or direct API query
```

### 2. Coordination Activation Frequency (Daily)

Track how often each coordination fires:

```
Date          P4_Activations  P5_Activations  P6_Activations  Notes
2026-05-06    22              31              38              Normal operation
2026-05-07    19              35              40              P5 spike
2026-05-08    25              28              36              Stable
...
7-day avg:    22.0            31.5            37.8            Within range
```

**Expected Ranges (per 1,000 findings):**
- P4 (Performance): 15-30 activations (we expect ~22)
- P5 (Serialization): 20-40 activations (we expect ~31)
- P6 (DI & Async): 25-50 activations (we expect ~38)

**Data Collection:**
```bash
# Count coordination tags in logs
for day in {6..12}; do
  grep "coordination:P4-performance" logs/labeling-2026-05-${day}.log | wc -l
  grep "coordination:P5-serialization" logs/labeling-2026-05-${day}.log | wc -l
  grep "coordination:P6-di-async" logs/labeling-2026-05-${day}.log | wc -l
done
```

### 3. Confidence Score Distribution

Analyze confidence scores before/after coordination:

```
Rule      Before_Avg  After_Avg  Boost_Delta  Distribution
GCI0044   0.60        0.75       +0.15        [0.70-0.80: 85%, >0.80: 15%]
GCI0035   0.55        0.70       +0.15        [0.65-0.75: 80%, >0.75: 20%]
GCI0039   0.55        0.85       +0.30        [0.80-0.90: 95%, >0.90: 5%]
GCI0048   0.60        0.80       +0.20        [0.75-0.85: 92%, >0.85: 8%]
GCI0045   0.55        0.75       +0.20        [0.70-0.80: 88%, >0.80: 12%]
GCI0016   0.65        0.80       +0.15        [0.75-0.85: 90%, >0.85: 10%]
```

**Analysis Goals:**
- Verify boost delta matches documented values (±5%)
- Check for unexpected clustering (indicates potential issues)
- Ensure no confidence > 0.95 (risk of over-confidence)

**Data Collection:**
```bash
# Extract confidence scores from labeling output
grep "ExpectedConfidence" logs/labeling.log | \
  awk '{print $2, $3}' > confidence_distribution.csv
```

### 4. False Negative Rate

Monitor for missed findings (false negatives):

```
Rule      Phase21_FN_Rate  Phase23_FN_Rate  Delta    Status
GCI0044   2.1%             2.3%             +0.2%    ✓ OK
GCI0035   1.8%             1.9%             +0.1%    ✓ OK
GCI0039   1.5%             2.2%             +0.7%    ⚠ Monitor
GCI0048   1.3%             1.8%             +0.5%    ⚠ Watch
GCI0045   2.4%             2.6%             +0.2%    ✓ OK
GCI0016   1.1%             1.4%             +0.3%    ✓ OK
```

**Target:** All deltas < 0.5% (increase by no more than 0.5 percentage points)  
**Alert:** If any delta > 1%, investigate coordination thresholds

**Data Collection:**
```bash
# Compare finding counts against known-good corpus
# If known truth available: measure recall = TP / (TP + FN)
# Calculate FN rate = FN / (TP + FN)
```

---

## Daily Metrics Report Template

Create daily report template (CSV):

```
date,p4_fp,p5_fp,p6_fp,total_phase23_fp,baseline_fp,reduction_pct,p4_activations,p5_activations,p6_activations,p4_avg_conf,p5_avg_conf,p6_avg_conf,notes
```

**Example Day 1:**
```
2026-05-06,12,18,22,52,100,48%,22,31,38,0.75,0.85,0.80,Initial deployment - all services stable
```

---

## Weekly Analysis Checkpoints

### End of Day 3 (Mid-Week) - Preliminary Check

**Question:** Are we on track?

```
Metric                    Target              Actual              Status
FP Reduction (3-day avg)  17-28% from BL      16-24% (example)    ✓ Tracking
P4 Activation Freq        15-30 per 1k        18-25 (example)     ✓ OK
P5 Activation Freq        20-40 per 1k        28-35 (example)     ✓ OK
P6 Activation Freq        25-50 per 1k        35-42 (example)     ✓ OK
Confidence Distribution   Clustered 0.75+     85-90% range (ok)   ✓ OK
FN Rate Change            <0.5% increase      +0.2-0.3% (ok)      ✓ OK
```

**Actions:**
- If ✓ all metrics: Continue monitoring
- If ⚠ any metric: Investigate cause (logs, sample findings)
- If ✗ critical metric: Prepare rollback decision

### End of Week 1 (Day 7) - Decision Gate 1

**Question:** Do Phase 23 metrics validate targets? GO or NO-GO?

**GO Criteria (proceed to Phase 24.1):**
- [ ] FP reduction: 17-28% confirmed
- [ ] Cumulative reduction: 39-60% confirmed
- [ ] Coordination activation frequency within ranges
- [ ] Confidence distribution reasonable (85%+ in target range)
- [ ] FN rate increase < 0.5%
- [ ] No critical errors in production

**NO-GO Criteria (tune and re-evaluate):**
- [ ] FP reduction < 12% (significantly below target)
- [ ] Any coordination activation frequency > 50% outside range
- [ ] FN rate increase > 1%
- [ ] Service stability issues detected
- [ ] Unexpected pattern in confidence distribution

**Gate 1 Decision Report Template:**

```
=== PHASE 24.0 DECISION GATE 1 ===
Date: 2026-05-13 (Day 7 post-deployment)

Metrics Summary:
├─ FP Reduction: 21.3% (Target: 17-28%) ✅ GO
├─ Cumulative: 45.8% (Target: 39-60%) ✅ GO
├─ P4 Activation: 22/1000 (Target: 15-30) ✅ GO
├─ P5 Activation: 31/1000 (Target: 20-40) ✅ GO
├─ P6 Activation: 38/1000 (Target: 25-50) ✅ GO
├─ Confidence Distribution: 88% in range ✅ GO
├─ FN Rate Change: +0.3% (Target: <0.5%) ✅ GO
└─ Production Stability: 99.8% uptime ✅ GO

Decision: ✅ GO TO PHASE 24.1

Next Phase:
- Start P7 (Concurrency & Lock Ordering) implementation
- Estimated timeline: 3-4 days
- Prerequisites met: GCI0038 baseline validation required

Recommendation:
Proceed with Phase 24.1 as planned. Phase 23 metrics validate targets.
No tuning needed at this time.
```

---

## If NO-GO: Tuning Procedure

If Gate 1 decision is NO-GO, follow tuning procedure:

### Step 1: Identify Root Cause

**Low FP Reduction (<12%):**
- Check coordination activation frequency - are they firing at all?
- Verify coordination code was deployed (check logs for "coordination:" tags)
- Review confidence boost values - were they set correctly?
- Sample 10 findings - manually verify boost is applied

**High FN Rate (>1%):**
- Review coordination thresholds - too aggressive?
- Check if any legitimate findings were suppressed
- Sample 10 false negatives - analyze why missed
- Consider lowering boost thresholds

**Out-of-range Activation Frequency:**
- If too low: Coordination not detecting pattern, check scope filtering
- If too high: Over-detecting, check precision (are they TP?)

### Step 2: Tune Thresholds

Example tuning for low FP reduction:

```
Current:
- P4: GCI0044 0.60→0.75, GCI0035 0.55→0.70 (not working)
- Action: Increase boost → GCI0044 0.60→0.78, GCI0035 0.55→0.72

Or:

Current:
- P5: GCI0039 0.55→0.85 (too aggressive, high FN)
- Action: Decrease boost → GCI0039 0.55→0.75

Process:
1. Adjust ONE coordination at a time
2. Deploy to staging environment
3. Re-run metrics collection (3-5 days)
4. Analyze impact
5. If improved: Deploy to production; if not: revert and try different adjustment
```

### Step 3: Re-evaluate Decision

After tuning:
- Collect metrics for 3-5 more days
- Reassess Gate 1 criteria
- Document tuning decisions in ADR-0005 appendix
- Make final GO/NO-GO decision

---

## Phase 24.1 Prerequisites (If GO)

Before starting Phase 24.1 (P7 Concurrency), verify:

- [ ] Phase 23 metrics validated (Gate 1 passed)
- [ ] GCI0038 (lock ordering) exists and has reasonable baseline confidence
- [ ] GCI0016 (async violations) metrics stable post-Phase 23
- [ ] Production environment stable (no cascading issues)

**GCI0038 Baseline Validation:**
```bash
# Query: How often does GCI0038 fire in production?
# Expected: 5-15 per 1,000 findings (moderate frequency)
# If <2 per 1,000: GCI0038 rarely fires, P7 may be low impact
# If >30 per 1,000: GCI0038 very noisy, needs pre-tuning

grep "GCI0038" logs/labeling.log | wc -l  # count detections
# Divide by total findings to get frequency
```

If GCI0038 baseline is weak (<2 per 1,000), recommend:
- Defer P7 to Phase 25
- Proceed with P8 (Cache) only
- Adjust Phase 24.1 scope

---

## Success Metrics Dashboard (Example)

Create dashboard or spreadsheet with:

```
┌─ Phase 24.0 Metrics Dashboard ─────────────────┐
│                                                 │
│ FP Reduction:  ████████░ 45.8% (Target: 39-60%)│
│ P4 Activity:   ███░░░░░░ 22/1000 (Target: 15-30)
│ P5 Activity:   ████░░░░░ 31/1000 (Target: 20-40)
│ P6 Activity:   █████░░░░ 38/1000 (Target: 25-50)
│ Stability:     █████████ 99.8% uptime          │
│ FN Rate:       ░░░░░░░░░ +0.3% (Target: <0.5%) │
│                                                 │
│ Gate 1 Status: ✅ GO TO PHASE 24.1              │
│                                                 │
└─────────────────────────────────────────────────┘
```

---

## Rollback Decision (Emergency)

If critical issues detected:

**Criteria for immediate rollback:**
- Production outage caused by coordination
- False positive rate > 70% (severe quality issue)
- Service latency increase > 20%
- Cascade failure detected

**Rollback procedure:**
```bash
# 1. Revert to v2.6.0 (Phase 21 last stable)
git checkout v2.6.0
dotnet build -c Debug
# 2. Deploy to production
# 3. Verify services recover
# 4. Document incident
# 5. Post-mortem analysis
```

**Post-rollback:** Analyze root cause and decide on Phase 23 re-tuning vs re-architecture.

---

## References

- **Deployment Checklist:** `DEPLOYMENT_CHECKLIST_v2.7.0.md`
- **Release Notes:** `RELEASE_NOTES_v2.7.0.md`
- **Runbook:** `docs/operations/coordination-runbook.md`
- **ADR-0005:** `docs/architecture/adr-0005-phase-23-heuristics-and-coordinations.md`

---

**Phase 24.0 Owner:** [Your Team]  
**Decision Gate 1 Date:** [7 days post-deployment]  
**Go-Live Target (if GO):** [Date + 2-3 weeks for Phase 24.1-24.2]

