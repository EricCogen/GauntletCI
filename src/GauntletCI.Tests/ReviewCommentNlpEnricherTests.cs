// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests;

public sealed class ReviewCommentNlpEnricherTests
{
    [Fact]
    public void MatchTaxonomy_RaceCondition_MatchesGCI0016()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("This has a race condition in the event handler.");

        Assert.Contains(result, m => m.RuleId == "GCI0016");
    }

    [Fact]
    public void MatchTaxonomy_NullReferenceAndMemoryLeak_ReturnsBothRules()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy(
            "There is a null reference here and the HttpClient is not disposed (memory leak).");

        var ruleIds = result.Select(m => m.RuleId).ToHashSet();
        Assert.Contains("GCI0043", ruleIds);
        Assert.Contains("GCI0024", ruleIds);
    }

    [Fact]
    public void MatchTaxonomy_NoKeywords_ReturnsEmpty()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("LGTM! Nice clean change.");

        Assert.Empty(result);
    }

    [Fact]
    public void MatchTaxonomy_CaseInsensitive_MatchesGCI0016()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("RACE CONDITION detected in this method.");

        Assert.Contains(result, m => m.RuleId == "GCI0016");
    }

    [Fact]
    public void MatchTaxonomy_EmptyString_ReturnsEmpty()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void MatchTaxonomy_WhitespaceOnly_ReturnsEmpty()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("   \t\n  ");
        Assert.Empty(result);
    }

    [Fact]
    public void MatchTaxonomy_SecurityKeyword_MatchesGCI0012()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("This looks like a security vulnerability - injection risk.");

        Assert.Contains(result, m => m.RuleId == "GCI0012");
    }

    [Fact]
    public void MatchTaxonomy_DeduplicatesPerRuleId()
    {
        // Both "race condition" and "deadlock" map to GCI0016 - should only get one entry
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("race condition and deadlock in this code");

        Assert.Single(result, m => m.RuleId == "GCI0016");
    }

    [Fact]
    public void MatchTaxonomy_BreakingChange_MatchesGCI0004()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("This is a breaking change to the public API.");

        Assert.Contains(result, m => m.RuleId == "GCI0004");
    }

    [Fact]
    public void MatchTaxonomy_SchemaChange_MatchesGCI0021()
    {
        var result = ReviewCommentNlpEnricher.MatchTaxonomy("This db schema change needs a migration.");

        Assert.Contains(result, m => m.RuleId == "GCI0021");
    }
}
