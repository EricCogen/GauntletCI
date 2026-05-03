// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests.Core.Analysis.Enrichment;

/// <summary>
/// Tests for EnrichmentPipeline dependency resolution, execution order, and error handling.
/// </summary>
public class EnrichmentPipelineTests
{
    private sealed class TestEnricher : IFindingEnricher
    {
        private readonly string _name;
        private readonly IReadOnlySet<string> _dependencies;
        private readonly bool _available;
        private readonly Action<Finding>? _action;

        public string StageName => _name;
        public bool IsAvailable => _available;
        public IReadOnlySet<string> DependsOn => _dependencies;

        public TestEnricher(string name, bool available = true, IReadOnlySet<string>? dependencies = null, Action<Finding>? action = null)
        {
            _name = name;
            _available = available;
            _dependencies = dependencies ?? new HashSet<string>();
            _action = action;
        }

        public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
        {
            _action?.Invoke(finding);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void Constructor_ValidEnrichersWithNoDependencies_Succeeds()
    {
        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1"),
            new TestEnricher("Stage2"),
            new TestEnricher("Stage3"),
        };

        var pipeline = new EnrichmentPipeline(enrichers);

        Assert.Equal(3, pipeline.Enrichers.Count);
        Assert.Equal(3, pipeline.ExecutionOrder.Count);
    }

    [Fact]
    public void Constructor_UnresolvableDependency_Throws()
    {
        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1", dependencies: new HashSet<string> { "DoesNotExist" }),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new EnrichmentPipeline(enrichers));
        Assert.Contains("DoesNotExist", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void Constructor_CircularDependency_Throws()
    {
        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1", dependencies: new HashSet<string> { "Stage2" }),
            new TestEnricher("Stage2", dependencies: new HashSet<string> { "Stage1" }),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new EnrichmentPipeline(enrichers));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void ExecutionOrder_RespectsDependencies()
    {
        // Stage1 has no deps, Stage2 depends on Stage1, Stage3 depends on Stage2
        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage3", dependencies: new HashSet<string> { "Stage2" }),
            new TestEnricher("Stage1"),
            new TestEnricher("Stage2", dependencies: new HashSet<string> { "Stage1" }),
        };

        var pipeline = new EnrichmentPipeline(enrichers);

        var order = pipeline.ExecutionOrder;
        Assert.Equal("Stage1", order[0]);
        Assert.Equal("Stage2", order[1]);
        Assert.Equal("Stage3", order[2]);
    }

    [Fact]
    public async Task EnrichAsync_AvailableEnrichers_AllRun()
    {
        var enrichedRules = new HashSet<string>();

        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1", action: f => enrichedRules.Add("Stage1")),
            new TestEnricher("Stage2", action: f => enrichedRules.Add("Stage2")),
        };

        var pipeline = new EnrichmentPipeline(enrichers);
        var finding = new Finding { RuleId = "GCI0001", RuleName = "Test", Summary = "Test", Evidence = "Test", WhyItMatters = "Test", SuggestedAction = "Test" };

        await pipeline.EnrichAsync(new[] { finding });

        Assert.Contains("Stage1", enrichedRules);
        Assert.Contains("Stage2", enrichedRules);
    }

    [Fact]
    public async Task EnrichAsync_UnavailableEnrichers_Skipped()
    {
        var enrichedRules = new HashSet<string>();

        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1", available: true, action: f => enrichedRules.Add("Stage1")),
            new TestEnricher("Stage2", available: false),  // This should be skipped
        };

        var pipeline = new EnrichmentPipeline(enrichers);
        var finding = new Finding { RuleId = "GCI0001", RuleName = "Test", Summary = "Test", Evidence = "Test", WhyItMatters = "Test", SuggestedAction = "Test" };

        var result = await pipeline.EnrichAsync(new[] { finding });

        Assert.Contains("Stage1", result.ExecutedStages.Select(s => s.StageName));
        Assert.Contains("Stage2", result.SkippedStages);
        Assert.Contains("Stage1", enrichedRules);
        Assert.DoesNotContain("Stage2", enrichedRules);
    }

    [Fact]
    public async Task EnrichAsync_MultipleFindingsEnriched()
    {
        var findings = Enumerable.Range(1, 5)
            .Select(i => new Finding
            {
                RuleId = "GCI0001",
                RuleName = "Test",
                Summary = "Test",
                Evidence = "Test",
                WhyItMatters = "Test",
                SuggestedAction = "Test",
            })
            .ToList();

        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1"),
        };

        var pipeline = new EnrichmentPipeline(enrichers);
        var result = await pipeline.EnrichAsync(findings);

        Assert.Single(result.ExecutedStages);
        Assert.Equal(5, result.ExecutedStages[0].SuccessCount);
    }

    [Fact]
    public async Task EnrichAsync_ReturnsMetrics()
    {
        var enrichers = new IFindingEnricher[]
        {
            new TestEnricher("Stage1"),
            new TestEnricher("Stage2", available: false),
        };

        var pipeline = new EnrichmentPipeline(enrichers);
        var findings = new[] { new Finding { RuleId = "GCI0001", RuleName = "Test", Summary = "Test", Evidence = "Test", WhyItMatters = "Test", SuggestedAction = "Test" } };

        var result = await pipeline.EnrichAsync(findings);

        Assert.Single(result.ExecutedStages);
        Assert.Equal("Stage1", result.ExecutedStages[0].StageName);
        Assert.Equal(1, result.ExecutedStages[0].SuccessCount);
        Assert.Single(result.SkippedStages);
        Assert.Contains("Stage2", result.SkippedStages);
    }
}
