# Phase 23: Next Coordination Tier & Rule Improvements

**Purpose:** Plan Phase 23 roadmap after Phase 21 metrics validation  
**Status:** PLANNING  
**Approach:** Hybrid (Heuristic improvements + New coordinations)

---

## Decision: Hybrid Approach Recommended

After Phase 21 (4 coordinations, 25-36% FP reduction target), Phase 23 combines:

1. **Quick win:** GCI0016 heuristic improvements (2-3 days) — 5-8% additional FP reduction
2. **Scaling:** Three new coordinations P4-P6 (9-12 days) — 9-16% additional FP reduction
3. **Documentation:** ADR-0005 + runbook updates (1-2 days)

**Total effort:** 12-17 days  
**Expected outcome:** Additional 14-24% FP reduction (Phase 21 + 23 combined: 39-60% total)

---

## Phase 23.0: GCI0016 Heuristic Improvements

### Objective
Reduce false positives on GCI0016 (async violations) by improving pattern detection and scope filtering

### Problem Statement

GCI0016 baseline FP rate:
- **Alone:** 35-40% FP
- **After P0 boost:** 15-20% FP (improved, but still high)

**Root causes of remaining FPs:**
1. Blocking calls on internal threads (not in async context)
2. Fire-and-forget task patterns (intentional, not a violation)
3. Blocking calls in sync wrapper methods (legitimate pattern)
4. Configuration code that blocks at startup (acceptable)

### Solution: Enhanced Pattern Detection

#### New Keywords to Detect
```csharp
// Currently detected:
"async", "blocking", "deadlock", "configureawait", ".result", ".wait()"

// Add async domain patterns:
"ConfigureAwait(false)",     // Explicit async best practice
"Task.Run",                   // Explicit thread pool usage
"ThreadPool.QueueUserWorkItem", // Thread pool delegation
"fire-and-forget",            // Intentional async pattern
"intentional",                // Developer intent marker
"startup code",               // Config/init context
"batch processing"            // Background work
```

#### Scope Filtering
```csharp
// Distinguish context:
// ❌ Violation: Blocking call in async method context
public async Task ProcessAsync()
{
    var result = longRunningOp.Result;  // ← GCI0016 violation
}

// ✅ Acceptable: Blocking call in sync context
public void ConfigureAsync()
{
    Task.WaitAll(initTasks);  // ← Acceptable (startup), not violation
}

// ✅ Acceptable: Fire-and-forget pattern
_ = ProcessInBackground();  // ← Intentional, not violation
Task.Run(() => Work());     // ← Explicit delegation
```

### Implementation Details

**File:** `src/GauntletCI.Core/Rules/Patterns/AsyncPatterns.cs`

Add method:
```csharp
private static bool IsLegitimateAsyncPattern(string context, string comment)
{
    // Legitimate patterns that should NOT trigger GCI0016
    var legitimatePatterns = new[]
    {
        "fire-and-forget",
        "ConfigureAwait(false)",
        "intentional",
        "startup",
        "initialization",
        "batch",
    };
    
    return legitimatePatterns.Any(p => comment.Contains(p, StringComparison.OrdinalIgnoreCase));
}
```

### Test Coverage

New tests in `SilverLabelEngineTests.cs`:
```csharp
[Test]
public void GCI0016_LegitimateFireAndForget_NoViolation()
{
    // Comment: "intentional fire-and-forget"
    // Code: _ = ProcessAsync();
    // Expected: GCI0016 NOT triggered
}

[Test]
public void GCI0016_StartupBlocking_NoViolation()
{
    // Comment: "startup config - blocking is OK here"
    // Code: Task.WaitAll(initTasks);
    // Expected: GCI0016 NOT triggered
}

[Test]
public void GCI0016_ActualViolation_StillDetected()
{
    // Comment: none
    // Code: var x = asyncOp.Result;  (in async method)
    // Expected: GCI0016 triggered
}
```

### Expected Outcome
- GCI0016 FP rate: 35-40% → 10-15% (additional 25-30% reduction)
- Combined with P0 boost: 20% → 5-10% overall
- Effort: 2-3 days

---

## Phase 23.1-23.3: New Coordinations (P4-P6)

### Phase 23.1: P4 - Performance & Concurrency Coordination

#### Rules: GCI0044 ↔ GCI0035 (Memory & GC Pressure)

**Pattern 1: GC Pressure + Memory Allocation**
```csharp
// Scenario: Unmanaged GC pressure + excessive allocation → performance cliff
void BadPerformance()
{
    var blocked = GetAsyncResult().Result;     // GCI0044: GC pressure from blocking
    for (int i = 0; i < 1000000; i++)
        list.Add(new Obj());                    // GCI0035: excessive allocation
    // Result: Gen2 collections stall, memory pressure spike
}
```

**Coordination Logic:**
```csharp
if (GCI0044?.Confidence >= 0.50 && GCI0035?.Confidence >= 0.50)
{
    boost GCI0044 from 0.60 → 0.78 (+30%)
    boost GCI0035 from 0.65 → 0.85 (+31%)
}
```

**Expected impact:** 3-5% FP reduction  
**Confidence thresholds:** Both ≥ 0.50  
**Complexity:** Medium  
**Effort:** 3-4 days

---

### Phase 23.2: P5 - Serialization Safety Coordination

#### Rules: GCI0039 ↔ GCI0048 (Unsafe Clients + Insecure Serialization)

**Pattern 1: Direct HttpClient + Unsafe Deserialization**
```csharp
// Scenario: Unsafe deserialization + unvalidated input → RCE
void UnsafeDeserialization()
{
    var client = new HttpClient();              // GCI0039: unsafe client
    var response = client.GetAsync(url).Result;
    var obj = JsonConvert.DeserializeObject<T>(
        response.Content.ReadAsStringAsync().Result,
        new JsonSerializerSettings { TypeNameHandling = Auto } // GCI0048: unsafe
    );
}
```

**Coordination Logic:**
```csharp
if (GCI0039?.Confidence >= 0.55 && GCI0048?.Confidence >= 0.60)
{
    boost GCI0039 from 0.70 → 0.90 (+29%)
    boost GCI0048 from 0.65 → 0.92 (+42%)
}
```

**Expected impact:** 4-7% FP reduction  
**Confidence thresholds:** GCI0039 ≥ 0.55, GCI0048 ≥ 0.60 (higher due to both are typically high-confidence)  
**Complexity:** High (cross-layer)  
**Effort:** 3-4 days

---

### Phase 23.3: P6 - Dependency Injection Coordination

#### Rules: GCI0045 ↔ GCI0016 (Service Locator + Async Violations in DI)

**Pattern 1: Service Locator + Async Scope Issues**
```csharp
// Scenario: Service locator + async scope mismatch → deadlock
void DIDeadlock()
{
    var service = ServiceLocator.Get<AsyncService>();  // GCI0045: locator
    var result = service.GetDataAsync().Result;         // GCI0016: blocking async
    // Result: Scope mismatch causes deadlock in DI container
}
```

**Coordination Logic:**
```csharp
if (GCI0045?.Confidence >= 0.60 && GCI0016?.Confidence >= 0.55)
{
    boost GCI0045 from 0.75 → 0.88 (+17%)
    boost GCI0016 from 0.70 → 0.85 (+21%)
}
```

**Expected impact:** 2-4% FP reduction  
**Confidence thresholds:** GCI0045 ≥ 0.60, GCI0016 ≥ 0.55 (both moderate-high)  
**Complexity:** Medium  
**Effort:** 3-4 days

---

## Phase 23.4: Documentation & Validation

### ADR-0005: Phase 23 Coordination Architecture

Document in `docs/architecture/adr-0005-phase-23-coordinations.md`:
- Why P4-P6 patterns (performance, security, DI)
- Design decisions (confidence thresholds, scope detection)
- Implementation patterns (reuse from P0-P3)
- Test fixtures and coverage

### Runbook Updates

Expand `docs/operations/coordination-runbook.md`:
- Add P4-P6 debugging examples
- Tuning guidance for new patterns
- New activation frequency baselines
- Rollback procedures for P4-P6

### Validation Checklist

```
Code Quality:
  [ ] 0 errors, 0 warnings
  [ ] 1,520+ tests (1,494 existing + 6 new + 20 for GCI0016 improvements)
  [ ] 0 regressions
  [ ] Coordination latency < 2ms per 1000 findings

Documentation:
  [ ] ADR-0005 complete with P4-P6 patterns
  [ ] Runbook updated with new examples
  [ ] Release notes prepared (v2.8.0)
  [ ] Operations team trained

Performance:
  [ ] GCI0016 heuristics improve accuracy by 25-30%
  [ ] P4-P6 activation frequencies match baselines
  [ ] No latency regression on existing pipeline

Deployment:
  [ ] Production canary pass (10% traffic)
  [ ] FP metrics trending toward target
  [ ] Zero production exceptions
  [ ] Ready for full rollout
```

---

## Phase 23 Success Criteria

| Criterion | Target | Verification |
|-----------|--------|---|
| **Test coverage** | 1,520+ tests (100% pass) | `dotnet test` output |
| **Code quality** | 0 errors, 0 warnings | `dotnet build` output |
| **Performance** | < 2ms latency (5 coordinations) | Benchmark tests |
| **GCI0016 improvement** | 25-30% additional FP reduction | Metrics comparison |
| **New coordinations** | 2+ fixtures per pattern, test pass | Fixture corpus validation |
| **Documentation** | ADR-0005 + runbook complete | Peer review + ops sign-off |
| **Regressions** | 0 bugs missed by coordinations | Production validation |
| **Deployment** | Canary pass, metrics aligned | Production metrics dashboard |

---

## Phase 23 Timeline & Effort

| Phase | Work | Effort | Days |
|-------|------|--------|------|
| 23.0 | GCI0016 heuristic improvements | 2-3 days | 2-3 |
| 23.1 | P4 coordination (Performance) | 3-4 days | 3-4 |
| 23.2 | P5 coordination (Serialization) | 3-4 days | 3-4 |
| 23.3 | P6 coordination (DI) | 3-4 days | 3-4 |
| 23.4 | Documentation + validation | 1-2 days | 1-2 |
| **Total** | **All phases** | **12-17 days** | **12-17** |

---

## Rollout Strategy

### Phase 23 Releases

- **v2.7.1:** Phase 23.0 (GCI0016 improvements only) — low-risk, quick validation
- **v2.8.0:** Phase 23.1-23.3 (P4-P6 coordinations) — after v2.7.1 metrics validate

### Canary & Full Rollout

1. **Canary:** v2.7.1 to 10% user traffic for 3 days
2. **Monitor:** FP metrics, error rates, activation frequencies
3. **Decision:** If metrics positive, v2.8.0 canary
4. **Full rollout:** v2.8.0 to 100% if no issues

---

## Next Steps

1. ✅ Complete Phase 21 metrics collection (2 weeks production data)
2. ✅ Validate FP reduction achieved 20-30% target
3. ✅ Confirm zero regressions in production
4. ⏳ **Start Phase 23.0** (GCI0016 improvements) — can begin in parallel with Phase 21 metrics
5. ⏳ Phase 23.1-23.3 (P4-P6) once Phase 23.0 validated
6. ⏳ Phase 23.4 (documentation) during coordinations implementation

---

## References

- **Phase 21 Metrics:** `docs/operations/phase-21-metrics.md`
- **Phase 21 Monitoring:** `docs/operations/phase-21-monitoring.md`
- **Coordination Runbook:** `docs/operations/coordination-runbook.md`
- **Phase 21 ADR:** `docs/architecture/adr-0004-phase-21-coordinations.md`
- **Implementation:** `src/GauntletCI.Core/Rules/Patterns/` and `src/GauntletCI.Corpus/Labeling/SilverLabelEngine.cs`
