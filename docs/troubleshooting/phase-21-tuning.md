# Phase 21 Troubleshooting Guide

**Purpose:** Diagnose and resolve issues with Phase 21 coordinations  
**Audience:** Developers, DevOps, Support Engineers  
**Scope:** v2.4.0+ (P0), v2.5.0+ (P1), v2.6.0+ (P2)

---

## Table of Contents

1. [Common Issues](#common-issues)
2. [Diagnostic Procedures](#diagnostic-procedures)
3. [Adjusting Confidence Thresholds](#adjusting-confidence-thresholds)
4. [Disabling Specific Coordinations](#disabling-specific-coordinations)
5. [Performance Tuning](#performance-tuning)

---

## Common Issues

### Issue 1: Coordination Not Triggering (Missing Expected Boosts)

**Symptoms:**
- Both GCI0024 and GCI0015 appear in findings, but confidence not boosted
- Expected boost (+0.15) didn't happen
- Review notes: "Expected GCI0024 0.65 → 0.80 but got 0.65"

**Root Causes:**

| Cause | Evidence | Fix |
|-------|----------|-----|
| Confidence too low | Both rules have confidence < 0.50 | See [Adjusting Thresholds](#adjusting-confidence-thresholds) |
| Rules in different methods | GCI0024 in method A, GCI0015 in method B | Expand scope (see below) |
| Rule IDs don't match | Typo in coordination logic (GCI0015 vs GCI015) | Check SilverLabelEngine.cs line 2090 |
| Coordination disabled | Feature flag off | Check deployment configuration |

**Diagnostic Steps:**

1. **Check if both rules fired:**
```csharp
// In SilverLabelEngine.cs, add debug logging
var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015");
if (gci0024 != null) logger.LogDebug("GCI0024 found: confidence={Conf}", gci0024.Confidence);
if (gci0015 != null) logger.LogDebug("GCI0015 found: confidence={Conf}", gci0015.Confidence);
```

2. **Check confidence values:**
```
If GCI0024.Confidence >= 0.50 AND GCI0015.Confidence >= 0.50
  → Coordination should trigger
Else
  → Need to improve underlying rule confidence
```

3. **Check scope detection:**
```
Are both findings in the same method?
- If yes: coordination should trigger
- If no: scope detection rejected them (working as intended)
```

**Resolution:**

**Option A: Lower confidence gate** (risky—may boost low-confidence findings)
```csharp
// In SilverLabelEngine.cs line 2080
if (gci0024?.Confidence >= 0.40 && gci0015?.Confidence >= 0.40)  // Was 0.50
```

**Option B: Improve rule heuristics** (correct approach)
- Improve GCI0024 heuristics to reliably fire at ≥ 0.50
- Improve GCI0015 heuristics to reliably fire at ≥ 0.50
- See `/docs/rules` for per-rule tuning

**Option C: Expand scope detection** (medium effort)
```csharp
// Currently: both in same method
// Could add: both in same class, or same transaction scope
private bool InSameScope(ExpectedFinding f1, ExpectedFinding f2)
{
    // Current: method-level
    return f1.MethodName == f2.MethodName;
    
    // Extended: class-level (catches more patterns, may boost FP)
    // return f1.ClassName == f2.ClassName;
}
```

---

### Issue 2: Coordination Over-Boosting (False Positives Increase)

**Symptoms:**
- FP rate increased after Phase 21 deployment
- Many GCI0024 + GCI0015 boosts appear in user feedback as "not a real risk"
- Boosted findings have scenarios that don't match the warning

**Root Causes:**

| Cause | Evidence | Fix |
|-------|----------|-----|
| Threshold too low | Coordination activates on low-confidence rules | Raise minimum confidence gate |
| Boost delta too high | 0.65 → 0.80 too aggressive | Lower boost target |
| Scope too broad | Unrelated rules boosted together | Tighten scope detection |

**Diagnostic Steps:**

1. **Check activation rate:**
```
If P2 coordination activates > 10 times/day
  → Threshold too low, raising too many low-signal findings
Else
  → Over-boosting may be in boost magnitude, not frequency
```

2. **Sample false positives:**
Collect 5-10 user-reported FP findings with boosts. Analyze:
```
Pattern: Are they boosted when both rules fired on unrelated code paths?
Example: GCI0024 (resource leak) in Database.cs + GCI0015 (data integrity) in API.cs
  → Scope too broad, should be same method/class
```

3. **Check boost magnitude:**
```
Current P2 boosts: 0.65 → 0.80 (+0.15 delta)
If users consistently report boosted findings as FP:
  → Delta too high, consider 0.65 → 0.75 (+0.10)
```

**Resolution:**

**Option A: Raise minimum confidence gate** (safest)
```csharp
// SilverLabelEngine.cs line 2080
if (gci0024?.Confidence >= 0.60 && gci0015?.Confidence >= 0.60)  // Was 0.50
```
Effect: Fewer coordinations activate, but only on high-confidence signals

**Option B: Lower boost magnitude** (medium risk)
```csharp
// SilverLabelEngine.cs line 2090
var boostedGci0024 = gci0024 with { Confidence = Math.Min(0.75, gci0024.Confidence * 1.15) };  // Was 0.80
var boostedGci0015 = gci0015 with { Confidence = Math.Min(0.70, gci0015.Confidence * 1.12) };  // Was 0.75
```
Effect: Boosts are gentler, may preserve more false negatives

**Option C: Tighten scope detection** (recommended)
```csharp
// Only coordinate if both rules fire in same method AND same code block
private bool InSameCodeBlock(ExpectedFinding f1, ExpectedFinding f2)
{
    return f1.MethodName == f2.MethodName && 
           f1.LineStart <= f2.LineEnd && 
           f2.LineStart <= f1.LineEnd;
}
```
Effect: Fewer spurious boosts, more precise signals

---

### Issue 3: Coordination Performance Impact

**Symptoms:**
- GauntletCI analysis slower after Phase 21 deployment
- Labeling phase takes 2-3x longer
- User perceives delay in pre-commit hook

**Root Causes:**
- Coordination scope detection doing expensive checks (unlikely)
- Debugging/logging overhead
- Unrelated performance regression (verify with profiling)

**Diagnostic:**

```bash
# Profile SilverLabelEngine coordination methods
dotnet run --project tests/GauntletCI.Benchmarks \
  --filter "Phase21*" \
  -- --memory --profiler sampling
```

**Resolution:**

If coordination is actually slower:
1. Check scope detection logic for O(n²) or regex-heavy operations
2. Cache scope determination results
3. Consider lazy evaluation

Likely: negligible performance impact (coordination adds ~10-20ms to 100ms+ labeling phase).

---

## Diagnostic Procedures

### Procedure A: Check if Coordination Fired

**Goal:** Determine why a specific finding wasn't boosted

**Steps:**

1. **Get the finding details:**
```
Rule ID: GCI0024
Confidence: 0.68
Method: Database.OpenConnection()
File: src/Data/DataAccess.cs
```

2. **Check if companion rule fired:**
```csharp
// In SilverLabelEngine.cs, add temporary logging
logger.LogInformation("Analyzing {RuleId} at {Method}:{Line}",
    finding.RuleId, finding.MethodName, finding.LineNumber);

// Check for companion rule
var companion = findings.FirstOrDefault(f => 
    f.RuleId == "GCI0015" && 
    f.MethodName == finding.MethodName);

if (companion != null)
    logger.LogInformation("Companion GCI0015 found at {Method}:{Line}",
        companion.MethodName, companion.LineNumber);
else
    logger.LogInformation("No companion GCI0015 in same method");
```

3. **Check confidence thresholds:**
```
GCI0024 confidence: 0.68 >= 0.50? YES ✓
GCI0015 confidence: 0.55 >= 0.50? YES ✓
→ Both pass threshold, coordination should apply
```

4. **Run tests:**
```bash
cd tests/GauntletCI.Benchmarks
dotnet test --filter "ResourceManagement" -v d
```

---

### Procedure B: Verify Boost Was Applied

**Goal:** Confirm that coordination logic ran and confidence was updated

**Steps:**

1. **Check logs for boost event:**
```
Grep for coordination activation:
  logs | grep "Coordination: Both GCI0024 and GCI0015"
  
If found:
  → Boost was applied
  → Check if new confidence matches expected value (0.80, 0.75)
Else:
  → Boost didn't apply, investigate Issue #1 or #2
```

2. **Manual test case:**
```csharp
var findings = new[] 
{
    new ExpectedFinding("GCI0024", confidence: 0.68),
    new ExpectedFinding("GCI0015", confidence: 0.55)
};

var result = engine.ApplyResourceManagementCoordination(findings);
// Expected: GCI0024 boosted to 0.80, GCI0015 boosted to 0.75
```

---

## Adjusting Confidence Thresholds

### Threshold A: Minimum Confidence to Activate Coordination

**Current setting:** Both rules must have confidence ≥ 0.50

**Location:** `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs` line 2080

**How to adjust:**

```csharp
// Current
if (gci0024?.Confidence >= 0.50 && gci0015?.Confidence >= 0.50)

// More aggressive (boosts more findings)
if (gci0024?.Confidence >= 0.40 && gci0015?.Confidence >= 0.40)

// More conservative (boosts only high-confidence findings)
if (gci0024?.Confidence >= 0.60 && gci0015?.Confidence >= 0.60)
```

**Trade-offs:**

| Threshold | Activation Rate | FP Impact | FN Impact |
|-----------|-----------------|-----------|-----------|
| **0.40** | Very frequent | Boosts noise | Catches edge cases |
| **0.50** | Moderate (default) | Balanced | Balanced |
| **0.60** | Rare | Less boost noise | Misses compound risks |
| **0.70** | Very rare | Minimal | Many missed patterns |

**Recommendation:**
- If FP rate > 35%: raise to 0.60
- If FP rate < 20%: lower to 0.40
- If balanced: keep at 0.50

---

### Threshold B: Boost Target Confidence

**Current setting:** Boost to 0.80 for GCI0024, 0.75 for GCI0015

**Location:** `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs` lines 2095-2100

**How to adjust:**

```csharp
// Current
var boostedGci0024 = gci0024 with { Confidence = 0.80 };  // From 0.65
var boostedGci0015 = gci0015 with { Confidence = 0.75 };  // From 0.60

// Gentler boosts
var boostedGci0024 = gci0024 with { Confidence = 0.75 };
var boostedGci0015 = gci0015 with { Confidence = 0.70 };

// More aggressive boosts
var boostedGci0024 = gci0024 with { Confidence = 0.85 };
var boostedGci0015 = gci0015 with { Confidence = 0.80 };
```

**Trade-offs:**

| Boost | FP Impact | FN Impact | Example |
|-------|-----------|-----------|---------|
| **0.75** (gentle) | Keeps more false +s | May miss edge cases | GCI0024: 0.65 → 0.75 |
| **0.80** (default) | Balanced | Balanced | GCI0024: 0.65 → 0.80 |
| **0.85+** (aggressive) | Reduces false +s | Risks false negatives | GCI0024: 0.65 → 0.85 |

**Recommendation:**
- If false negatives increase: lower boost (0.75)
- If false positives increase: raise threshold gate instead (don't lower boost)

---

## Disabling Specific Coordinations

### Disable P0 (Async Coordination)

If P0 is causing issues:

```csharp
// In SilverLabelEngine.cs line 305-306
// Comment out:
// inferred = ApplyAsyncExecutionCoordination(inferred);

// Or add feature flag:
if (_featureFlags.IsPhase21P0Enabled)
    inferred = ApplyAsyncExecutionCoordination(inferred);
```

**Effect:** GCI0016, GCI0039, GCI0044 boosts disabled  
**FP Reduction maintained:** ~6-10% (from P1+P2)

---

### Disable P1 (Exception Handling Coordination)

```csharp
// In SilverLabelEngine.cs line 306-307
// Comment out:
// inferred = ApplyExceptionHandlingCoordination(inferred);

if (_featureFlags.IsPhase21P1Enabled)
    inferred = ApplyExceptionHandlingCoordination(inferred);
```

**Effect:** GCI0032, GCI0003, GCI0016 boosts disabled  
**FP Reduction maintained:** ~8-12% (from P0+P2)

---

### Disable P2 (Resource Management Coordination)

```csharp
// In SilverLabelEngine.cs line 308
// Comment out:
// inferred = ApplyResourceManagementCoordination(inferred);

if (_featureFlags.IsPhase21P2Enabled)
    inferred = ApplyResourceManagementCoordination(inferred);
```

**Effect:** GCI0024, GCI0015 boosts disabled  
**FP Reduction maintained:** ~14-22% (from P0+P1)

---

## Performance Tuning

### Optimization 1: Lazy Evaluation

If scope detection is expensive:

```csharp
// Current: eager evaluation
private bool InSameMethod(ExpectedFinding f1, ExpectedFinding f2)
{
    return f1.MethodName == f2.MethodName;  // String comparison
}

// Optimized: cache + lazy
private readonly Dictionary<string, int> _methodNameCache = new();
private bool InSameMethod(ExpectedFinding f1, ExpectedFinding f2)
{
    var key1 = _methodNameCache.GetOrAdd(f1.MethodName, f => f.GetHashCode());
    var key2 = _methodNameCache.GetOrAdd(f2.MethodName, f => f.GetHashCode());
    return key1 == key2;
}
```

### Optimization 2: Early Exit

If one rule doesn't fire, skip coordination entirely:

```csharp
// Current: linear search
var gci0024 = findings.FirstOrDefault(f => f.RuleId == "GCI0024");
var gci0015 = findings.FirstOrDefault(f => f.RuleId == "GCI0015");

// Optimized: index findings by rule for O(1) lookup
private Dictionary<string, ExpectedFinding> _findingsByRule = new();
var found0024 = _findingsByRule.TryGetValue("GCI0024", out var gci0024);
var found0015 = _findingsByRule.TryGetValue("GCI0015", out var gci0015);
if (!found0024 || !found0015) return findings;  // Early exit
```

---

## Getting Help

| Issue Type | Reference |
|------------|-----------|
| Architecture questions | `docs/architecture/adr-0004-phase-21-coordinations.md` |
| Monitoring/production | `docs/operations/phase-21-monitoring.md` |
| Implementation details | `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs` |
| Test cases | `tests/GauntletCI.Benchmarks/Fixtures/curated/p21-*/` |

---

## See Also

- **Architecture:** `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Monitoring:** `docs/operations/phase-21-monitoring.md`
- **Release Notes:** `docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md`
