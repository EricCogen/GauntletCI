# Phase 21 Coordination Debugging & Tuning Runbook

**Purpose:** Operational guide for debugging false positives, tuning coordination parameters, and validating new coordination patterns  
**Audience:** DevOps, SRE, Engineers adding new coordinations (Phase 22+)  
**Scope:** Phase 21 coordinations (P0-P3) + template for future phases

---

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Common Issues & Fixes](#common-issues--fixes)
3. [Tuning Confidence Boosts](#tuning-confidence-boosts)
4. [Testing New Coordinations](#testing-new-coordinations)
5. [Logging & Monitoring](#logging--monitoring)
6. [Rollback Procedure](#rollback-procedure)

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
# Prometheus-style metrics
gauntletci_coordination_activations_total{phase="p0|p1|p2"} 
gauntletci_coordination_boost_applied{rule_id="GCI0024"} 0.80
gauntletci_coordination_confidence_delta{rule_id="GCI0024"} 0.15
gauntletci_false_positive_rate 0.25
gauntletci_coordination_skipped{reason="low_confidence"} 15
```

---

## Rollback Procedure

### If Coordination Causes Issues

**Critical Issue (FP rate > 50%, or real bugs missed):**

```bash
cd /path/to/GauntletCI

# 1. Identify problematic commit
git log --oneline | grep -i "coordination\|phase"
# Output: 4b2b52f cleanup: phase-21-p3-coordination

# 2. Revert commit
git revert 4b2b52f --no-edit

# 3. Verify revert
git log -1 --oneline

# 4. Run tests to confirm no new failures
dotnet test -q

# 5. Push
git push origin main

# 6. Deploy v2.x.0-pre (without latest coordination)
gh release create v2.x.0-pre --draft
```

### Partial Rollback (Keep P0-P1, Remove P2)

If only P2 (Phase 21.2) is problematic:

```bash
# Edit SilverLabelEngine.cs
# Remove line: inferred = ApplyResourceManagementCoordination(inferred);
git commit -am "ops: disable P2-coordination while investigating"
git push
```

### Full Rollback (Keep P0 only)

```bash
# Revert all coordinations except P0
# In SilverLabelEngine.cs, remove:
#   - inferred = ApplyExceptionHandlingCoordination(inferred);
#   - inferred = ApplyResourceManagementCoordination(inferred);
#   - inferred = ApplyDataSecurityCoordination(inferred);
# Keep:
#   - inferred = ApplyAsyncExecutionCoordination(inferred);

git commit -am "ops: rollback to P0-coordination only"
git push
```

**Rollback time:** 2-5 minutes  
**Impact:** FP reduction drops from 25-36% → 8-12% (P0 only)

---

## Contact & Escalation

- **Coordination Questions:** See ADR-0004: `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Monitoring Issues:** See `docs/operations/phase-21-monitoring.md`
- **Phase 22+ Coordinations:** Follow this runbook as template
- **Critical Alert:** Initiate rollback → post-mortem within 1 hour
