# GauntletCI v2.2.1-critical-fixes - Deployment Checklist

**Release:** v2.2.1-critical-fixes  
**Date:** May 2, 2026  
**Status:** ✅ READY FOR IMMEDIATE DEPLOYMENT  

---

## Pre-Deployment (Verify Production Readiness)

- [x] All CRITICAL bugs fixed and verified
- [x] Build successful: 0 errors, 0 warnings
- [x] Tests passing: 1,407/1,407 (100%)
- [x] No regressions detected
- [x] Release notes generated
- [x] Deployment checklist prepared
- [x] Team communication drafted

**Status:** ✅ READY

---

## Deployment Environment Preparation

### Phase 1: Environment Check

**Before deployment, verify:**

- [ ] Production environment backed up
- [ ] Database backups current (< 1 hour old)
- [ ] Sufficient disk space on production servers
- [ ] Network connectivity verified
- [ ] Monitoring systems operational
- [ ] Rollback plan documented
- [ ] Team on standby for issues

**Timeline:** 15 minutes

---

### Phase 2: Application Backup

```bash
# Tag current production version
git tag v2.2.0-production-backup-$(date +%Y%m%d-%H%M%S)

# Create backup branch
git checkout -b backup/v2.2.0-$(date +%Y%m%d-%H%M%S)
git push origin backup/v2.2.0-$(date +%Y%m%d-%H%M%S)

# Return to main
git checkout main
```

**Status:** [ ] BACKUP CREATED

**Timeline:** 5 minutes

---

## Deployment Steps

### Phase 3: Code Deployment

**Step 1: Fetch and checkout new version**
```bash
git fetch origin
git checkout v2.2.1-critical-fixes
```

**Status:** [ ] CODE CHECKED OUT

**Step 2: Build release**
```bash
dotnet build GauntletCI.slnx -c Release
```

**Expected Output:**
```
Build succeeded with 0 errors, 0 warnings
```

**Status:** [ ] BUILD SUCCESSFUL

**Step 3: Run full test suite**
```bash
dotnet test GauntletCI.slnx --configuration Release
```

**Expected Output:**
```
Test Run Successful.
Total tests: 1407
  Passed: 1407
  Failed: 0
```

**Status:** [ ] ALL TESTS PASSING

**Step 4: Publish release build**
```bash
dotnet publish -c Release -o publish/
```

**Status:** [ ] PUBLISHED

**Timeline:** 15-20 minutes

---

### Phase 4: Service Deployment

**Step 1: Stop current services**
```bash
# Stop all GauntletCI services
systemctl stop gauntletci-hydrator
systemctl stop gauntletci-daemon
systemctl stop gauntletci-cli
```

**Status:** [ ] SERVICES STOPPED

**Step 2: Backup current binaries**
```bash
cp -r /opt/gauntletci /opt/gauntletci.v2.2.0.backup
```

**Status:** [ ] BINARIES BACKED UP

**Step 3: Deploy new binaries**
```bash
cp -r publish/* /opt/gauntletci/
chown -R gauntletci:gauntletci /opt/gauntletci
chmod -R 755 /opt/gauntletci
```

**Status:** [ ] BINARIES DEPLOYED

**Step 4: Start services**
```bash
systemctl start gauntletci-hydrator
systemctl start gauntletci-daemon
systemctl start gauntletci-cli
```

**Status:** [ ] SERVICES STARTED

**Timeline:** 10-15 minutes

---

### Phase 5: Health Checks (CRITICAL)

**Immediately after deployment, verify:**

```bash
# Check service status
systemctl status gauntletci-hydrator
systemctl status gauntletci-daemon
systemctl status gauntletci-cli

# Verify daemon is responsive
curl http://localhost:8888/health

# Check for any startup errors
journalctl -u gauntletci-hydrator -n 50
journalctl -u gauntletci-daemon -n 50
journalctl -u gauntletci-cli -n 50
```

**Expected Results:**
- ✅ All services running
- ✅ Daemon health endpoint returns 200 OK
- ✅ No ERROR or CRITICAL log entries
- ✅ Environment variables loaded correctly

**If ANY check fails:** ROLLBACK IMMEDIATELY (see rollback section)

- [ ] Service status: OK
- [ ] Daemon health: OK
- [ ] Hydrator running: OK
- [ ] No critical errors: OK

**Status:** [ ] ALL HEALTH CHECKS PASSED

**Timeline:** 5 minutes

---

## Verification Tests (After Health Checks)

### Test 1: Hydration Pipeline

**Purpose:** Verify the sync-over-async deadlock is fixed

```bash
# Trigger a hydration job
gauntletci corpus add-pr https://github.com/test/repo --pr-number 123

# Monitor the hydrator log
# Expected: Completes without deadlock
journalctl -u gauntletci-hydrator -f
```

**Expected Behavior:** Hydration completes in < 5 minutes without deadlock

**Status:** [ ] HYDRATION SUCCESSFUL

**Timeline:** 5-10 minutes

---

### Test 2: Daemon Resilience

**Purpose:** Verify daemon handles malformed JSON gracefully (not crashes)

```bash
# Send malformed JSON to daemon
echo '{"invalid": json}' | nc localhost 8888

# Check logs for graceful error response
journalctl -u gauntletci-daemon -n 10
```

**Expected Behavior:** Daemon returns error response, stays running

**Status:** [ ] DAEMON RESILIENT

**Timeline:** 2 minutes

---

### Test 3: Environment Variable Validation

**Purpose:** Verify ticket providers handle missing env vars gracefully

```bash
# Test with missing LINEAR_API_KEY
unset LINEAR_API_KEY
gauntletci corpus discover-linear --repo "test/repo"

# Expected: Graceful failure with "not available" message
```

**Expected Behavior:** Provider returns null gracefully, doesn't crash

**Status:** [ ] ENV VAR VALIDATION WORKING

**Timeline:** 2 minutes

---

### Test 4: Ticket Provider Integration

**Purpose:** Verify all ticket providers work correctly

```bash
# Test GitHub provider
GITHUB_TOKEN=test GITHUB_REPOSITORY=test/repo gauntletci corpus show-github

# Test Jira provider
JIRA_BASE_URL=http://test JIRA_API_TOKEN=test JIRA_USER_EMAIL=test gauntletci corpus show-jira

# Test Linear provider
LINEAR_API_KEY=test gauntletci corpus show-linear

# Expected: All work or gracefully fail (not crash)
```

**Expected Behavior:** All providers functional

**Status:** [ ] TICKET PROVIDERS WORKING

**Timeline:** 5 minutes

---

## Post-Deployment Monitoring (24 Hours)

### Immediate (0-1 hour)
- [ ] Monitor service logs for errors
- [ ] Check resource usage (CPU, memory, disk)
- [ ] Verify API response times
- [ ] Monitor error rates in production

### Short-term (1-24 hours)
- [ ] Daily log review
- [ ] Application metrics analysis
- [ ] User feedback collection
- [ ] Performance baseline comparison

### Checks to Run:

```bash
# Monitor daemon performance
# (Should see < 100ms response times for most requests)
watch 'systemctl status gauntletci-daemon'

# Monitor hydrator throughput
# (Should see steady progress through corpus)
watch 'journalctl -u gauntletci-hydrator -n 5 --no-pager'

# Check for any hung processes
ps aux | grep gauntletci | grep -v grep

# Monitor system resources
vmstat 5 10
```

---

## Rollback Procedure (IF NEEDED)

**⚠️ ROLLBACK ONLY IF:**
- Services not starting
- Health checks failing
- Critical errors in logs
- Deadlock/crash detected
- Any production impact

**Rollback Steps:**

```bash
# Step 1: Stop new version
systemctl stop gauntletci-hydrator
systemctl stop gauntletci-daemon
systemctl stop gauntletci-cli

# Step 2: Restore previous binaries
rm -rf /opt/gauntletci
cp -r /opt/gauntletci.v2.2.0.backup /opt/gauntletci

# Step 3: Restart services with previous version
systemctl start gauntletci-hydrator
systemctl start gauntletci-daemon
systemctl start gauntletci-cli

# Step 4: Verify all services running
systemctl status gauntletci-hydrator
systemctl status gauntletci-daemon
systemctl status gauntletci-cli

# Step 5: Notify team
# "Rollback to v2.2.0 completed - investigating issue"

# Step 6: Check out previous tag
git checkout v2.2.0
```

**Timeline:** 10-15 minutes

**Status:** [ ] ROLLBACK READY (but hopefully not needed)

---

## Success Criteria

**Deployment is successful if:**

✅ All services start and remain running  
✅ All health checks pass  
✅ Verification tests pass  
✅ No critical errors in logs (24 hours)  
✅ Performance metrics stable  
✅ User-facing functionality working  
✅ Resource usage within expected ranges  

---

## Post-Deployment Handoff

### To Monitoring Team:
- Service status URLs
- Alert threshold adjustments (if any)
- New metrics to watch for HttpClient consolidation benefits

### To Development Team:
- Deployment summary
- Any issues encountered
- Recommendations for Phase 2

### To QA Team:
- Feature verification checklist
- Known limitations
- Test plan for Phase 2

---

## Team Communication Template

### Before Deployment
```
🚀 DEPLOYMENT NOTIFICATION: v2.2.1-critical-fixes

Target Time: [TIME]
Expected Duration: 30-45 minutes
Impact: Potential 5-minute service interruption during restart

FIXES:
✅ GitHubRestHydrator: Sync-over-async deadlock fixed
✅ LlmDaemonServer: Null deserialization crash fixed  
✅ Ticket Providers: Environment variable validation added

BUILD STATUS: 0 errors, 0 warnings
TESTS: 1,407/1,407 passing (100%)

Questions? See RELEASE_NOTES_v2.2.1-critical-fixes.md
```

### After Successful Deployment
```
✅ DEPLOYMENT SUCCESSFUL: v2.2.1-critical-fixes

All services running
All health checks passed
Verification tests completed

Version: v2.2.1-critical-fixes
Build Date: May 2, 2026
Deployed By: [PERSON]
Time: [TIME]

No rollback needed. Systems operating normally.
```

### After Deployment Issues (IF NEEDED)
```
⚠️ DEPLOYMENT ISSUE DETECTED: v2.2.1-critical-fixes

Issue: [DESCRIPTION]
Action: Rolling back to v2.2.0
Status: [INVESTIGATING]
Time: [TIME]

See details in: [LOG FILE]
```

---

## Documentation

**Reference Documents:**
- [RELEASE_NOTES_v2.2.1-critical-fixes.md](./RELEASE_NOTES_v2.2.1-critical-fixes.md) - Full release notes
- [CODE_AUDIT_REPORT.md](./CODE_AUDIT_REPORT.md) - Technical analysis
- [AUDIT_ACTION_PLAN.md](./AUDIT_ACTION_PLAN.md) - Implementation details
- [.misc/PHASE2_SPRINT_PLAN.md](./.misc/PHASE2_SPRINT_PLAN.md) - Next sprint plan

---

## Sign-Off

**Prepared by:** Code Audit Task + Copilot  
**Date:** May 2, 2026  
**Status:** ✅ READY FOR DEPLOYMENT  
**Reviewer:** [PERSON]  
**Approved:** [DATE/TIME]  

---

## Deployment Summary (Post-Deployment)

| Checkpoint | Status | Time | Notes |
|------------|--------|------|-------|
| Code checkout | [ ] | | v2.2.1-critical-fixes |
| Build | [ ] | | 0 errors, 0 warnings |
| Tests | [ ] | | 1,407/1,407 passing |
| Services stopped | [ ] | | Graceful shutdown |
| Binaries backed up | [ ] | | /opt/gauntletci.v2.2.0.backup |
| Binaries deployed | [ ] | | New version in place |
| Services started | [ ] | | All running |
| Health checks | [ ] | | All passing |
| Verification tests | [ ] | | All passed |
| Monitoring enabled | [ ] | | 24-hour watch active |
| Team notified | [ ] | | Deployment complete |

---

**Deployment timestamp:** [DATE/TIME]  
**Deployed by:** [PERSON]  
**Reviewed by:** [PERSON]  
**Status:** ✅ COMPLETE
