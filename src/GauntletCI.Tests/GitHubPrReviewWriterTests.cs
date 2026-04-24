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

    // --- FormatEvidenceMarkdown ---

    [Fact]
    public void FormatEvidenceMarkdown_WasNow_ReturnsDiffBlock()
    {
        var result = GitHubPrReviewWriter.FormatEvidenceMarkdown("Was: public void Foo(int a, int b) | Now: public void Foo(int a)");

        Assert.Contains("```diff", result);
        Assert.Contains("- public void Foo(int a, int b)", result);
        Assert.Contains("+ public void Foo(int a)", result);
    }

    [Fact]
    public void FormatEvidenceMarkdown_Removed_ReturnsDiffBlock()
    {
        var result = GitHubPrReviewWriter.FormatEvidenceMarkdown("Removed: public void OldMethod()");

        Assert.Contains("```diff", result);
        Assert.Contains("- public void OldMethod()", result);
        Assert.DoesNotContain("+", result);
    }

    [Fact]
    public void FormatEvidenceMarkdown_RemovedLogic_ReturnsDiffBlockWithMultipleLines()
    {
        var result = GitHubPrReviewWriter.FormatEvidenceMarkdown("Removed logic: return x > 0 | if (x == null) | throw new Exception()");

        Assert.Contains("```diff", result);
        Assert.Contains("- return x > 0", result);
        Assert.Contains("- if (x == null)", result);
        Assert.Contains("- throw new Exception()", result);
    }

    [Fact]
    public void FormatEvidenceMarkdown_PlainText_ReturnsBlockquote()
    {
        var result = GitHubPrReviewWriter.FormatEvidenceMarkdown("await Task.Delay(0);");

        Assert.StartsWith(">", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void BuildCommentBody_WasNowEvidence_RendersDiffBlock()
    {
        var f    = MakeFinding(evidence: "Was: Task.Run(Foo) | Now: await FooAsync()");
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("```diff", body);
        Assert.Contains("- Task.Run(Foo)", body);
        Assert.Contains("+ await FooAsync()", body);
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

    [Fact]
    public void BuildCommentBody_WithCoverageNote_IncludesCoverageSection()
    {
        var f    = MakeFinding();
        f.CoverageNote = "⚠️ No test coverage detected for this file (Codecov).";
        var body = GitHubPrReviewWriter.BuildCommentBody(f);

        Assert.Contains("📊", body);
        Assert.Contains("Coverage", body);
        Assert.Contains("No test coverage", body);
    }

    // --- BuildReviewBody ---

    private static GroupedFinding MakeGroup(
        string ruleId           = "GCI0016",
        string ruleName         = "Concurrency Rule",
        string summary          = "Static mutable field detected",
        string filePath         = "src/Foo.cs",
        int    primaryLine      = 12,
        string whyItMatters     = "Mutable static fields are shared across threads.",
        string suggestedAction  = "Use Interlocked or readonly.",
        string evidence         = "Line 12: private static long _count;") => new()
    {
        RuleId          = ruleId,
        RuleName        = ruleName,
        Summary         = summary,
        FilePath        = filePath,
        PrimaryLine     = primaryLine,
        Lines           = new[] { primaryLine },
        Evidence        = new[] { evidence },
        WhyItMatters    = whyItMatters,
        SuggestedAction = suggestedAction,
        Confidence      = Confidence.Medium,
        Severity        = RuleSeverity.Warn,
        Count           = 1,
    };

    [Fact]
    public void BuildReviewBody_NoSummaryGroups_NoInline_ReturnsEmpty()
    {
        var body = GitHubPrReviewWriter.BuildReviewBody(new(), hasInlineComments: false);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void BuildReviewBody_NoSummaryGroups_HasInline_ReturnsPointerNote()
    {
        var body = GitHubPrReviewWriter.BuildReviewBody(new(), hasInlineComments: true);
        Assert.Contains("inline comments", body);
    }

    [Fact]
    public void BuildReviewBody_SummaryGroup_EmbedsRichDetailsBlock()
    {
        var groups = new List<GroupedFinding> { MakeGroup() };
        var body   = GitHubPrReviewWriter.BuildReviewBody(groups, hasInlineComments: false);

        // Top-level header preserved
        Assert.Contains("**GauntletCI** found the following issues:", body);
        // Details/summary scaffolding present
        Assert.Contains("<details>", body);
        Assert.Contains("</details>", body);
        Assert.Contains("<summary>", body);
        // Rich body: rule id, evidence, why, action, confidence/severity all present (matches inline format)
        Assert.Contains("GCI0016", body);
        Assert.Contains("**Evidence:**", body);
        Assert.Contains("Why it matters", body);
        Assert.Contains("Suggested action", body);
        Assert.Contains("Confidence:", body);
        Assert.Contains("Severity:", body);
    }

    [Fact]
    public void BuildReviewBody_MultipleSummaryGroups_EmitsOneDetailsPerGroup()
    {
        var groups = new List<GroupedFinding>
        {
            MakeGroup(ruleId: "GCI0010", ruleName: "Hardcoding", summary: "Hardcoded conn string"),
            MakeGroup(ruleId: "GCI0042", ruleName: "TODO Detection", summary: "TODO in payment flow"),
        };
        var body = GitHubPrReviewWriter.BuildReviewBody(groups, hasInlineComments: false);

        var detailsCount = System.Text.RegularExpressions.Regex.Matches(body, "<details>").Count;
        Assert.Equal(2, detailsCount);
        Assert.Contains("GCI0010", body);
        Assert.Contains("GCI0042", body);
    }

    [Fact]
    public void BuildReviewBody_HasInlineAndSummary_AppendsInlinePointer()
    {
        var groups = new List<GroupedFinding> { MakeGroup() };
        var body   = GitHubPrReviewWriter.BuildReviewBody(groups, hasInlineComments: true);
        Assert.Contains("inline comments on the diff", body);
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
