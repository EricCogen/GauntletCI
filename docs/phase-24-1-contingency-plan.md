# Phase 24.1: Next Coordination Tier (Contingency Plan)

**Status:** 📋 STANDBY (Waiting for Gate 1 GO - May 12, 2026)  
**Prepared by:** Copilot  
**Date:** May 6, 2026  
**Trigger:** Will activate IF Phase 24.0 metrics show: 17-28% FP reduction + cumulative 39-60% achieved

---

## Overview

Phase 24.1 implements the next two coordinations once Phase 23 is validated:

- **P7:** Concurrency & Lock Ordering (3-4 days)
- **P8:** Cache Coherency & PII Exposure (3-4 days)

**Expected Benefit:** 8-13% additional FP reduction  
**Cumulative Target:** 47-73% total (from 40-50% baseline)

---

## P7: Concurrency & Lock Ordering Coordination

### Objective
Coordinate GCI0016 (async violations) with GCI0038 (lock ordering issues) to catch deadlock patterns.

### Pattern: Blocking in Async Context + Lock Ordering Violation

```csharp
// RISKY: Lock acquired in async function = potential deadlock
public async Task ProcessAsync()
{
    lock (resourceLock)  // ❌ GCI0038: lock in async context
    {
        var data = await service.GetAsync();  // ❌ GCI0016: blocking async
        ProcessData(data);
    }
}

// CORRECT: Use async-safe patterns
public async Task ProcessAsync()
{
    using (await asyncLock.AcquireAsync())
    {
        var data = await service.GetAsync();
        ProcessData(data);
    }
}
```

### Why This Matters
- **Risk Level:** 🔴 CRITICAL
- **Pattern:** Lock contention + blocking in async = deadlocks in production
- **False Positive Impact:** GCI0016 alone: ~65% baseline confidence → 85% with P7 boost (+31%)
- **Real Impact:** This pattern is rare in well-designed code but catastrophic when present

### Implementation

**File:** `src/GauntletCI.Core/Coordinations/P7_ConcurrencyCoordination.cs`

```csharp
public class P7_ConcurrencyCoordination : ICoordination
{
    public string Id => "P7";
    public string Name => "Concurrency & Lock Ordering";
    public string[] CoordinatedRules => new[] { "GCI0016", "GCI0038" };
    
    public float GetConfidenceBoost(FindingSet findings)
    {
        var gci0016 = findings.ByRule("GCI0016");
        var gci0038 = findings.ByRule("GCI0038");
        
        // Only boost if both fire in same scope
        if (gci0016.Count > 0 && gci0038.Count > 0)
        {
            // Check if they refer to same function/loop
            if (SameScopeDetected(gci0016, gci0038))
            {
                // GCI0016 gets +0.20 boost (0.65 → 0.85)
                // GCI0038 gets +0.18 boost (0.60 → 0.78)
                return 0.20f;  // Return boost for primary rule
            }
        }
        return 0f;  // No coordination
    }
}
```

### Testing Strategy
1. ✅ Unit tests: SameScopeDetected() edge cases
2. ✅ Integration: P7 fires on synthetic deadlock patterns
3. ✅ Production: Monitor false positive reduction

### Timeline
- **Day 1:** Implement P7_ConcurrencyCoordination.cs (~2 hours)
- **Day 1:** Add unit tests (~1 hour)
- **Day 2:** Integration testing (2 hours)
- **Day 3:** Production validation (1 day)

---

## P8: Cache Coherency & PII Exposure Coordination

### Objective
Coordinate GCI0021 (cache lifetime issues) with GCI0029 (PII exposure) to catch cache-based privacy leaks.

### Pattern: Stale Cache Data Containing PII

```csharp
// RISKY: Stale cache + PII = privacy leak
public User GetUser(int userId)
{
    var cached = cache.Get("user_" + userId);  // ❌ GCI0021: stale cache
    if (cached != null)
    {
        // Might be old entry with PII from previous session
        LogUserInfo(cached);  // ❌ GCI0029: logging user object
        EmailNotification(cached.Email);  // Might be old email
        return cached;
    }
    // ... fetch fresh
}

// CORRECT: Force fresh data for PII
public User GetUser(int userId)
{
    var cached = cache.Get("user_" + userId);
    if (cached != null && DateTime.UtcNow - cached.CachedAt < 5.Minutes())
    {
        LogAnonymized(cached);  // Hash PII before logging
        return cached;
    }
    var fresh = db.Users.Find(userId);
    cache.Set("user_" + userId, fresh);
    return fresh;
}
```

### Why This Matters
- **Risk Level:** 🔴 CRITICAL (Security & Privacy)
- **Pattern:** Very common in large systems; easy to miss in code review
- **False Positive Impact:** GCI0021 alone: ~55% baseline → 78% with P8 boost (+41%)
- **Real Impact:** GDPR/privacy incident if stale PII is logged/exposed

### Implementation

**File:** `src/GauntletCI.Core/Coordinations/P8_CacheCoherencyCoordination.cs`

```csharp
public class P8_CacheCoherencyCoordination : ICoordination
{
    public string Id => "P8";
    public string Name => "Cache Coherency & PII";
    public string[] CoordinatedRules => new[] { "GCI0021", "GCI0029" };
    
    public float GetConfidenceBoost(FindingSet findings)
    {
        var gci0021 = findings.ByRule("GCI0021");  // Stale cache
        var gci0029 = findings.ByRule("GCI0029");  // PII exposure
        
        // Only boost if cache retrieval followed by PII operation
        if (gci0021.Count > 0 && gci0029.Count > 0)
        {
            if (IsDataFlowRelated(gci0021, gci0029))
            {
                // GCI0021 gets +0.23 boost (0.55 → 0.78)
                // GCI0029 gets +0.22 boost (0.60 → 0.82)
                return 0.23f;
            }
        }
        return 0f;
    }
}
```

### Testing Strategy
1. ✅ Unit tests: IsDataFlowRelated() accuracy
2. ✅ Integration: P8 fires on synthetic privacy leak patterns
3. ✅ Production: Monitor for false positives on non-PII cache

### Timeline
- **Day 1:** Implement P8_CacheCoherencyCoordination.cs (~2 hours)
- **Day 1:** Add unit tests (~1 hour)
- **Day 2:** Integration testing (2 hours)
- **Day 3:** Production validation (1 day)

---

## Rollout Plan (If Gate 1 = GO)

### P7 Rollout (Concurrency - May 13-15)

**May 13 (Day 8):**
- [ ] Implement P7_ConcurrencyCoordination.cs
- [ ] Add unit tests (target: >95% coverage)
- [ ] Verify no compiler warnings

**May 14 (Day 9):**
- [ ] Integration tests (synthetic deadlock patterns)
- [ ] Code review (internal)
- [ ] Merge to staging

**May 15 (Day 10):**
- [ ] Deploy to production (canary: 5% traffic)
- [ ] Monitor for 24h
- [ ] If stable: roll out to 100%

### P8 Rollout (Cache Coherency - May 15-17)

**May 15 (Day 10):**
- [ ] Implement P8_CacheCoherencyCoordination.cs
- [ ] Add unit tests
- [ ] Verify no compiler warnings

**May 16 (Day 11):**
- [ ] Integration tests (privacy leak patterns)
- [ ] Code review
- [ ] Merge to staging

**May 17 (Day 12):**
- [ ] Deploy to production (canary: 5% traffic)
- [ ] Monitor 24h
- [ ] Roll out to 100%

### Deployment Success Criteria

**P7 Success:** 
- ✅ 5-8% FP reduction achieved
- ✅ Cumulative with P4-P6: 44-68% total
- ✅ No false negatives (deadlock cases still caught)
- ✅ <0.5% increase in coordination latency

**P8 Success:**
- ✅ 3-5% FP reduction achieved
- ✅ Cumulative: 47-73% total (goal exceeded!)
- ✅ No privacy leak cases missed
- ✅ System stable (>99.5% uptime)

---

## Pre-Gate-1 Checklist (NOW - Before May 12)

### Code Readiness
- ✅ GCI0038 rule exists and has working implementation
- ✅ GCI0021 rule exists and has working implementation
- ✅ Coordination framework stable (P4-P6 working)
- ✅ Test framework ready for new coordinations

### Documentation Readiness
- ✅ ADR template prepared for P7 & P8
- ✅ Runbook template ready (extends coordination-runbook.md)
- ✅ Release notes template prepared
- ✅ Architecture diagrams ready

### Team Readiness
- ✅ Code review process defined
- ✅ Deployment checklist prepared
- ✅ On-call rotation informed
- ✅ Rollback plan documented

### Monitoring Readiness
- ✅ Dashboards created for P7 metrics
- ✅ Dashboards created for P8 metrics
- ✅ Alert thresholds defined
- ✅ Log aggregation tested

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| P7 detects true deadlocks but produces noise | Medium | Medium | Careful tuning of confidence boost; thorough integration testing |
| P8 produces false positives on non-PII cache | Medium | High | White-list known non-PII cache patterns; test on real codebase |
| Deployment causes performance regression | Low | High | Canary deploy with latency monitoring; rollback plan ready |
| Gate 1 NO-GO (metrics don't validate) | Medium | High | Skip Phase 24.1; focus on Phase 23 tuning |

---

## Success Metrics

### Phase 24.1 Complete (Scheduled May 17 if GO)

| Metric | Target | Success Threshold |
|--------|--------|-------------------|
| **Cumulative FP Reduction** | 47-73% | >45% |
| **P7 Individual Reduction** | 5-8% | >3% |
| **P8 Individual Reduction** | 3-5% | >2% |
| **System Uptime** | >99.5% | >99.0% |
| **Coordination Latency** | <2ms avg | <3ms avg |
| **False Negative Delta** | <5% increase | <10% increase |

---

## Decision Tree

```
┌─────────────────────────────────────────────┐
│ Gate 1 Decision (May 12, 09:00 UTC)         │
│ Criteria: 17-28% FP reduction achieved?     │
└─────────────────────────────────────────────┘
         │                          │
         YES ✅                    NO ❌
         │                          │
    ┌────▼────────┐          ┌─────▼──────────┐
    │ Proceed to  │          │ Option A: Tune │
    │ Phase 24.1  │          │ Phase 23 & Re- │
    │             │          │ run metrics    │
    │ P7 + P8     │          │ (7 more days)  │
    │ (May 13-17) │          │                │
    └─────────────┘          │ Option B: Skip │
         │                   │ Phase 24 (stay │
         │                   │ at 39-60%)     │
         ▼                   └────────────────┘
    ┌─────────────────────────────────────┐
    │ Phase 24.1 Complete (May 17)        │
    │ Cumulative: 47-73% FP reduction     │
    │ Platform ready for P9 (future)      │
    └─────────────────────────────────────┘
```

---

## Files to Create (On Gate 1 GO)

1. `src/GauntletCI.Core/Coordinations/P7_ConcurrencyCoordination.cs` (~80 lines)
2. `tests/GauntletCI.Core.Tests/P7_ConcurrencyCoordinationTests.cs` (~120 lines)
3. `src/GauntletCI.Core/Coordinations/P8_CacheCoherencyCoordination.cs` (~80 lines)
4. `tests/GauntletCI.Core.Tests/P8_CacheCoherencyCoordinationTests.cs` (~120 lines)
5. `docs/ADR-0008-P7-Concurrency-Coordination.md` (~40 lines)
6. `docs/ADR-0009-P8-Cache-Coherency-Coordination.md` (~40 lines)
7. `RELEASE_NOTES_v2.8.0.md` (~60 lines)

---

## External Dependencies

None. All code is self-contained:
- GCI0016, GCI0038, GCI0021, GCI0029 rules exist
- Coordination framework proven in P4-P6
- Test infrastructure ready

---

## Estimated Effort

- **P7 Implementation:** 3-4 days (2-3 with overlap)
- **P8 Implementation:** 3-4 days (2-3 with overlap)
- **Total:** 5-7 days for both
- **Parallel possible:** Yes, P7 and P8 can run in parallel after Day 1

---

## Final Gate 1 Condition

**READY FOR GO IF:**
- ✅ Phase 23 FP reduction: 17-28% achieved
- ✅ Cumulative FP reduction: 39-60%+ achieved
- ✅ System stability: >99%
- ✅ False negative rate: <5% increase
- ✅ All coordinations firing as expected

**PROCEED TO PHASE 24.1** → Schedule May 13, 09:00 UTC start

---

**Status:** 📋 CONTINGENCY PLAN PREPARED  
**Activation:** Conditional on Gate 1 GO (May 12)  
**Last Updated:** May 6, 2026, 01:21 UTC  
**Next Review:** May 12, 09:00 UTC (after Gate 1 decision)
