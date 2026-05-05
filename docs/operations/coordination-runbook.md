# Multi-Phase Coordination Debugging & Tuning Runbook

**Purpose:** Operational guide for debugging false positives, tuning coordination parameters, and validating new coordination patterns  
**Audience:** DevOps, SRE, Engineers adding new coordinations  
**Scope:** Phase 21 (P0-P3) + Phase 23 (P4-P6) + template for future phases  
**Phases Covered:**
- Phase 21 (P0-P3): Async, Exception Handling, Resource Management, Data Security
- Phase 23 (P4-P6): Performance & GC, Serialization Safety, DI & Async

---

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Common Issues & Fixes](#common-issues--fixes)
3. [Tuning Confidence Boosts](#tuning-confidence-boosts)
4. [Testing New Coordinations](#testing-new-coordinations)
5. [Phase 23 Specific Guidance](#phase-23-specific-guidance)
6. [Logging & Monitoring](#logging--monitoring)
7. [Rollback Procedure](#rollback-procedure)

---

## Quick Diagnostics

### Problem: Coordination Not Triggering

**Symptom:** Expected coordination activates < 1 time per day

**Diagnosis:**
```bash
# 1. Check if both rules are firing on same findings
grep -n "GCI0024\|GCI0015" logs/gci-analysis.log | head -50

# 2. Look for evidence both fired on same fixture/method
# Both should appear in same log line or adjacent lines with same fixture context
```

**Root Causes:**
1. **Confidence threshold too high** — Rule firing at 0.45 but coordination requires 0.50+
2. **Rules on different code paths** — Both fire, but not on same finding/method
3. **Coordination logic has a bug** — Boolean checks incorrect

**Fix:**
- **Option 1:** Lower confidence threshold in coordination (e.g., `>= 0.50` → `>= 0.40`)
- **Option 2:** Expand scope detection (method-level → cross-method heuristics)
- **Option 3:** Review coordination logic for logic errors

---

### Problem: Coordination Over-Triggering

**Symptom:** Coordination activates > 50 times per day (expected ~2-15/day)

**Diagnosis:**
```bash
# Count activations per day
grep "Coordination boost: GCI" logs/gci-analysis.log | wc -l

# Check which rule is being boosted most
grep "Coordination boost: GCI" logs/gci-analysis.log | cut -d: -f1 | sort | uniq -c
```

**Root Causes:**
1. **Confidence threshold too low** — Noisy heuristics causing both rules to fire on false positives
2. **Rule pair too generic** — Both rules firing on unrelated code patterns
3. **Boost values too aggressive** — Moving low-confidence findings to high threshold

**Fix:**
- **Option 1:** Raise confidence threshold (e.g., `>= 0.50` → `>= 0.60`)
- **Option 2:** Add additional scope detection (confirm both in same method, same variable, etc.)
- **Option 3:** Lower boost values (e.g., 0.80 → 0.75)

---

### Problem: False Positives Still High (> 35%)

**Symptom:** After coordination deployment, FP rate remains above target

**Diagnosis:**
```bash
# Check if coordination is even running
grep "Applying.*Coordination" logs/gci-analysis.log | wc -l

# Sample actual findings to see if boosted or not
grep "Confidence" logs/gci-analysis.log | head -20
```

**Root Causes:**
1. **Coordination not activating** — See "Not Triggering" section above
2. **Underlying rule has poor heuristics** — Both rules fire, but FP rate still high
3. **Boost values insufficient** — Raising confidence 0.65 → 0.80 not enough
4. **Reporting threshold mismatch** — Boosted to 0.80 but system filters at 0.90

**Fix:**
- Verify coordination is activating (check logs)
- If activating: improve underlying rule heuristics (Phase 22 work)
- If not activating: see "Not Triggering" section

---

## Common Issues & Fixes

### Issue 1: Both Rules Fire, Coordination Doesn't Apply

**Error Log:**
```
GCI0024 fired: confidence 0.65 (method: LeakResource)
GCI0015 fired: confidence 0.55 (method: LeakResource)
No coordination applied
```

**Why:**
- One rule below threshold (0.55 < 0.50 minimum)
- OR logic check is AND when it should be OR
- OR rules detected on different methods despite being same fixture

**Solution:**
1. Check coordination logic in `SilverLabelEngine.cs`:
   ```csharp
   if (gci0024?.Confidence >= 0.50 && gci0015?.Confidence >= 0.50)
   ```
   - Verify both conditions use `>=`, not `>`
   - Verify both use same threshold (e.g., 0.50)

2. Lower threshold if justified:
   ```csharp
   if (gci0024?.Confidence >= 0.40 && gci0015?.Confidence >= 0.40)
   ```

3. Verify methods/scope match:
   ```csharp
   // Current: detect on same finding
   var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
   var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015");
   
   // If not triggering: expand to same method/file
   var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
   var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015" 
       && f.FilePath == gci0024?.FilePath);
   ```

---

### Issue 2: Boosted Confidence Too High, Findings Filtered Out

**Error Log:**
```
GCI0024 boosted: 0.65 → 0.80
Result finding has confidence 0.80, but report threshold is 0.90
Finding filtered from output
```

**Why:**
- Reporting threshold is higher than max boost value
- Coordination boost isn't sufficient to move finding above reporting threshold

**Solution:**
1. Check reporting/filtering logic (CLI or config):
   ```csharp
   // In AnalyzeCommand.cs or reporter
   var minConfidence = 0.85; // Too high if boosting to 0.80
   ```

2. Align boost values with reporting threshold:
   - If threshold is 0.90: boost to at least 0.92
   - If threshold is 0.85: boost to 0.88-0.90

3. Update boost values in coordination:
   ```csharp
   // Before
   boost GCI0024 from 0.65 → 0.80
   
   // After (if threshold is 0.85)
   boost GCI0024 from 0.65 → 0.87
   ```

---

### Issue 3: Coordination Applying to Wrong Findings

**Error Log:**
```
GCI0024 + GCI0015 coordination applied
But findings are: GCI0024 (FileIO) + GCI0015 (SQLQuery)
Expected both on same code pattern
```

**Why:**
- Scope detection too broad (FirstOrDefault matches wrong instance)
- Multiple instances of same rule in same fixture
- Method-level scope insufficient

**Solution:**
1. Add scope context to detection:
   ```csharp
   // Current (too broad)
   var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
   var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015");
   
   // Better (same method/line)
   var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
   var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015"
       && f.FilePath == gci0024?.FilePath
       && Math.Abs(f.LineNumber - gci0024.LineNumber) < 5);
   ```

2. Update test fixtures to validate scope:
   ```csharp
   // Fixture: both patterns in same method
   void BrokenMethod() 
   {
       var conn = GetConnection();  // GCI0024 fires here
       // ...
       SqlQuery(conn);              // GCI0015 fires here
       // conn never closed
   }
   
   // Fixture: patterns in different methods (should NOT trigger coordination)
   void Method1() { var c = GetConn(); }  // GCI0024
   void Method2() { SqlQuery(c); }        // GCI0015
   ```

---

## Tuning Confidence Boosts

### Understanding Confidence Score Impact

| Boost Delta | Effect | Use Case |
|---|---|---|
| +0.05-0.10 | Minor amplification | Very confident rule pairs |
| +0.15-0.25 | Moderate amplification | Standard coordination |
| +0.30+ | Aggressive amplification | Only for rare, high-signal pairs |

### How to Choose Boost Values

**Step 1: Determine baseline confidence**
```bash
# Sample findings before coordination
grep "GCI0024" logs/gci-pre-coordination.log | grep -o "confidence: [0-9.]*" | sort | uniq -c
# Output might show: most GCI0024 fires at 0.60-0.70, some at 0.50-0.60
```

**Step 2: Estimate reporting threshold**
```csharp
// In AnalyzeCommand or reporter
var reportingThreshold = 0.85;  // or config value
```

**Step 3: Calculate boost needed**
```
boost_target = reporting_threshold - 0.05 (safety margin)
boost_delta = boost_target - baseline_confidence
```

**Example:**
```
baseline_confidence(GCI0024) = 0.65
reporting_threshold = 0.85
boost_target = 0.85 - 0.05 = 0.80
boost_delta = 0.80 - 0.65 = 0.15

So: 0.65 → 0.80 (delta +0.15) ✓
```

**Step 4: Add safety margin**
```
Never boost above: (reporting_threshold + 0.05)
To avoid: findings disappearing if threshold raises in future
```

---

### Typical Boost Configurations

**Conservative (targeting 0.80 threshold):**
```csharp
// Rule baseline ~0.60, boost to ~0.78-0.82
if (rule1?.Confidence >= 0.55 && rule2?.Confidence >= 0.55)
{
    findings = findings.Replace(
        old: (rule1_id, 0.60) → (rule1_id, 0.80),
        old: (rule2_id, 0.55) → (rule2_id, 0.78)
    );
}
```

**Moderate (targeting 0.85 threshold):**
```csharp
// Rule baseline ~0.65, boost to ~0.85-0.90
if (rule1?.Confidence >= 0.60 && rule2?.Confidence >= 0.60)
{
    findings = findings.Replace(
        old: (rule1_id, 0.65) → (rule1_id, 0.88),
        old: (rule2_id, 0.60) → (rule2_id, 0.85)
    );
}
```

**Aggressive (only for high-confidence pairs):**
```csharp
// Rule baseline ~0.80, boost to ~0.95+
if (rule1?.Confidence >= 0.75 && rule2?.Confidence >= 0.75)
{
    findings = findings.Replace(
        old: (rule1_id, 0.80) → (rule1_id, 0.96),
        old: (rule2_id, 0.75) → (rule2_id, 0.92)
    );
}
```

---

## Testing New Coordinations

### Validation Checklist

Before committing a new coordination:

**1. Unit Test Coverage**
```csharp
[Test]
public async Task Coordination_DetectsBothRulesFiring_AppliesBoost()
{
    var findings = new List<ExpectedFinding>
    {
        new ExpectedFinding { RuleId = "GCI0024", Confidence = 0.65 },
        new ExpectedFinding { RuleId = "GCI0015", Confidence = 0.60 }
    };
    
    var result = ApplyResourceManagementCoordination(findings);
    
    var gci0024 = result.First(f => f.RuleId == "GCI0024");
    Assert.That(gci0024.Confidence, Is.EqualTo(0.80)); // boosted
}
```

**2. Fixture Testing**
```bash
# Place test fixtures in:
tests/GauntletCI.Benchmarks/Fixtures/curated/p21-[phase]-coordination/

# Include:
# - fixture_both_rules_fire.cs (both rules should trigger)
# - fixture_only_rule1.cs (only one rule, no boost)
# - fixture_only_rule2.cs (only one rule, no boost)
# - fixture_edge_case.cs (boundary conditions)
```

**3. Integration Test**
```bash
# Run full pipeline
dotnet test --filter "TestClass=SilverLabelEngineTests" -v normal

# Verify:
# - All coordination tests pass
# - No regressions in other tests
# - Full test suite: 1,494/1,494 passing
```

**4. Performance Check**
```csharp
// Coordination should be O(n) in findings count
var watch = Stopwatch.StartNew();
var boosted = ApplyResourceManagementCoordination(findings);
watch.Stop();

// Should be < 1ms for 1000 findings
Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1));
```

**5. Code Review Checklist**
- [ ] Coordination logic only *raises* confidence, never lowers
- [ ] Both rules use same confidence threshold gate (e.g., 0.50)
- [ ] Boost values documented in code comment
- [ ] Scope detection matches intent (same method, file, line range)
- [ ] Immutability pattern used (new findings, don't mutate)
- [ ] No side effects on other findings
- [ ] Test coverage >= 90%

---

## Phase 23 Specific Guidance

### Phase 23.0: GCI0016 Heuristic Improvements

**Monitoring GCI0016 improvements:**

After Phase 23.0 deployment, baseline GCI0016 confidence should shift higher:

```bash
# Before Phase 23.0 (baseline)
grep "GCI0016" logs/gci-pre-23.log | grep -o "confidence: [0-9.]*" | sort | uniq -c
# Expected: peak at 0.35-0.45 confidence (noisy)

# After Phase 23.0 (improved)
grep "GCI0016" logs/gci-post-23.log | grep -o "confidence: [0-9.]*" | sort | uniq -c
# Expected: peak at 0.55-0.65 confidence (cleaner)
```

**Key improvements in Phase 23.0:**
- Task.Run() now distinguished from Task.Run().Result (blocking guard added)
- Fire-and-forget patterns (with explicit markers) no longer trigger
- Startup context patterns filtered out
- ConfigureAwait(false) patterns treated as legitimate

### Phase 23 Coordinations (P4-P6)

#### P4 Performance & GC Coordination

**When to suspect P4 is under-triggering:**
```bash
# Check if both GCI0044 and GCI0035 are firing
grep "GCI0044\|GCI0035" logs/gci-analysis.log | head -20

# If both appear but coordination isn't applying:
grep "P4-Coordination" logs/gci-analysis.log | wc -l
# Expected: 1-5 per day on typical codebase
```

**Tuning P4:**
```
If P4 not triggering enough (< 1 per week):
  1. Lower threshold from 0.50 to 0.45 (more sensitive)
  2. Check if GCI0044 or GCI0035 baseline is weak (< 0.40)
  
If P4 over-triggering (> 10 per day):
  1. Raise threshold from 0.50 to 0.60 (more selective)
  2. Add line-proximity check (both within 5 lines of each other)
```

#### P5 Serialization Safety Coordination

**When to suspect P5 is under-triggering:**
```bash
# P5 is the highest-priority coordination (security)
# Should trigger on all RCE-vulnerable patterns

grep "P5-Coordination" logs/gci-analysis.log | wc -l
# Expected: 2-8 per day on typical codebase (higher than P4)
```

**Tuning P5:**
```
If P5 not triggering:
  1. Verify GCI0039 and GCI0048 baseline confidence is reasonable (≥0.55, ≥0.60)
  2. P5 has higher thresholds than P4 intentionally (security-critical)
  3. Check scope: both must be on HTTP client instantiation path
  
If P5 over-triggering (> 15 per day):
  1. Raise thresholds: GCI0039 ≥0.60, GCI0048 ≥0.70
  2. Validate scope detection (currently FirstOrDefault on same finding)
```

#### P6 Dependency Injection & Async Coordination

**When to suspect P6 is under-triggering:**
```bash
# P6 depends on improved GCI0016 from Phase 23.0
# Make sure Phase 23.0 is deployed first

grep "P6-Coordination" logs/gci-analysis.log | wc -l
# Expected: 1-4 per day (rarer than P4/P5, depends on service locator usage)

# If low: check if GCI0016 baseline improved after Phase 23.0
grep "GCI0016" logs/gci-post-23.log | grep -o "confidence: [0-9.]*" | stats
```

**Tuning P6:**
```
P6 requires GCI0016 baseline confidence ≥0.55 to activate
  - If GCI0016 baseline is weak: improve Phase 23.0 heuristics
  - If GCI0016 strong but P6 not triggering: lower threshold to 0.50

P6 interacts with Phase 23.0 heuristics:
  - Task.Run() blocking guard: May reduce false GCI0016 findings
  - Fire-and-forget marker: May filter out legitimate async patterns
  - Result: Fewer low-confidence GCI0016 findings, higher precision P6
```

### Phase 21 vs Phase 23 Interactions

**Important:** Phase 23 coordinations are independent of Phase 21:

```
Phase 21 (P0-P3): First four coordinations deployed
Phase 23 (P4-P6): Three new coordinations, independent scope
```

- P4, P5, P6 do NOT interact with P0, P1, P2, P3
- P6 depends on GCI0016 heuristics (Phase 23.0), not Phase 21 coordination
- You can rollback Phase 23 without affecting Phase 21

---

## Logging & Monitoring

### Enable Coordination Logs

In `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs`:

```csharp
private IEnumerable<ExpectedFinding> ApplyResourceManagementCoordination(
    IEnumerable<ExpectedFinding> findings)
{
    var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
    var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015");
    
    if (gci0024?.Confidence >= 0.50 && gci0015?.Confidence >= 0.50)
    {
        logger.LogInformation(
            "P2-Coordination: Both GCI0024 (conf={OldConf}) and GCI0015 fired, applying boosts",
            gci0024.Confidence);
        
        // Apply boosts...
        var boostedGci0024 = new ExpectedFinding 
        {
            RuleId = gci0024.RuleId,
            Confidence = 0.80,  // was 0.65
            // ... other fields
        };
        
        logger.LogInformation(
            "P2-Coordination: Boost applied - GCI0024 {Old} -> {New}",
            gci0024.Confidence, boostedGci0024.Confidence);
    }
    
    return findings;
}
```

### Query Logs for Debugging

```bash
# Find all coordination activations
grep "Coordination:" logs/gci-analysis.log

# Count by phase
grep "P0-Coordination" logs/gci-analysis.log | wc -l
grep "P1-Coordination" logs/gci-analysis.log | wc -l
grep "P2-Coordination" logs/gci-analysis.log | wc -l

# Find boosted findings
grep "Boost applied" logs/gci-analysis.log | head -20

# Check for coordination failures
grep -i "error\|exception" logs/gci-analysis.log | grep -i "coordination"
```

### Metrics to Export

```
# Prometheus-style metrics - Phase 21 coordinations
gauntletci_coordination_activations_total{phase="p0|p1|p2|p3"} 
gauntletci_coordination_boost_applied{rule_id="GCI0024"} 0.80
gauntletci_coordination_confidence_delta{rule_id="GCI0024"} 0.15
gauntletci_false_positive_rate 0.25
gauntletci_coordination_skipped{reason="low_confidence"} 15

# Phase 23 specific metrics
gauntletci_coordination_activations_total{phase="p4|p5|p6"}
gauntletci_gci0016_baseline_confidence_after_23_0   0.60 (improved from 0.40)
gauntletci_phase_23_heuristic_improvement_pct      +5-8
gauntletci_coordination_performance_gc_boost{rule="GCI0044"}  0.30
gauntletci_coordination_serialization_boost{rule="GCI0048"}   0.42
gauntletci_coordination_di_async_boost{rule="GCI0045"}        0.37

# Cumulative impact (Phase 21 + 23)
gauntletci_cumulative_fp_reduction_pct  39-60 (target)
gauntletci_total_coordinations_deployed 7 (P0-P3, P4-P6)
```

---

## Rollback Procedure

### If Coordination Causes Issues

**Critical Issue (FP rate > 50%, or real bugs missed):**

```bash
cd /path/to/GauntletCI

# Option 1: Full rollback (Phase 21 + 23)
# Keep neither Phase 21 nor Phase 23 coordinations
git revert <phase-21-commit> <phase-23-commit> --no-edit
git push

# Option 2: Rollback Phase 23 only (keep Phase 21)
git revert <phase-23-commit> --no-edit
git push

# Option 3: Rollback Phase 23.0 heuristics only (keep P4-P6 coordinations)
# Edit GCI0016_ConcurrencyAndStateRisk.cs, revert heuristic changes
git commit -am "ops: rollback Phase 23.0 GCI0016 heuristics"
git push

# 4. Run tests to confirm no new failures
dotnet test -q

# 5. Deploy v2.x.0-hotfix (without problematic phase)
gh release create v2.x.0-hotfix --draft
```

### Partial Rollback (Keep P0-P3, Remove P4)

If only P4 coordination is problematic:

```bash
# Edit SilverLabelEngine.cs
# Remove/comment line: inferred = ApplyPhase23P4PerformanceCoordination(inferred);
git commit -am "ops: disable P4-coordination while investigating"
git push
```

### Granular Rollback Matrix

| Scenario | Action | FP Impact |
|----------|--------|-----------|
| P6 alone breaks | Comment P6 call | -6% (keep P0-P5) |
| P5 alone breaks | Comment P5 call | -5% (keep P0-P4, P6) |
| P4 alone breaks | Comment P4 call | -3% (keep P0-P3, P5-P6) |
| Phase 23.0 breaks P6 | Revert Phase 23.0 heuristics | Keep P0-P3, P4-P5 (no P6) |
| All Phase 23 breaks | Revert Phase 23 commit | Revert to Phase 21 (25-36% FP reduction) |
| All coordinations break | Revert P0-P3 + P4-P6 | Baseline (40-50% FP) |

### Rollback Time

| Scenario | Time | Impact |
|----------|------|--------|
| Full revert | 2-5 minutes | Immediate deploy |
| Partial (one coordination) | 2-3 minutes | FP reduction drops ~3-6% |
| Heuristic revert | 1-2 minutes | P6 may not activate, but P4-P5 remain |
| Code fix + re-deploy | 15-30 minutes | If issue is fixable, redeploy with fix |

---

## Contact & Escalation

- **Phase 21 Questions:** See `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Phase 23 Questions:** See `docs/architecture/adr-0005-phase-23-heuristics-and-coordinations.md`
- **Monitoring Issues:** See `docs/operations/phase-21-monitoring.md`
- **Phase 24+ Coordinations:** Follow this runbook as template
- **Critical Alert:** Initiate rollback → post-mortem within 1 hour

### Phase 23 Escalation Checklist

If Phase 23 coordination causes issues:

```
[ ] Check FP rate increased > 40% (vs 25-36% baseline)
[ ] Check logs for P4/P5/P6-Coordination messages
[ ] Verify Phase 23.0 heuristics deployed correctly
[ ] Run rollback test (disable one coordination at a time)
[ ] Measure impact (which coordination caused regression?)
[ ] Initiate rollback if FP increase > 10%
[ ] Document finding in post-mortem for Phase 24 planning
```
