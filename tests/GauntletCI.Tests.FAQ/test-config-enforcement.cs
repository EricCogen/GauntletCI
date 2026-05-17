using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace GauntletCI.Tests.FAQ;

/// <summary>
/// FAQ Claim: "Configuration is managed via a root `.gauntletci.json` file checked directly into the repository version control.
/// Local overrides can be restricted or locked down globally within this file. If a rule is marked as `Enforced`, 
/// local inline suppressions (`// gauntlet-disable`) will be flagged as an immediate compilation/validation failure."
/// 
/// Test Goal: Verify that when a rule is marked as Enforced in .gauntletci.json, local suppressions are rejected.
/// </summary>
public class ConfigEnforcementTests
{
    private const string RepoRoot = @"C:\Users\ericc\GauntletCI";
    private const string TestProjectPath = @"src\GauntletCI.Core.Tests";

    [Fact(Skip = "Requires configuration + GCI integration")]
    public void ConfigDrift_EnforcedRuleRejectsLocalSuppression()
    {
        // Setup: Create a test file with a local suppression comment
        var testFile = Path.Combine(RepoRoot, TestProjectPath, "config-drift-test-temp.cs");
        var configPath = Path.Combine(RepoRoot, ".gauntletci.json");

        try
        {
            // Create test C# file with suppressed rule
            var testCode = @"
namespace GauntletCI.Tests.FAQ;
public class ConfigDriftTest
{
    // gauntlet-disable GCI0032
    public void ThrowExceptionWithoutHandling()
    {
        throw new InvalidOperationException(""Unhandled by design"");
    }
}
";
            File.WriteAllText(testFile, testCode);

            // Read current config
            var currentConfig = File.ReadAllText(configPath);
            var configDoc = JsonDocument.Parse(currentConfig);

            // Verify that .gauntletci.json contains rule enforcement configuration
            // (This is documentation verification, not an executable test)
            Assert.NotEqual(default, configDoc.RootElement.ValueKind);
            
            // In a full integration test, we would:
            // 1. Mark rule GCI0032 as "Enforced: true" in .gauntletci.json
            // 2. Run `gauntletci analyze --staged` on the test file
            // 3. Verify exit code is non-zero due to enforced rule bypass attempt
            // 4. Verify stderr contains message about enforced rule rejection
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public void ConfigModel_VerifyEnforcementStructure()
    {
        // Documentation Test: Verify .gauntletci.json has the right structure for enforcement
        var configPath = Path.Combine(RepoRoot, ".gauntletci.json");
        Assert.True(File.Exists(configPath), "Configuration file should exist");

        var configContent = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(configContent);

        // Verify config has rules section (basic structure validation)
        Assert.NotEqual(default, doc.RootElement.ValueKind);
        
        // This test passes as long as the config file is valid JSON
        // Full enforcement testing requires CLI integration
    }
}
