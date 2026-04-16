// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class GitHubAnnotationWriterBuildMessageTests
{
    private static Finding MakeFinding(
        string summary = "test finding",
        string? llmExplanation = null,
        ExpertFact? expertContext = null) => new()
    {
        RuleId = "GCI0001",
        RuleName = "Test Rule",
        Summary = summary,
        Evidence = "some code",
        WhyItMatters = "it matters",
        SuggestedAction = "fix it",
        LlmExplanation = llmExplanation,
        ExpertContext = expertContext,
    };

    [Fact]
    public void BuildMessage_SummaryOnly_ReturnsSanitizedSummary()
    {
        var f = MakeFinding(summary: "test finding");
        Assert.Equal("test finding", GitHubAnnotationWriter.BuildMessage(f));
    }

    [Fact]
    public void BuildMessage_WithNewlines_SanitizesNewlines()
    {
        var f = MakeFinding(summary: "line1\nline2");
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.Contains("%0A", msg);
        Assert.DoesNotContain("\n", msg);
    }

    [Fact]
    public void BuildMessage_WithLlmExplanation_AppendsExplanation()
    {
        var f = MakeFinding(llmExplanation: "use async");
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.Contains("| LLM: use async", msg);
    }

    [Fact]
    public void BuildMessage_WithExpertContext_AppendsExpert()
    {
        var ctx = new ExpertFact("prefer async", "MSDN", 0.9f);
        var f = MakeFinding(expertContext: ctx);
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.Contains("| Expert: prefer async (MSDN)", msg);
    }

    [Fact]
    public void BuildMessage_LlmExplanationWithNewline_IsSanitized()
    {
        var f = MakeFinding(llmExplanation: "line1\nline2");
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.DoesNotContain("\n", msg);
    }

    [Fact]
    public void BuildMessage_NoLlmNoExpert_JustSummary()
    {
        var f = MakeFinding(summary: "plain summary", llmExplanation: null, expertContext: null);
        var msg = GitHubAnnotationWriter.BuildMessage(f);
        Assert.Equal("plain summary", msg);
        Assert.DoesNotContain("LLM", msg);
        Assert.DoesNotContain("Expert", msg);
    }
}
