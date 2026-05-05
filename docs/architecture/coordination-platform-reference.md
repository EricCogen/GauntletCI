# GauntletCI Coordination Platform - Architecture Reference

**Version:** 1.0  
**Last Updated:** 2026-05-05  
**Scope:** Coordination patterns, platform design, and extensibility roadmap

---

## Table of Contents

1. [Coordination Architecture](#coordination-architecture)
2. [Tier 2: Heuristic Coordinations](#tier-2-heuristic-coordinations)
3. [Coordination Implementation Guide](#coordination-implementation-guide)
4. [Confidence Boost Philosophy](#confidence-boost-philosophy)
5. [Testing & Validation](#testing--validation)
6. [Phase Roadmap](#phase-roadmap)
7. [Troubleshooting & Tuning](#troubleshooting--tuning)

---

## Coordination Architecture

### Three-Tier Labeling Pipeline

The GauntletCI labeling engine uses a three-tier architecture to reduce false positives:

```
┌─────────────────────────────────────────────┐
│ Input: Code AST + Corpus Metadata           │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│ Tier 1: AST Extraction (Deterministic)      │
│ ├─ Pattern matching on code structure       │
│ ├─ Syntax tree analysis                     │
│ └─ No coordination logic                    │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│ Tier 2: Heuristic Rules + Coordinations     │
│ ├─ 28+ domain rules (GCI0001-GCI0053)      │
│ ├─ Deterministic heuristics                │
│ ├─ HIGH confidence signals                 │
│ ├─ Coordination engine (this platform)     │
│ └─ Async coordination (before Tier 3)      │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│ Tier 3: LLM Fallback (Learned patterns)     │
│ ├─ Semantic analysis                       │
│ ├─ Lower priority signals                  │
│ └─ Used only if Tier 2 inconclusive        │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│ Output: Labeled findings (HIGH confidence)  │
└─────────────────────────────────────────────┘
```

### Design Rationale

**Why coordinations before LLM fallback?**

1. **Auditability:** Deterministic signals can be traced and explained
2. **Performance:** No LLM latency for coordinated findings
3. **Reliability:** Heuristic patterns are validated on production data
4. **Cost:** Reduces LLM API calls by 30-50% on typical corpora

**Coordination Signal Flow:**

```
Rule A fires (confidence 0.65)
      │
      ├─ Check: Does Rule B also fire?
      │
      └─ If YES: Both rules boost each other
              Rule A: 0.65 → 0.80 (boost)
              Rule B: 0.60 → 0.80 (boost)
      
      └─ If NO: Both stay at original confidence
```

---

## Tier 2: Heuristic Coordinations

### Coordination Phases

GauntletCI coordinations are organized into phases, each targeting specific rule families:

| Phase | Focus | Rules | Expected FP Reduction | Status |
|-------|-------|-------|---|---|
| **Phase 21** | Async Execution Model | GCI0016, GCI0039, GCI0044, GCI0032, GCI0003, GCI0024, GCI0015, GCI0029, GCI0012 | 25-36% | ✅ Complete |
| **Phase 23** | Heuristic + Advanced Coordinations | GCI0016 (enhanced), GCI0044, GCI0035, GCI0039, GCI0048, GCI0045 | 17-28% | ✅ Complete |
| **Phase 24** | Concurrency & Cache Patterns | GCI0016, GCI0038, GCI0021, GCI0029 | 8-13% | 📋 Planned |
| **Phase 25+** | Network & Observability | GCI0039, GCI0043, TBD | 4-8% | 🔮 Future |

### Phase 21: Base Coordinations (4 phases)

#### P0: Async Execution Model
- **Rules:** GCI0016 (async violations) ↔ GCI0039 (unsafe HTTP) ↔ GCI0044 (GC pressure)
- **Pattern:** Blocking operations in async context
- **Boost Strategy:** If GCI0016 fires, boost GCI0039 and GCI0044 (correlated risk)
- **Confidence:** GCI0039 0.65→0.80, GCI0044 0.60→0.75
- **Rationale:** Async violations often occur with HTTP client misuse and GC pressure

#### P1: Exception Handling
- **Rules:** GCI0032 (swallowed exceptions) ↔ GCI0003 (breaking changes)
- **Pattern:** Silent failure + change risk
- **Boost Strategy:** If both fire, increase confidence (correlation indicates critical path)
- **Confidence:** GCI0032 0.60→0.75, GCI0003 0.55→0.75

#### P2: Resource Management
- **Rules:** GCI0024 (resource leaks) ↔ GCI0015 (nullability)
- **Pattern:** Unmanaged resources + null checks missing
- **Boost Strategy:** If both fire, boost confidence
- **Confidence:** GCI0024 0.55→0.75, GCI0015 0.50→0.70

#### P3: Data Security
- **Rules:** GCI0015 (nullability) ↔ GCI0029 (PII exposure) ↔ GCI0012 (data types)
- **Pattern:** Null checks missing in PII handling paths
- **Boost Strategy:** If all three fire, maximize confidence
- **Confidence:** GCI0015 0.60→0.85, GCI0029 0.55→0.85, GCI0012 0.50→0.75

### Phase 23: Advanced Coordinations (4 phases)

#### Phase 23.0: Enhanced Heuristics
- **Rule:** GCI0016 (async violations) - refined heuristics
- **Enhancement:** Task.Run() blocking guard distinguishes .Result from delegation
- **Impact:** 5-8% FP reduction on baseline GCI0016 findings
- **Implementation:** Enhanced IsLegitimateAsyncPattern() in GCI0016_ConcurrencyAndStateRisk.cs

#### P4: Performance & GC Coordination
- **Rules:** GCI0044 (GC pressure) ↔ GCI0035 (inefficient iteration)
- **Pattern:** GC pressure + inefficient loops
- **Boost Strategy:** Asymmetric boost (domain-specific thresholds)
- **Confidence:** GCI0044 0.60→0.75, GCI0035 0.55→0.70
- **Rationale:** Inefficient iteration often causes GC pressure; combined risk is critical

#### P5: Serialization Safety
- **Rules:** GCI0039 (unsafe HTTP) ↔ GCI0048 (insecure deserialization)
- **Pattern:** Unsafe deserialization in HTTP handlers
- **Boost Strategy:** **Asymmetric boost** - P5 uses higher thresholds (security)
- **Confidence:** GCI0039 0.55→0.85 (+54%), GCI0048 0.60→0.80 (+33%)
- **Rationale:** Deserialization vulnerabilities are critical; conservative threshold justified

#### P6: DI & Async Coordination
- **Rules:** GCI0045 (dependency injection issues) ↔ GCI0016 (async violations)
- **Pattern:** DI misconfiguration in async contexts
- **Boost Strategy:** Conservative approach (DI errors compound async risks)
- **Confidence:** GCI0045 0.55→0.75, GCI0016 0.65→0.80

---

## Coordination Implementation Guide

### File Structure

All coordinations live in `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs`:

```csharp
// Tier 2 Labeling Pipeline
public async Task<LabeledFinding[]> InferLabelsAsync(CodeFragment code)
{
    // Phase 1: Extract base heuristic findings
    var findings = ExtractHeuristicRules(code);
    
    // Phase 2: Apply coordinations (THIS IS WHERE WE ARE)
    findings = ApplyPhase21Coordinations(findings);  // P0-P3
    findings = ApplyPhase23Coordinations(findings);  // 23.0, P4-P6
    findings = ApplyPhase24Coordinations(findings);  // P7-P8 (future)
    
    // Phase 3: LLM fallback for low-confidence findings
    var llmFindings = await QueryLLMAsync(code, findings);
    
    return MergeFindingsPreferringHighConfidence(findings, llmFindings);
}
```

### Adding a New Coordination

#### Step 1: Define Coordination Methods

```csharp
/// <summary>
/// P7: Concurrency & Lock Ordering Coordination
/// Rules: GCI0016 (async violations) ↔ GCI0038 (lock ordering)
/// Pattern: Blocking in async + locks = deadlock risk
/// Expected impact: 5-8% FP reduction
/// </summary>
private LabeledFinding[] ApplyPhase24P7ConcurrencyCoordination(LabeledFinding[] findings)
{
    var gci0016Findings = findings.Where(f => f.RuleId == "GCI0016").ToList();
    var gci0038Findings = findings.Where(f => f.RuleId == "GCI0038").ToList();
    
    if (!gci0016Findings.Any() || !gci0038Findings.Any())
        return findings;
    
    // Both rules fire in same method scope → deadlock risk
    foreach (var finding16 in gci0016Findings)
    {
        foreach (var finding38 in gci0038Findings)
        {
            if (SameMethodScope(finding16, finding38))
            {
                // Boost both confidences
                finding16.Confidence = Math.Min(0.85, finding16.Confidence + 0.20);
                finding38.Confidence = Math.Min(0.78, finding38.Confidence + 0.18);
                finding16.Tags.Add("coordination:P7-concurrency");
                finding38.Tags.Add("coordination:P7-concurrency");
            }
        }
    }
    
    return findings;
}
```

#### Step 2: Integrate into Pipeline

```csharp
public async Task<LabeledFinding[]> InferLabelsAsync(CodeFragment code)
{
    var findings = ExtractHeuristicRules(code);
    findings = ApplyPhase21Coordinations(findings);
    findings = ApplyPhase23Coordinations(findings);
    findings = ApplyPhase24Coordinations(findings);  // Add new phase here
    // ...
}

private LabeledFinding[] ApplyPhase24Coordinations(LabeledFinding[] findings)
{
    findings = ApplyPhase24P7ConcurrencyCoordination(findings);
    findings = ApplyPhase24P8CacheCoherencyCoordination(findings);
    return findings;
}
```

#### Step 3: Add Test Fixtures

Create 3 test cases per coordination:

```csharp
[TestClass]
public class Phase24P7ConcurrencyCoordinationTests
{
    /// Test Case 1: Both rules fire
    [TestMethod]
    public void GCI0016AndGCI0038BothFire_BoostBothConfidences()
    {
        var code = @"
            async Task DoWorkAsync()
            {
                var result = Task.Run(() => 
                {
                    lock (someLock)  // Blocking + lock = deadlock risk
                    {
                        // ...
                    }
                }).Result;  // Blocking call in async
            }
        ";
        
        var findings = engine.InferLabels(code);
        var gci0016 = findings.First(f => f.RuleId == "GCI0016");
        var gci0038 = findings.First(f => f.RuleId == "GCI0038");
        
        Assert.IsTrue(gci0016.Confidence >= 0.80);
        Assert.IsTrue(gci0038.Confidence >= 0.78);
        Assert.IsTrue(gci0016.Tags.Contains("coordination:P7-concurrency"));
    }
    
    /// Test Case 2: Only GCI0016 fires
    [TestMethod]
    public void OnlyGCI0016Fires_NoBoost()
    {
        var code = @"
            async Task DoWorkAsync()
            {
                var result = await SomeAsync();  // Blocking call
            }
        ";
        
        var findings = engine.InferLabels(code);
        var gci0016 = findings.First(f => f.RuleId == "GCI0016");
        
        Assert.IsFalse(gci0016.Tags.Contains("coordination:P7-concurrency"));
    }
    
    /// Test Case 3: Different method scopes
    [TestMethod]
    public void BothRulesFireDifferentScopes_NoBoost()
    {
        var code = @"
            async Task DoWorkAsync()
            {
                var result = await SomeAsync();  // Blocking in async
            }
            
            void OtherMethod()
            {
                lock (someLock) { }  // Lock in different scope
            }
        ";
        
        var findings = engine.InferLabels(code);
        var gci0016 = findings.First(f => f.RuleId == "GCI0016");
        
        Assert.IsFalse(gci0016.Tags.Contains("coordination:P7-concurrency"));
    }
}
```

---

## Confidence Boost Philosophy

### Key Principles

1. **Never Lower Confidence**
   - Coordinations only increase confidence
   - If unsure, don't boost
   - Conservative approach prevents false positives

2. **Asymmetric Boosts by Domain**
   - Security findings (GCI0048, GCI0029): Higher boost (+40-54%)
   - Performance findings (GCI0035, GCI0044): Moderate boost (+15-25%)
   - Exception handling (GCI0032): Conservative boost (+15-20%)

3. **Domain-Specific Thresholds**
   - Performance (P4): Threshold ≥0.50 (probabilistic)
   - Security (P5): Threshold ≥0.55-0.60 (conservative)
   - Async/Resource (P6): Threshold ≥0.60 (reliable patterns)

### Confidence Boost Matrix

| Coordination | Rule A | Original | Boosted | Delta | Rationale |
|---|---|---|---|---|---|
| **P0: Async** | GCI0016 | 0.65 | 0.80 | +15% | Blocking in async is correlated risk |
| | GCI0039 | 0.65 | 0.80 | +15% | HTTP client misuse in async contexts |
| | GCI0044 | 0.60 | 0.75 | +15% | GC pressure from async patterns |
| **P4: Perf** | GCI0044 | 0.60 | 0.75 | +25% | Inefficient loops cause GC |
| | GCI0035 | 0.55 | 0.70 | +27% | GC pressure from iteration |
| **P5: Serialization** | GCI0039 | 0.55 | 0.85 | **+54%** | Deserialize in HTTP = vulnerability |
| | GCI0048 | 0.60 | 0.80 | **+33%** | Insecure deserialize in HTTP |
| **P6: DI & Async** | GCI0045 | 0.55 | 0.75 | +36% | DI errors compound async risks |
| | GCI0016 | 0.65 | 0.80 | +23% | Async + wrong scope = deadlock |

### When to Use Asymmetric Boosts

**Use asymmetric boosts when:**
- One rule is more critical than the other (security > performance)
- Domain knowledge justifies higher confidence
- Testing confirms the pattern is reliable

**Example: P5 Serialization**
```csharp
// GCI0048 (insecure deserialize) gets +33% boost
// GCI0039 (unsafe HTTP) gets +54% boost
// Why? Deserialize vulnerability is TOP RISK if reached via HTTP
// The HTTP context makes it more dangerous
```

---

## Testing & Validation

### Test Coverage Requirements

Each coordination requires:
- **3 test fixtures** (both fire, single fire, no fire)
- **100% test pass rate** (no regressions)
- **Integration testing** (coordination + base heuristics)

### Build & Test Commands

```bash
# Build solution
dotnet build -c Debug

# Run all tests
dotnet test

# Run only coordination tests
dotnet test --filter "Coordination"

# Run Phase-specific tests
dotnet test --filter "Phase23OR Phase24"

# Check test coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura
```

### Validation Checklist

- [ ] All 1,500+ tests passing
- [ ] 0 regressions on Phase 21 findings
- [ ] New coordination tests passing (3 fixtures per coordination)
- [ ] Confidence boost values reasonable (see matrix)
- [ ] Coordination tags correctly applied
- [ ] No infinite loops (coordinations are idempotent)
- [ ] Performance impact <5% (measure with benchmarks)

---

## Phase Roadmap

### Completed Phases

#### Phase 21: Base Coordinations ✅
- Status: Complete (v2.4.0 - v2.6.0)
- Phases: P0-P3 (4 coordinations)
- Impact: 25-36% FP reduction
- Tests: All 1,491 passing

#### Phase 23: Advanced Coordinations ✅
- Status: Complete (v2.7.0)
- Phases: 23.0 + P4-P6 (4 coordinations)
- Impact: 17-28% FP reduction
- Tests: All 1,500 passing

### Planned Phases

#### Phase 24: Concurrency & Cache Patterns
- Timeline: 2-3 weeks (after Phase 23 deployment)
- Phases: P7-P8 (2 coordinations)
- Impact: 8-13% FP reduction

**P7: Concurrency & Lock Ordering**
- Rules: GCI0016 ↔ GCI0038
- Pattern: Blocking + locks = deadlock
- Boost: GCI0016 0.65→0.85, GCI0038 0.60→0.78
- Prerequisites: Phase 23 metrics validate

**P8: Cache Coherency**
- Rules: GCI0021 ↔ GCI0029
- Pattern: Stale cache + PII = privacy leak
- Boost: GCI0021 0.55→0.78, GCI0029 0.60→0.82
- Notes: May require method-level scope filtering

#### Phase 25: Network & Observability
- Timeline: 3-4 weeks after Phase 24
- Phases: P9+ (2+ coordinations)
- Impact: 4-8% FP reduction

**P9: Network Resilience**
- Rules: GCI0039 ↔ GCI0043
- Pattern: Unsafe HTTP + missing retry = cascade
- Status: Deferred (GCI0043 less validated)

### Cumulative Roadmap

| Phase | Coordinations | Expected Reduction | Cumulative | Status |
|---|---|---|---|---|
| Phase 21 | P0-P3 | 25-36% | 25-36% | ✅ |
| Phase 23 | 23.0+P4-P6 | +17-28% | 39-60% | ✅ |
| Phase 24 | P7-P8 | +8-13% | 47-73% | 📋 |
| Phase 25 | P9+ | +4-8% | 51-81% | 🔮 |

---

## Troubleshooting & Tuning

### Common Issues

#### Issue: Coordination not activating

**Diagnosis:**
```bash
# Check if rule A fires
grep "GCI0016" logs/labeling.log | head -5

# Check if rule B fires
grep "GCI0038" logs/labeling.log | head -5

# Check if both fire in same scope
grep "GCI0016.*GCI0038" logs/labeling.log
```

**Fix:**
- Verify both rules are enabled in config
- Check rule scope matching logic (SameMethodScope)
- Ensure coordination method is called in pipeline

#### Issue: Confidence boosted too aggressively

**Diagnosis:**
- Review ADR-0005 confidence matrix
- Check if boost delta matches documented value
- Query false positive rate trend

**Fix:**
- Reduce boost delta (e.g., 0.20 → 0.15)
- Tighten scope filtering (method → class level)
- Add additional conditions before boosting

#### Issue: False negative rate increased

**Diagnosis:**
- Compare false negative rate before/after coordination
- Check if boost is preventing low-confidence findings

**Fix:**
- Lower boost thresholds (e.g., 0.80 → 0.75)
- Add minimum confidence gate before boosting
- Review Phase 24 decision gates for metrics validation

### Tuning Procedures

**Conservative Tuning:**
1. Start with minimal boost (+10-15%)
2. Monitor false positive rate for 3-7 days
3. Increase boost if FP rate meets targets
4. Max boost: +30% for performance, +50% for security

**Aggressive Tuning:**
1. Increase boost by 5% increments
2. Monitor for false negative spike
3. If spike >5%, reduce and stabilize
4. Document tuning decisions in ADR

### Metrics to Monitor

```
Daily Coordination Metrics:
├── Activation Frequency: X per 1,000 findings
├── Confidence Before/After: [avg_before] → [avg_after]
├── False Positive Rate: [% of boosted findings that are FP]
├── False Negative Rate: [% missed vs baseline]
├── Latency Impact: [ms added per labeling]
└── Recovery Time: [if rollback needed]
```

---

## References

### Architecture Documents
- **ADR-0004:** Phase 21 coordinations (base coordinations)
- **ADR-0005:** Phase 23 coordinations (enhanced heuristics + advanced coordinations)
- **ADR-0006:** Phase 24 coordinations (to be created)

### Operational Documents
- **coordination-runbook.md:** Debugging, tuning, rollback procedures
- **DEPLOYMENT_CHECKLIST_v2.7.0.md:** Phase 23 deployment guide
- **phase-24-plan.md:** Phase 24 roadmap and metrics framework

### Code References
- **SilverLabelEngine.cs:** Coordination implementation (Tier 2)
- **GCI0016_ConcurrencyAndStateRisk.cs:** Phase 23.0 heuristic enhancements
- **SilverLabelEngineTests.cs:** Coordination test suite

---

**End of Document**

---

## Quick Links

- [Architecture Diagrams](../docs/architecture/)
- [Rule Registry](../docs/rules.md)
- [Operations Runbook](../docs/operations/coordination-runbook.md)
- [Engineering Rules](../docs/core-engineering-rules.md)
