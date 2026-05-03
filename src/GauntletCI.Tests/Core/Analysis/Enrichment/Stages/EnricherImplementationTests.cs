// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Enrichment.Stages;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests.Core.Analysis.Enrichment.Stages;

/// <summary>
/// Tests for concrete enricher implementations (CodeSnippetEnricher, etc.)
/// Note: LlmExplanationEnricher requires ILlmEngine which is only tested in integration scenarios.
/// </summary>
public class EnricherImplementationTests
{
    [Fact]
    public async Task CodeSnippetEnricher_ValidEvidence_ExtractsSnippet()
    {
        var enricher = new CodeSnippetEnricher();
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "src/Program.cs:15: async void EventHandler() { }",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.Equal("async void EventHandler() { }", finding.CodeSnippet);
    }

    [Fact]
    public async Task CodeSnippetEnricher_EvidenceWithoutColon_UsesWhole()
    {
        var enricher = new CodeSnippetEnricher();
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "var secret = \"password123\";",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.Equal("var secret = \"password123\";", finding.CodeSnippet);
    }

    [Fact]
    public async Task CodeSnippetEnricher_AlreadyEnriched_Skipped()
    {
        var enricher = new CodeSnippetEnricher();
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "src/Program.cs:15: code",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
            CodeSnippet = "Already set",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.False(enriched);
        Assert.Equal("Already set", finding.CodeSnippet);
    }

    [Fact]
    public async Task CodeSnippetEnricher_EmptyEvidence_Skipped()
    {
        var enricher = new CodeSnippetEnricher();
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.False(enriched);
        Assert.Null(finding.CodeSnippet);
    }

    [Fact]
    public async Task CodeSnippetEnricher_NullFinding_ReturnsFalse()
    {
        var enricher = new CodeSnippetEnricher();

        var enriched = await enricher.EnrichAsync(null!);

        Assert.False(enriched);
    }

    [Fact]
    public void CodeSnippetEnricher_AlwaysAvailable()
    {
        var enricher = new CodeSnippetEnricher();

        Assert.True(enricher.IsAvailable);
        Assert.Empty(enricher.DependsOn);
    }

    [Fact]
    public void CodeSnippetEnricher_StageName_IsCorrect()
    {
        var enricher = new CodeSnippetEnricher();

        Assert.Equal("CodeSnippet", enricher.StageName);
    }

    [Fact]
    public async Task CodeSnippetEnricher_MultipleColons_CorrectlyExtracts()
    {
        var enricher = new CodeSnippetEnricher();
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test",
            Evidence = "src/Program.cs:15:20: var x = \"key:value\";",
            WhyItMatters = "Test",
            SuggestedAction = "Test",
        };

        var enriched = await enricher.EnrichAsync(finding);

        Assert.True(enriched);
        Assert.Equal("20: var x = \"key:value\";", finding.CodeSnippet);
    }
}
