// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests.Core.Rules;

/// <summary>
/// Tests for RuleOrchestrator enrichment extensions.
/// </summary>
public class RuleOrchestratorExtensionsTests
{
    [Fact]
    public async Task EnrichAsync_WithNullPipeline_ThrowsArgumentNullException()
    {
        var result = new EvaluationResult
        {
            Findings = new List<Finding>(),
            RulesEvaluated = 0,
            RuleMetrics = new List<RuleExecutionMetric>(),
            FileStatistics = new FileEligibilityStatistics(),
        };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => result.EnrichAsync(null!));

        Assert.Contains("pipeline", ex.Message);
    }

    [Fact]
    public async Task EnrichAsync_WithNullFindings_ReturnsResultUnchanged()
    {
        var result = new EvaluationResult
        {
            Findings = null!,
            RulesEvaluated = 0,
            RuleMetrics = new List<RuleExecutionMetric>(),
            FileStatistics = new FileEligibilityStatistics(),
        };

        var pipeline = new EnrichmentPipeline(new[] { new NullFindingEnricher("Null") });

        var enriched = await result.EnrichAsync(pipeline);

        Assert.Same(result, enriched);
        Assert.Null(enriched?.Findings);
    }

    [Fact]
    public async Task EnrichAsync_WithEmptyFindings_ReturnsResultUnchanged()
    {
        var result = new EvaluationResult
        {
            Findings = new List<Finding>(),
            RulesEvaluated = 0,
            RuleMetrics = new List<RuleExecutionMetric>(),
            FileStatistics = new FileEligibilityStatistics(),
        };

        var pipeline = new EnrichmentPipeline(new[] { new NullFindingEnricher("Null") });

        var enriched = await result.EnrichAsync(pipeline);

        Assert.Same(result, enriched);
        Assert.Empty(enriched?.Findings ?? new List<Finding>());
    }

    [Fact]
    public async Task EnrichAsync_WithFindings_EnrichesAllInPlace()
    {
        var findings = new List<Finding>
        {
            new()
            {
                RuleId = "GCI0001",
                RuleName = "Test1",
                Summary = "Test",
                Evidence = "src/Program.cs:15: code1",
                WhyItMatters = "Test",
                SuggestedAction = "Test",
            },
            new()
            {
                RuleId = "GCI0002",
                RuleName = "Test2",
                Summary = "Test",
                Evidence = "src/Data.cs:20: code2",
                WhyItMatters = "Test",
                SuggestedAction = "Test",
            },
        };

        var result = new EvaluationResult
        {
            Findings = findings,
            RulesEvaluated = 2,
            RuleMetrics = new List<RuleExecutionMetric>(),
            FileStatistics = new FileEligibilityStatistics(),
        };

        // Use a test enricher that extracts code snippets
        var testEnricher = new TestEnricher();
        var pipeline = new EnrichmentPipeline(new[] { testEnricher });

        var enriched = await result.EnrichAsync(pipeline);

        Assert.Same(result, enriched);
        Assert.NotNull(enriched);
        Assert.Equal(2, enriched.Findings!.Count);
        Assert.Equal("code1", enriched.Findings[0].CodeSnippet);
        Assert.Equal("code2", enriched.Findings[1].CodeSnippet);
    }

    [Fact]
    public async Task EnrichAsync_WhenEnrichmentFails_DoesNotThrow()
    {
        var findings = new List<Finding>
        {
            new()
            {
                RuleId = "GCI0001",
                RuleName = "Test",
                Summary = "Test",
                Evidence = "test",
                WhyItMatters = "Test",
                SuggestedAction = "Test",
            },
        };

        var result = new EvaluationResult
        {
            Findings = findings,
            RulesEvaluated = 1,
            RuleMetrics = new List<RuleExecutionMetric>(),
            FileStatistics = new FileEligibilityStatistics(),
        };

        var errorEnricher = new ErrorThrowingEnricher();
        var pipeline = new EnrichmentPipeline(new[] { errorEnricher });

        // Should not throw even though enricher throws
        var enriched = await result.EnrichAsync(pipeline);

        Assert.Same(result, enriched);
    }

    /// <summary>Test enricher that extracts code from evidence (after last colon).</summary>
    private class TestEnricher : IFindingEnricher
    {
        public string StageName => "TestExtractor";
        public bool IsAvailable => true;
        public IReadOnlySet<string> DependsOn => new HashSet<string>();

        public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
        {
            if (finding?.Evidence == null)
                return Task.FromResult(false);

            var parts = finding.Evidence.Split(':');
            if (parts.Length >= 2)
            {
                finding.CodeSnippet = parts[^1].Trim();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    /// <summary>Test enricher that throws an exception.</summary>
    private class ErrorThrowingEnricher : IFindingEnricher
    {
        public string StageName => "ErrorThrower";
        public bool IsAvailable => true;
        public IReadOnlySet<string> DependsOn => new HashSet<string>();

        public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test error");
        }
    }
}

