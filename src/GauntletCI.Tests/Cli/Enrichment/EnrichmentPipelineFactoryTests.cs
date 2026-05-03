// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Enrichment;
using GauntletCI.Llm;

namespace GauntletCI.Tests.Cli.Enrichment;

/// <summary>
/// Tests for EnrichmentPipelineFactory configurations and builder methods.
/// </summary>
public class EnrichmentPipelineFactoryTests
{
    private class TestLlmEngine : ILlmEngine
    {
        public bool IsAvailable { get; set; } = true;

        public Task<string> EnrichFindingAsync(GauntletCI.Core.Model.Finding finding, CancellationToken ct = default)
        {
            return Task.FromResult("Test explanation");
        }

        public Task<string> SummarizeReportAsync(IEnumerable<GauntletCI.Core.Model.Finding> findings, CancellationToken ct = default)
        {
            return Task.FromResult("Test summary");
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult("Test completion");
        }

        public void Dispose()
        {
            // No-op for test
        }
    }

    [Fact]
    public void CreateDefault_WithoutLlmEngine_IncludesOnlyCodeSnippetEnricher()
    {
        var pipeline = EnrichmentPipelineFactory.CreateDefault();

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("CodeSnippet", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void CreateDefault_WithAvailableLlmEngine_IncludesBothEnrichers()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = true };

        var pipeline = EnrichmentPipelineFactory.CreateDefault(llmEngine);

        Assert.Equal(2, pipeline.Enrichers.Count);
        var stageNames = pipeline.Enrichers.Select(e => e.StageName).ToList();
        Assert.Contains("CodeSnippet", stageNames);
        Assert.Contains("LlmExplanation", stageNames);
    }

    [Fact]
    public void CreateDefault_WithUnavailableLlmEngine_SkipsLlmEnricher()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = false };

        var pipeline = EnrichmentPipelineFactory.CreateDefault(llmEngine);

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("CodeSnippet", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void CreateMinimal_ReturnsOnlyCodeSnippetEnricher()
    {
        var pipeline = EnrichmentPipelineFactory.CreateMinimal();

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("CodeSnippet", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void Create_WithCodeSnippetOption_IncludesCodeSnippetEnricher()
    {
        var pipeline = EnrichmentPipelineFactory.Create(EnricherOptions.CodeSnippet);

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("CodeSnippet", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void Create_WithLlmExplanationOptionAndNullEngine_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => EnrichmentPipelineFactory.Create(EnricherOptions.LlmExplanation));

        Assert.Contains("LlmExplanation", ex.Message);
        Assert.Contains("llmEngine is null", ex.Message);
    }

    [Fact]
    public void Create_WithLlmExplanationOptionAndAvailableEngine_IncludesLlmEnricher()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = true };

        var pipeline = EnrichmentPipelineFactory.Create(EnricherOptions.LlmExplanation, llmEngine);

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("LlmExplanation", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void Create_WithAllOption_IncludesBothEnrichersWhenLlmAvailable()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = true };

        var pipeline = EnrichmentPipelineFactory.Create(EnricherOptions.All, llmEngine);

        Assert.Equal(2, pipeline.Enrichers.Count);
        var stageNames = pipeline.Enrichers.Select(e => e.StageName).ToHashSet();
        Assert.Contains("CodeSnippet", stageNames);
        Assert.Contains("LlmExplanation", stageNames);
    }

    [Fact]
    public void Create_WithAllOptionButUnavailableLlm_IncludesOnlyCodeSnippet()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = false };

        var pipeline = EnrichmentPipelineFactory.Create(EnricherOptions.All, llmEngine);

        Assert.Single(pipeline.Enrichers);
        Assert.Equal("CodeSnippet", pipeline.Enrichers.First().StageName);
    }

    [Fact]
    public void Create_WithNoneOption_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => EnrichmentPipelineFactory.Create(EnricherOptions.None));

        Assert.Contains("At least one enricher", ex.Message);
    }

    [Fact]
    public void Create_WithCombinedOptions_IncludesRequestedEnrichers()
    {
        var llmEngine = new TestLlmEngine { IsAvailable = true };
        var options = EnricherOptions.CodeSnippet | EnricherOptions.LlmExplanation;

        var pipeline = EnrichmentPipelineFactory.Create(options, llmEngine);

        Assert.Equal(2, pipeline.Enrichers.Count);
    }
}
