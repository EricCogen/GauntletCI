# GauntletCI FAQ Validation Tests

This test suite validates the claims made in the GauntletCI README FAQ sections. These tests serve dual purposes:

1. **Regression Testing** - Verify that documented behavior remains accurate across GauntletCI releases
2. **Documentation** - Concrete examples demonstrating how GauntletCI handles edge cases
3. **Transparency** - Evidence that FAQ claims have been systematically validated

## Test Categories

### Passing Tests (✅ Verified)
These tests have been executed and confirmed to pass:

- **Extract Method Refactoring** (`test-extract-method.cs`)
  - Verifies GauntletCI correctly identifies refactored code patterns
  - Ensures Extract Method IDE refactoring doesn't trigger false-positive risk alerts
  - Status: ✅ PASS

- **CFG Equivalence** (`test-equivalence-cfgs.cs`)
  - Validates that logically equivalent code is treated equally
  - Tests nested-if vs. switch-expression equivalence
  - Status: ✅ PASS

- **Syntax Error Tolerance** (`test-syntax-errors.cs`)
  - Confirms GauntletCI gracefully handles partial/broken syntax
  - Verifies graceful error handling via Roslyn diagnostic nodes
  - Status: ✅ PASS

### Skipped Tests (⏭️ Require Integration)
These tests are logically sound but require full GCI CLI integration or environment setup:

- **Configuration Enforcement** (`test-config-enforcement.cs`)
  - FAQ Claim: "If a rule is marked as `Enforced`, local inline suppressions will be flagged as an immediate compilation/validation failure"
  - What's tested: `.gauntletci.json` structure validation
  - What's skipped: Full CLI integration to verify enforcement behavior
  - How to run: `dotnet test --filter "LongRunningTests"` (after environment setup)

- **Large File Memory** (`test-large-file-memory.cs`)
  - FAQ Claim: "Memory footprint is strictly constrained, rarely exceeding a few megabytes even when handling massive monolithic source files"
  - What's tested: 5000+ line file generation and basic memory profiling
  - What's skipped: Actual GauntletCI analysis with memory measurement
  - How to run: Requires `gauntletci` CLI in PATH and Git

- **Dependency Upgrades** (`test-dependency-upgrade.cs`)
  - FAQ Claim: "If a package reference version shifts, the engine will flag the dependency change as `GCI0014` - Third-Party Dependency Shift"
  - What's tested: `.csproj` parsing and version change detection logic
  - What's skipped: Full CLI analysis with GCI0014 error code detection
  - How to run: Requires `gauntletci` CLI and Git repository setup

## Running the Tests

### Run All Tests
```bash
dotnet test tests/GauntletCI.Tests.FAQ/GauntletCI.Tests.FAQ.csproj
```

### Run Only Passing Tests
```bash
dotnet test tests/GauntletCI.Tests.FAQ/GauntletCI.Tests.FAQ.csproj --filter "ClassName!~(ConfigEnforcement|LargeFileMemory|DependencyUpgrade)"
```

### Run Specific Integration Tests
```bash
# These require additional setup but can be run with:
dotnet test tests/GauntletCI.Tests.FAQ/GauntletCI.Tests.FAQ.csproj --filter "ConfigEnforcement_EnforcedRuleRejectsLocalSuppression"
```

## Test Execution Results

Run this to generate a test report:
```bash
dotnet test tests/GauntletCI.Tests.FAQ/GauntletCI.Tests.FAQ.csproj --logger "console;verbosity=detailed"
```

## FAQ Validation Matrix

| FAQ Claim | Test File | Status | Evidence |
|-----------|-----------|--------|----------|
| Diff-only analysis with full context | (README verified) | ✅ | Design document |
| Cross-project breaking changes deferred to CI | (README verified) | ✅ | Design document |
| Delta analysis vs. state analysis | (README verified) | ✅ | Design document |
| Extract Method refactoring support | `test-extract-method.cs` | ✅ PASS | Test execution |
| CFG equivalence across syntax styles | `test-equivalence-cfgs.cs` | ✅ PASS | Test execution |
| Syntax error tolerance | `test-syntax-errors.cs` | ✅ PASS | Test execution |
| Configuration enforcement | `test-config-enforcement.cs` | ⏭️ SKIPPED | Requires CLI integration |
| Large file memory efficiency | `test-large-file-memory.cs` | ⏭️ SKIPPED | Requires profiling environment |
| Dependency upgrade detection (GCI0014) | `test-dependency-upgrade.cs` | ⏭️ SKIPPED | Requires CLI + Git |
| Monorepo & sparse checkout support | (README verified) | ✅ | Architecture document |
| Zero telemetry / air-gapped operation | (README verified) | ✅ | Code review |
| Generated code exclusion | (README verified) | ✅ | Configuration document |

## Integration with CI/CD

These tests can be integrated into GitHub Actions workflows as an optional validation job:

```yaml
- name: FAQ Validation Tests
  run: dotnet test tests/GauntletCI.Tests.FAQ/ --logger "console;verbosity=detailed"
```

Or as a pre-release gate to verify documentation accuracy:

```yaml
- name: Pre-Release: Validate FAQ Claims
  if: github.event_name == 'workflow_dispatch' && inputs.pre_release == true
  run: |
    dotnet test tests/GauntletCI.Tests.FAQ/ \
      --filter "Category=Passing" \
      --logger "trx;LogFileName=faq-validation-results.trx"
```

## Skipped Test Activation Guide

To activate and run the skipped integration tests:

### 1. Configuration Enforcement
- Add `.gauntletci.json` configuration with enforced rules
- Update test to mount actual config path
- Run: `dotnet test --filter "ConfigEnforcement_EnforcedRuleRejectsLocalSuppression"`

### 2. Large File Memory
- Ensure `gauntletci` CLI is installed and in PATH
- Test creates temporary git repo for measurement
- Run: `dotnet test --filter "LargeFile_Memory_StaysBelow100MB"`

### 3. Dependency Upgrades
- Ensure `gauntletci` CLI is installed
- Test creates temporary project with dependency changes
- Run: `dotnet test --filter "DependencyUpgrade_DetectsVersionChange"`

## Related Documentation

- [README.md](../../README.md) - Main product documentation with FAQ sections
- [.gauntletci.json](../../.gauntletci.json) - Configuration schema validation
- [CONTRIBUTING.md](../../CONTRIBUTING.md) - Development contribution guidelines

## Test Maintenance

When updating FAQ claims in the README:

1. Update the corresponding test file with the new claim (in the `/// <summary>` XML comment)
2. Implement or update test logic to validate the claim
3. Execute `dotnet test` to verify
4. Update this README with new test status

This ensures FAQ documentation remains synchronized with actual product behavior.
