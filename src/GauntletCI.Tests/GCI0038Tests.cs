// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class GCI0038TimeoutGuardTests
{
    private static readonly DiffContext EmptyDiff = DiffParser.Parse("""
        diff --git a/src/Foo.cs b/src/Foo.cs
        index abc..def 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,1 +1,1 @@
        -old
        +new
        """);

    [Fact]
    public async Task SlowRule_TimesOut_ProducesSyntheticFinding()
    {
        var slowRule = new SlowRule();
        var orchestrator = new RuleOrchestrator(
            [slowRule],
            ruleTimeout: TimeSpan.FromMilliseconds(100));

        var result = await orchestrator.RunAsync(EmptyDiff);

        Assert.Single(result.Findings);
        Assert.Contains("timed out", result.Findings[0].Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SlowRule", result.Findings[0].RuleId);
    }

    [Fact]
    public async Task OuterCancellation_IsNotSwallowedAsTimeout()
    {
        var slowRule = new SlowRule();
        var orchestrator = new RuleOrchestrator(
            [slowRule],
            ruleTimeout: TimeSpan.FromSeconds(30));

        using var outerCts = new CancellationTokenSource();
        outerCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.RunAsync(EmptyDiff, ct: outerCts.Token));
    }

    [Fact]
    public async Task FastRule_CompletesNormally_NoTimeoutFinding()
    {
        var orchestrator = RuleOrchestrator.CreateDefault(
            ruleTimeout: TimeSpan.FromSeconds(30));

        var result = await orchestrator.RunAsync(EmptyDiff);

        Assert.DoesNotContain(result.Findings, f => f.Summary.Contains("timed out"));
    }

    private sealed class SlowRule : IRule
    {
        public string Id   => "SlowRule";
        public string Name => "SlowRule";

        public async Task<List<Finding>> EvaluateAsync(
            AnalysisContext context, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }
    }
}

public class GCI0038NoEchoLogsTests
{
    [Theory]
    [InlineData("Line 42: _logger.LogInformation(user.Email)", "Line 42: [REDACTED]")]
    [InlineData("Line 7: Log.Information(\"SSN={ssn}\", patient.Ssn)", "Line 7: [REDACTED]")]
    [InlineData("src/Auth.cs:99", "src/Auth.cs:99")]   // no snippet — unchanged
    [InlineData("No colon-space here", "No colon-space here")]  // no pattern — unchanged
    public void MaskEvidenceSnippet_MasksSnippetButKeepsFileRef(string input, string expected)
    {
        Assert.Equal(expected, ConsoleReporter.MaskEvidenceSnippet(input));
    }
}
