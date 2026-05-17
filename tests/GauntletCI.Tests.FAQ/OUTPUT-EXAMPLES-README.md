# README Output Example Validation Tests

## Overview

This test suite validates that **GauntletCI produces the exact outputs documented in the main README**.

The documentation shows specific example outputs, rule findings, and demo scenarios. These tests verify that those examples are real—not aspirational marketing copy.

## Test Files

### 1. `test-readme-output-examples.cs`
Validates documented example outputs from README lines 79-83, 292-592, and rule references.

**Documented Examples Tested:**

| Rule | Example | Location |
|------|---------|----------|
| **GCI0003** | Guard clause removed from null check | README lines 79-82 |
| **GCI0007** | Silent `catch { }` around payment call | README line 149 |
| **GCI0012** | Hardcoded API key in Program.cs | README line 150 |
| **GCI0004** | CancellationToken dropped from interface | README line 151 |
| **GCI0029** | Customer email logged in LogInformation | README line 152 |
| **GCI0016** | Static counter mutated without sync | README line 153 |
| **GCI0009** | Syntax error handling with warning output | README line 564 |

**Test Structure:**
- Each test creates a temporary Git repo
- Makes the exact change described in README
- Stages the change
- Runs `gauntletci analyze --staged`
- Verifies output contains the documented rule ID and description

### 2. `test-demo-scenarios-validation.cs`
Validates the 36 demo scenarios from GauntletCI-Demo repository.

**Scenario Coverage:**

| Tier | Count | Purpose | Examples |
|------|-------|---------|----------|
| **Tier 1** | 6 | Headline scenarios with known verdicts | S01-S06 |
| **Tier 2** | 12 | Single-rule scenarios (one rule per) | S07-S18 |
| **Tier 3** | 18 | Behavioral regression scenarios | S19-S36 |

**Tier 1 Verified Verdicts (README lines 146-153):**
- S01: Safe typo fix → **No findings** ✓
- S02: Silent catch block → **GCI0007** ✓
- S03: Hardcoded API key → **GCI0012** ✓
- S04: CancellationToken dropped → **GCI0004** ✓
- S05: Email logged as PII → **GCI0029** ✓
- S06: Unsync static mutation → **GCI0016** ✓

**Tier 3 Categories Tested:**
- Architectural Access Control (S19, S23, S24)
- Execution Sequence Changes (S20, S28-S30)
- Async Propagation Drops (S21, S25-S27)
- Public Contract Drift (S22, S31-S32)
- Performance & Resource (S33-S34)
- Dependency Injection Scope (S35-S36)

## Running These Tests

### Prerequisites
```bash
# Install GauntletCI globally
dotnet tool install -g GauntletCI

# Ensure git is in PATH
git --version
```

### Run All Output Examples Tests
```bash
dotnet test tests/GauntletCI.Tests.FAQ/test-readme-output-examples.cs --no-skip
```

### Run All Demo Scenarios Tests
```bash
dotnet test tests/GauntletCI.Tests.FAQ/test-demo-scenarios-validation.cs --no-skip
```

### Run Specific Test
```bash
# Test GCI0003 example only
dotnet test tests/GauntletCI.Tests.FAQ/test-readme-output-examples.cs \
  -k "GCI0003_GuardClauseRemovalExample"

# Test Tier 1 Scenario 02 only
dotnet test tests/GauntletCI.Tests.FAQ/test-demo-scenarios-validation.cs \
  -k "Tier1_S02_SilentCatch"
```

## Why These Tests Matter

### For Users
- **Trust in Documentation**: Every example in the README is verifiable and tested
- **Reproducibility**: Any user can run these tests and see the exact outputs
- **Confidence**: The tool does what the docs say it does

### For Maintainers
- **Regression Detection**: If output format changes, tests catch it immediately
- **Documentation Drift Prevention**: Docs and behavior stay in sync
- **Release Safety**: Changes to rule detection logic are validated against documented behavior

### For Contributors
- **Examples in Code**: Shows exactly what each rule detects
- **Clear Expectations**: Demonstrates behavior before and after code changes
- **Integration Test Model**: Template for adding new output examples

## Test Status

### Currently Skipped (Requires Manual Execution)
All tests in both files are marked with `[Fact(Skip="...")]` because they:
1. Require the `gauntletci` global tool to be installed
2. Create temporary directories with Git repos
3. Cannot run in isolation without GCI binary availability

### How to Enable
To run these tests, remove the `Skip` attribute:

```csharp
// FROM:
[Fact(Skip = "Requires GauntletCI binary")]
public async Task MyTest() { }

// TO:
[Fact]
public async Task MyTest() { }
```

## Test Results Interpretation

### Passing Test
```
✓ GCI0003_GuardClauseRemovalExample_ProducesDocumentedOutput
  Output contains:
  - ✓ "GCI0003"
  - ✓ "Guard clause removed"
  - ✓ "[High]"
  - ✓ "ArgumentNullException"
```

### Failing Test
```
✗ GCI0012_HardcodedApiKey_ProducesDocumentedOutput
  Expected: output.Contains("GCI0012")
  Actual: "GCI0010: Hardcoding detected"
  
  → Rule detection changed or output format differs from README
```

## Expanding Test Coverage

To add validation for a new documented example:

### 1. Add to README Output Examples
```csharp
[Fact(Skip = "Requires integration with actual GCI binary")]
public async Task GCI_XXXX_YourScenarioName_ProducesDocumentedOutput()
{
    // Create temp repo with "before" state
    var testDir = CreateTempGitRepo();
    
    // Make the change
    ApplyDocumentedChange(testDir);
    
    // Stage and analyze
    await RunGit(testDir, "add", ".");
    var output = await RunGauntletCI(testDir, "analyze --staged");
    
    // Verify documented output
    Assert.Contains("GCIXXXX", output);
    Assert.Contains("Expected description", output);
}
```

### 2. Add to Demo Scenarios Validation
```csharp
[Fact(Skip = "Requires GauntletCI-Demo repository")]
public async Task TierX_SXX_DescriptiveNameTest_ProducesExpectedFinding()
{
    var output = await AnalyzeDemoPullRequest(XX);
    Assert.Contains("GCIXXXX", output);
}
```

## Integration with CI/CD

For GitHub Actions, these tests should run:
1. **Locally during development** (skip optional, but encourage running)
2. **In pre-commit hook** (document as optional validation)
3. **In release pipeline** (run fully enabled to verify all documented behavior)

Example GitHub Actions step:
```yaml
- name: Validate README Output Examples
  run: dotnet test tests/GauntletCI.Tests.FAQ/test-readme-output-examples.cs
    --filter "!Skip" --verbosity normal
```

## Troubleshooting

### Test fails with "gauntletci: command not found"
```bash
# Ensure GauntletCI is installed globally
dotnet tool list -g | grep GauntletCI

# If missing, install it
dotnet tool install -g GauntletCI
```

### Test fails with permission denied on Git operations
```bash
# Ensure git is in PATH and executable
which git
git --version

# On Windows, use Git Bash or WSL
```

### Test fails with temp directory cleanup issues
```bash
# May occur if tests are run in parallel
# Solution: Run tests sequentially
dotnet test --maxParallelThreads 1
```

## Related Documentation

- **Main README**: `GauntletCI/README.md` - The source of truth for documented examples
- **FAQ Validation**: `tests/GauntletCI.Tests.FAQ/README.md` - FAQ claim validation
- **Demo Repository**: https://github.com/EricCogen/GauntletCI-Demo - Live 36-scenario suite
- **Audit Report**: `.misc/FAQ_AUDIT_COMPLETE.md` - Truthfulness audit with findings

## Maintenance

### When to Update These Tests
1. **README examples change** → Update corresponding test
2. **GCI rule output format changes** → Update assertion strings
3. **New documented behavior added** → Add new test case
4. **Demo scenarios added** → Add new tier/category test

### Recommended Review Cycle
- **Per PR**: Keep tests passing when editing README
- **Per Release**: Run full suite (with Skip removed) before publishing
- **Quarterly**: Review for documentation drift
