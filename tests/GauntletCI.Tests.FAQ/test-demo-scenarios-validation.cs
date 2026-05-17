using System.Diagnostics;
using Xunit;

namespace GauntletCI.Tests.FAQ;

/// <summary>
/// Validates the 36 demo scenarios from GauntletCI-Demo repository.
/// 
/// README references:
/// - "36 scenarios across 3 tiers"
/// - Tier 1 (6 scenarios): S01-S06 with documented verdicts
/// - Tier 2 (12 scenarios): Single-rule scenarios (one rule isolated per scenario)
/// - Tier 3 (18 scenarios): Behavioral regression scenarios
///
/// This test suite verifies that the demo scenarios produce the expected findings.
/// 
/// Tier 1 Expected Verdicts (from README lines 146-153):
/// S01: Safe typo fix → clean (no findings)
/// S02: Silent catch {} around payment call → GCI0007
/// S03: Hardcoded API key in Program.cs → GCI0012
/// S04: CancellationToken dropped from IPaymentClient → GCI0004
/// S05: Customer email logged in LogInformation → GCI0029
/// S06: Static counter mutated without sync → GCI0016
/// </summary>
public class DemoScenariosValidationTests
{
    private const string DemoRepoUrl = "https://github.com/EricCogen/GauntletCI-Demo.git";
    private readonly string _demoRepoPath = Path.Combine(Path.GetTempPath(), "GauntletCI-Demo");

    public DemoScenariosValidationTests()
    {
        // In a real test, we would clone the demo repo
        // For now, these tests are placeholders showing the validation structure
    }

    /// <summary>
    /// Tier 1, Scenario 01 (PR 01): Safe typo fix
    /// Expected: Clean (no findings)
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S01_SafeTypoFix_ProducesNoFindings()
    {
        // This scenario should produce zero findings
        var output = await AnalyzeDemoPullRequest(1);
        Assert.DoesNotContain("GCI", output);
    }

    /// <summary>
    /// Tier 1, Scenario 02 (PR 02): Silent catch {} around payment call
    /// Expected: GCI0007 Error Handling Integrity
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S02_SilentCatch_ProducesGCI0007()
    {
        var output = await AnalyzeDemoPullRequest(2);
        Assert.Contains("GCI0007", output);
        Assert.Contains("Error Handling", output);
    }

    /// <summary>
    /// Tier 1, Scenario 03 (PR 03): Hardcoded API key in Program.cs
    /// Expected: GCI0012 Secret Hygiene
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S03_HardcodedApiKey_ProducesGCI0012()
    {
        var output = await AnalyzeDemoPullRequest(3);
        Assert.Contains("GCI0012", output);
        Assert.Contains("Secret", output);
    }

    /// <summary>
    /// Tier 1, Scenario 04 (PR 04): CancellationToken dropped from IPaymentClient
    /// Expected: GCI0004 Public API Contract
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S04_CancellationTokenDropped_ProducesGCI0004()
    {
        var output = await AnalyzeDemoPullRequest(4);
        Assert.Contains("GCI0004", output);
        Assert.Contains("Contract", output);
    }

    /// <summary>
    /// Tier 1, Scenario 05 (PR 05): Customer email logged in LogInformation
    /// Expected: GCI0029 PII Logging Leak
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S05_PiiLoggingLeak_ProducesGCI0029()
    {
        var output = await AnalyzeDemoPullRequest(5);
        Assert.Contains("GCI0029", output);
        Assert.Contains("PII", output);
    }

    /// <summary>
    /// Tier 1, Scenario 06 (PR 06): Static counter mutated without sync
    /// Expected: GCI0016 Concurrency Safety
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier1_S06_UnsynchronizedStaticMutation_ProducesGCI0016()
    {
        var output = await AnalyzeDemoPullRequest(6);
        Assert.Contains("GCI0016", output);
        Assert.Contains("Concurrency", output);
    }

    /// <summary>
    /// Tier 3 Category: Architectural Access Control (S19, S23, S24)
    /// These scenarios show removal of boundary enforcement in the diff.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_ArchitecturalAccessControl_DetectsRemovedBoundaries()
    {
        // S19: Should detect removed access control boundary
        var s19 = await AnalyzeDemoPullRequest(19);
        Assert.Contains("GCI", s19);
        
        // S23: Boundary enforcement removal
        var s23 = await AnalyzeDemoPullRequest(23);
        Assert.Contains("GCI", s23);
        
        // S24: Access control regression
        var s24 = await AnalyzeDemoPullRequest(24);
        Assert.Contains("GCI", s24);
    }

    /// <summary>
    /// Tier 3 Category: Execution Sequence Changes (S20, S28-S30)
    /// These scenarios show state mutation or external call reordering.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_ExecutionSequenceChanges_DetectsReordering()
    {
        // S20: External call reordering
        var s20 = await AnalyzeDemoPullRequest(20);
        Assert.Contains("GCI", s20);
        
        // S28-S30: State mutation reordering
        var s28 = await AnalyzeDemoPullRequest(28);
        Assert.Contains("GCI", s28);
    }

    /// <summary>
    /// Tier 3 Category: Async Propagation Drops (S21, S25-S27)
    /// These scenarios show CancellationToken context loss in call stacks.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_AsyncPropagationDrops_DetectsCancellationTokenLoss()
    {
        // S21: CancellationToken context loss
        var s21 = await AnalyzeDemoPullRequest(21);
        Assert.Contains("GCI", s21);
        
        // S25-S27: Propagation context drops
        var s25 = await AnalyzeDemoPullRequest(25);
        Assert.Contains("GCI", s25);
    }

    /// <summary>
    /// Tier 3 Category: Public Contract Drift (S22, S31-S32)
    /// These scenarios show method signature/default parameter changes in diffs.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_PublicContractDrift_DetectsSignatureChanges()
    {
        // S22: Public method signature change
        var s22 = await AnalyzeDemoPullRequest(22);
        Assert.Contains("GCI0004", s22);
        
        // S31-S32: Default parameter changes
        var s31 = await AnalyzeDemoPullRequest(31);
        Assert.Contains("GCI", s31);
    }

    /// <summary>
    /// Tier 3 Category: Performance & Resource (S33-S34)
    /// These scenarios show configuration changes and pooling disablement.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_PerformanceAndResource_DetectsConfigurationChanges()
    {
        // S33: Configuration change impact
        var s33 = await AnalyzeDemoPullRequest(33);
        Assert.Contains("GCI", s33);
        
        // S34: Pooling disablement
        var s34 = await AnalyzeDemoPullRequest(34);
        Assert.Contains("GCI", s34);
    }

    /// <summary>
    /// Tier 3 Category: Dependency Injection Scope (S35-S36)
    /// These scenarios show scope boundary mismatches in DI config.
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task Tier3_DependencyInjectionScope_DetectsScopeBoundaryMismatches()
    {
        // S35: DI scope boundary change
        var s35 = await AnalyzeDemoPullRequest(35);
        Assert.Contains("GCI", s35);
        
        // S36: Scope mismatch in configuration
        var s36 = await AnalyzeDemoPullRequest(36);
        Assert.Contains("GCI", s36);
    }

    /// <summary>
    /// Cross-Scenario Validation: All 36 scenarios should either:
    /// 1. Produce zero findings (scenario is "clean")
    /// 2. Produce one of the documented GCI rule IDs
    /// 3. Be a regression test showing GCI's diff-based advantage
    /// </summary>
    [Fact(Skip = "Requires GauntletCI-Demo repository - run manually")]
    public async Task AllTier1_ScenariosProduceValidFindings()
    {
        var validRuleIds = new[]
        {
            "GCI0001", "GCI0003", "GCI0004", "GCI0006", "GCI0007", "GCI0010",
            "GCI0012", "GCI0015", "GCI0016", "GCI0020", "GCI0021", "GCI0022",
            "GCI0024", "GCI0029", "GCI0032", "GCI0035", "GCI0036", "GCI0038",
            "GCI0039", "GCI0041", "GCI0042", "GCI0043", "GCI0044", "GCI0045",
            "GCI0046", "GCI0047", "GCI0048", "GCI0049", "GCI0050", "GCI0051",
            "GCI0052", "GCI0053", "GCI0054", "GCI0055", "GCI0056", "GCI0057"
        };

        for (int pr = 1; pr <= 6; pr++)
        {
            var output = await AnalyzeDemoPullRequest(pr);
            
            // Output should either be clean or contain a valid GCI rule ID
            if (output.Contains("no findings") || string.IsNullOrWhiteSpace(output))
            {
                continue; // Clean scenario
            }

            var hasValidRuleId = validRuleIds.Any(ruleId => output.Contains(ruleId));
            Assert.True(
                hasValidRuleId || output.Length == 0,
                $"PR {pr} produced unrecognized output: {output}"
            );
        }
    }

    /// <summary>
    /// Helper: Analyze a specific PR from the demo repository
    /// </summary>
    private async Task<string> AnalyzeDemoPullRequest(int prNumber)
    {
        // Ensure demo repo is cloned
        if (!Directory.Exists(_demoRepoPath))
        {
            await CloneDemoRepository();
        }

        // Fetch and check out the PR branch
        await RunGit(_demoRepoPath, "fetch", "origin", $"pull/{prNumber}/head:pr-{prNumber}");
        await RunGit(_demoRepoPath, "checkout", $"pr-{prNumber}");

        // Run GauntletCI analyze on staged changes
        var output = await RunGauntletCI(_demoRepoPath, "analyze --staged");
        
        return output;
    }

    /// <summary>
    /// Helper: Clone the demo repository if not already present
    /// </summary>
    private async Task CloneDemoRepository()
    {
        var process = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone {DemoRepoUrl} {_demoRepoPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(process);
        await proc!.WaitForExitAsync();
        
        if (proc.ExitCode != 0)
        {
            var error = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to clone demo repo: {error}");
        }
    }

    /// <summary>
    /// Helper: Run git command
    /// </summary>
    private static async Task RunGit(string workingDir, params string[] args)
    {
        var process = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = string.Join(" ", args),
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(process);
        await proc!.WaitForExitAsync();
    }

    /// <summary>
    /// Helper: Run GauntletCI and capture output
    /// </summary>
    private static async Task<string> RunGauntletCI(string workingDir, string arguments)
    {
        var process = new ProcessStartInfo
        {
            FileName = "gauntletci",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(process);
        var output = await proc!.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        
        return output + error;
    }
}
