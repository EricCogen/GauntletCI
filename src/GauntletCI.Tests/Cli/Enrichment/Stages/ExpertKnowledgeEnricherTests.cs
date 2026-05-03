// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Enrichment.Stages;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests.Cli.Enrichment.Stages;

/// <summary>
/// Tests for ExpertKnowledgeEnricher with knowledge base lookups.
/// </summary>
public class ExpertKnowledgeEnricherTests
{
    private static readonly Dictionary<string, ExpertFact> SampleKnowledgeBase = new()
    {
        { "GCI0001", new ExpertFact("Async void is dangerous in event handlers", "CSharp.Best.Practices", 0.95f) },
        { "GCI0022", new ExpertFact("Event handlers should use single source of truth pattern", "Event.Handling", 0.9f) },
    };

    [Fact]
    public async Task ExpertKnowledgeEnricher_WithEmptyKnowledgeBase_IsNotAvailable()
    {
        var enricher = new ExpertKnowledgeEnricher(new Dictionary<string, ExpertFact>());

        Assert.False(enricher.IsAvailable);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_WithKnowledgeBase_IsAvailable()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);

        Assert.True(enricher.IsAvailable);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_MatchesByRuleId_EnrichesWithExpertFact()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "EventHandlerSafety",
            Summary = "Async void detected",
            Evidence = "code",
            WhyItMatters = "Dangerous",
            SuggestedAction = "Use async Task",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.NotNull(finding.ExpertContext);
        Assert.Equal("Async void is dangerous in event handlers", finding.ExpertContext.Content);
        Assert.Equal("CSharp.Best.Practices", finding.ExpertContext.Source);
        Assert.Equal(0.95f, finding.ExpertContext.Score);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_MatchesByRuleName_EnrichesWithExpertFact()
    {
        var enricher = new ExpertKnowledgeEnricher(
            new Dictionary<string, ExpertFact>
            {
                { "EventHandlerSafety", new ExpertFact("Safety best practice", "Testing", 0.8f) },
            });

        var finding = new Finding
        {
            RuleId = "GCI0999",
            RuleName = "EventHandlerSafety",
            Summary = "Test",
            Evidence = "code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.NotNull(finding.ExpertContext);
        Assert.Equal("Safety best practice", finding.ExpertContext.Content);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_MatchesCaseInsensitiveRuleName()
    {
        var enricher = new ExpertKnowledgeEnricher(
            new Dictionary<string, ExpertFact>
            {
                { "EVENTHANDLERSAFETY", new ExpertFact("Safety fact", "Testing", 0.8f) },
            });

        var finding = new Finding
        {
            RuleId = "GCI0999",
            RuleName = "eventhandlersafety",  // Lowercase
            Summary = "Test",
            Evidence = "code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.NotNull(finding.ExpertContext);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_NoMatchingFact_ReturnsFalse()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);
        var finding = new Finding
        {
            RuleId = "GCI0099",
            RuleName = "UnknownRule",
            Summary = "Test",
            Evidence = "code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.False(enriched);
        Assert.Null(finding.ExpertContext);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_AlreadyEnriched_SkipsWithoutLookup()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);
        var existingFact = new ExpertFact("Existing fact", "Existing", 0.5f);
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
            ExpertContext = existingFact,
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.False(enriched);
        Assert.Same(existingFact, finding.ExpertContext);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_NullFinding_ReturnsFalse()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);

        var enriched = await enricher.EnrichAsync(null!);

        Assert.False(enriched);
    }

    [Fact]
    public void ExpertKnowledgeEnricher_StageName_IsCorrect()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);

        Assert.Equal("ExpertKnowledge", enricher.StageName);
    }

    [Fact]
    public void ExpertKnowledgeEnricher_DependsOn_IsEmpty()
    {
        var enricher = new ExpertKnowledgeEnricher(SampleKnowledgeBase);

        Assert.Empty(enricher.DependsOn);
    }

    [Fact]
    public async Task ExpertKnowledgeEnricher_PrefersExactRuleIdMatch()
    {
        var knowledgeBase = new Dictionary<string, ExpertFact>
        {
            { "GCI0001", new ExpertFact("Exact ID match", "Id", 0.95f) },
            { "TestRule", new ExpertFact("Rule name match", "Name", 0.8f) },
        };

        var enricher = new ExpertKnowledgeEnricher(knowledgeBase);
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "TestRule",
            Summary = "Test",
            Evidence = "code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        // Should match on RuleId first
        Assert.True(enriched);
        Assert.Equal("Exact ID match", finding.ExpertContext!.Content);
    }
}
