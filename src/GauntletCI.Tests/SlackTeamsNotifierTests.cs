// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class SlackTeamsNotifierTests
{
    private static EvaluationResult MakeResult(params Finding[] findings) =>
        new() { Findings = [.. findings] };

    private static Finding MakeFinding(
        string ruleId    = "GCI0001",
        string summary   = "A test summary",
        string evidence  = "some evidence",
        RuleSeverity severity = RuleSeverity.Block) => new()
    {
        RuleId          = ruleId,
        RuleName        = "Test Rule",
        Summary         = summary,
        Evidence        = evidence,
        WhyItMatters    = "why it matters",
        SuggestedAction = "do something",
        Confidence      = Confidence.High,
        Severity        = severity,
    };

    [Fact]
    public void BuildSlackPayload_WithBlockFindings_ContainsRuleId()
    {
        var result = MakeResult(MakeFinding(ruleId: "GCI0099", severity: RuleSeverity.Block));

        var json = SlackTeamsNotifier.BuildSlackPayload(result, "owner/repo", "42", "abc12345");

        Assert.Contains("GCI0099", json);
    }

    [Fact]
    public void BuildSlackPayload_WithNoBlockFindings_ReturnsEmpty()
    {
        var result = MakeResult(MakeFinding(severity: RuleSeverity.Warn));

        var json = SlackTeamsNotifier.BuildSlackPayload(result, "owner/repo", "42", "abc12345");

        Assert.True(string.IsNullOrEmpty(json));
    }

    [Fact]
    public void BuildTeamsPayload_WithBlockFindings_ContainsTitle()
    {
        var result = MakeResult(MakeFinding(ruleId: "GCI0099", severity: RuleSeverity.Block));

        var json = SlackTeamsNotifier.BuildTeamsPayload(result, "owner/repo", "42", "abc12345");

        Assert.Contains("GauntletCI", json);
        Assert.Contains("GCI0099", json);
    }

    [Fact]
    public void BuildSlackPayload_MoreThanThreeBlockFindings_ContainsMoreMessage()
    {
        var result = MakeResult(
            MakeFinding(ruleId: "GCI0001", severity: RuleSeverity.Block),
            MakeFinding(ruleId: "GCI0002", severity: RuleSeverity.Block),
            MakeFinding(ruleId: "GCI0003", severity: RuleSeverity.Block),
            MakeFinding(ruleId: "GCI0004", severity: RuleSeverity.Block));

        var json = SlackTeamsNotifier.BuildSlackPayload(result, "owner/repo", "42", "abc12345");

        Assert.Contains("more Block findings", json);
    }

    [Fact]
    public void BuildTeamsPayload_ContainsRepoAndPr()
    {
        var result = MakeResult(MakeFinding(severity: RuleSeverity.Block));

        var json = SlackTeamsNotifier.BuildTeamsPayload(result, "my-org/my-repo", "77", "deadbeef");

        Assert.Contains("my-org/my-repo", json);
        Assert.Contains("77", json);
    }
}
