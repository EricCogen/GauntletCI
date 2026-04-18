// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Output;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class GitHubPrReviewWriterTests
{
    private static Finding MakeFinding(
        string ruleId          = "GCI0001",
        string ruleName        = "Test Rule",
        string summary         = "test finding",
        string? filePath       = "src/Foo.cs",
        int?    line           = 42,
        string? evidence       = null,
        string? whyItMatters   = null,
        string? suggestedAction = null,
        string? llmExplanation = null,
        ExpertFact? expertContext = null,
        Confidence confidence  = Confidence.Medium,
        RuleSeverity severity  = RuleSeverity.Warn) => new()
    {
        RuleId          = ruleId,
        RuleName        = ruleName,
        Summary         = summary,
        FilePath        = filePath,
        Line            = line,
        Evidence        = evidence ?? string.Empty,
        WhyItMatters    = whyItMatters ?? string.Empty,
        SuggestedAction = suggestedAction ?? string.Empty,
        LlmExplanation  = llmExplanation,
        ExpertContext   = expertContext,
        Confidence      = confidence,
        Severity        = severity,
    };

    // --- BuildCommentBody ---

    [Fact]
    public void BuildCommentBody_MinimalFinding_ContainsRuleIdAndSummary()
    {
        var f   = MakeFinding(ruleId: "GCI0042", ruleName: "Async Rule", summary: "Use async here");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("GCI0042", body);
        Assert.Contains("Async Rule", body);
        Assert.Contains("Use async here", body);
    }

    [Fact]
    public void BuildCommentBody_ContainsConfidenceAndSeverity()
    {
        var f    = MakeFinding(confidence: Confidence.High, severity: RuleSeverity.Block);
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("High", body);
        Assert.Contains("Block", body);
    }

    [Fact]
    public void BuildCommentBody_WithEvidence_QuotesEvidence()
    {
        var f    = MakeFinding(evidence: "await Task.Delay(0);");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("> await Task.Delay(0);", body);
    }

    [Fact]
    public void BuildCommentBody_WithWhyItMatters_IncludesSection()
    {
        var f    = MakeFinding(whyItMatters: "Deadlocks under load");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("Why it matters", body);
        Assert.Contains("Deadlocks under load", body);
    }

    [Fact]
    public void BuildCommentBody_WithSuggestedAction_IncludesSection()
    {
        var f    = MakeFinding(suggestedAction: "Use ConfigureAwait(false)");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("Suggested action", body);
        Assert.Contains("ConfigureAwait(false)", body);
    }

    [Fact]
    public void BuildCommentBody_WithLlmExplanation_IncludesInsightSection()
    {
        var f    = MakeFinding(llmExplanation: "This blocks the thread pool.");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("LLM insight", body);
        Assert.Contains("This blocks the thread pool.", body);
    }

    [Fact]
    public void BuildCommentBody_WithExpertContext_IncludesExpertSection()
    {
        var ctx  = new ExpertFact("Prefer async all the way", "MSDN", 0.95f);
        var f    = MakeFinding(expertContext: ctx);
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("Expert context", body);
        Assert.Contains("Prefer async all the way", body);
        Assert.Contains("MSDN", body);
    }

    [Fact]
    public void BuildCommentBody_NoLlmNoExpert_DoesNotContainThoseSections()
    {
        var f    = MakeFinding(llmExplanation: null, expertContext: null);
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.DoesNotContain("LLM insight", body);
        Assert.DoesNotContain("Expert context", body);
    }

    // --- ResolvePrNumber ---

    [Fact]
    public void ResolvePrNumber_ExplicitEnvVar_ReturnsParsedNumber()
    {
        var prev = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        try
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", "99");
            Environment.SetEnvironmentVariable("GITHUB_REF", null);
            Assert.Equal(99, GitHubPrReviewWriter.ResolvePrNumber());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", prev);
        }
    }

    [Fact]
    public void ResolvePrNumber_FromGithubRef_ParsesCorrectly()
    {
        var prevNum = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        var prevRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        try
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", null);
            Environment.SetEnvironmentVariable("GITHUB_REF", "refs/pull/42/merge");
            Assert.Equal(42, GitHubPrReviewWriter.ResolvePrNumber());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", prevNum);
            Environment.SetEnvironmentVariable("GITHUB_REF", prevRef);
        }
    }

    [Fact]
    public void ResolvePrNumber_ExplicitTakesPrecedenceOverGithubRef()
    {
        var prevNum = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        var prevRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        try
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", "7");
            Environment.SetEnvironmentVariable("GITHUB_REF", "refs/pull/99/merge");
            Assert.Equal(7, GitHubPrReviewWriter.ResolvePrNumber());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", prevNum);
            Environment.SetEnvironmentVariable("GITHUB_REF", prevRef);
        }
    }

    [Fact]
    public void ResolvePrNumber_NeitherSet_ReturnsNull()
    {
        var prevNum = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        var prevRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        try
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", null);
            Environment.SetEnvironmentVariable("GITHUB_REF", "refs/heads/main");
            Assert.Null(GitHubPrReviewWriter.ResolvePrNumber());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", prevNum);
            Environment.SetEnvironmentVariable("GITHUB_REF", prevRef);
        }
    }

    [Fact]
    public void ResolvePrNumber_ZeroOrNegativeIgnored_ReturnsNull()
    {
        var prevNum = Environment.GetEnvironmentVariable("GAUNTLETCI_PR_NUMBER");
        var prevRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        try
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", "0");
            Environment.SetEnvironmentVariable("GITHUB_REF", null);
            Assert.Null(GitHubPrReviewWriter.ResolvePrNumber());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAUNTLETCI_PR_NUMBER", prevNum);
            Environment.SetEnvironmentVariable("GITHUB_REF", prevRef);
        }
    }
}
