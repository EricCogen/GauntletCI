// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Llm.Embeddings;
using Xunit;

namespace GauntletCI.Tests;

/// <summary>
/// End-to-end Phase 1 test: seed IDisposable / resource-lifecycle facts, fire the adjudicator
/// against a finding that represents an IDisposable misuse, and verify the returned citation
/// points to a real GitHub issue URL.
/// </summary>
public sealed class LlmIDisposableIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly VectorStore _store;

    // Fixed unit vector used by both seeding and querying (cosine similarity = 1.0).
    private static readonly float[] _vec = [1f, 0f, 0f];

    public LlmIDisposableIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"idisposable-test-{Guid.NewGuid():N}.db");
        _store  = new VectorStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEmbeddingEngine StaticEngine() => new FixedVectorEngine(_vec);

    private static Finding MakeIDisposableFinding() => new()
    {
        RuleId          = "GCI0021",
        RuleName        = "IDisposable Not Disposed",
        Summary         = "StreamReader opened without using statement may leak file handle",
        Evidence        = "Line 55: new StreamReader(path)",
        WhyItMatters    = "Undisposed IDisposable objects silently leak OS file handles",
        SuggestedAction = "Wrap in a using statement or call Dispose() in a finally block",
        Confidence      = Confidence.High,
    };

    // ── Phase 1: seed → adjudicate → citation check ───────────────────────────

    [Fact]
    public async Task Phase1_IDisposableMisuse_AdjudicatorAttachesGitHubCitation()
    {
        // Arrange: seed the IDisposable expert fact from ExpertSeedFacts
        var idisFact = ExpertSeedFacts.All.Single(f => f.Id.Contains("dotnet/runtime#issues/358"));
        _store.Upsert(idisFact.Id, idisFact.Content, idisFact.Source, _vec);

        var adjudicator = new LlmAdjudicator(StaticEngine(), _store, minScore: 0.0f);
        var finding     = MakeIDisposableFinding();

        // Act
        await adjudicator.AdjudicateAsync([finding]);

        // Assert: citation attached
        Assert.NotNull(finding.ExpertContext);

        // Pass/fail criterion: Source must point to a real GitHub issue
        Assert.StartsWith("https://github.com/", finding.ExpertContext!.Source);
        Assert.Contains("/issues/", finding.ExpertContext.Source);
    }

    [Fact]
    public async Task Phase1_ExpertContext_ContentDescribesIDisposableDisposal()
    {
        var idisFact = ExpertSeedFacts.All.Single(f => f.Id.Contains("dotnet/runtime#issues/358"));
        _store.Upsert(idisFact.Id, idisFact.Content, idisFact.Source, _vec);

        var adjudicator = new LlmAdjudicator(StaticEngine(), _store, minScore: 0.0f);
        var finding     = MakeIDisposableFinding();

        await adjudicator.AdjudicateAsync([finding]);

        Assert.NotNull(finding.ExpertContext);
        Assert.Contains("IDisposable", finding.ExpertContext!.Content);
        Assert.Contains("Dispose", finding.ExpertContext.Content);
    }

    [Fact]
    public async Task Phase1_SeedAll_ResourceLifecycleFacts_AreRetrievable()
    {
        // Seed all resource-lifecycle facts with the same fixed vector
        var resourceFacts = ExpertSeedFacts.All.Where(f =>
            f.Content.Contains("Dispose") || f.Content.Contains("disposed") ||
            f.Content.Contains("leak") || f.Content.Contains("IDisposable"));

        foreach (var fact in resourceFacts)
            _store.Upsert(fact.Id, fact.Content, fact.Source, _vec);

        Assert.True(_store.Count() >= 1, "At least one resource-lifecycle fact must be seeded");

        var adjudicator = new LlmAdjudicator(StaticEngine(), _store, minScore: 0.0f);
        var finding     = MakeIDisposableFinding();

        await adjudicator.AdjudicateAsync([finding]);

        Assert.NotNull(finding.ExpertContext);
        Assert.StartsWith("https://github.com/", finding.ExpertContext!.Source);
    }

    [Fact]
    public async Task Phase1_AllSeedFacts_HaveGitHubSource()
    {
        // Verify all 11 seed facts have proper GitHub citations
        Assert.All(ExpertSeedFacts.All, fact =>
        {
            Assert.StartsWith("https://github.com/", fact.Source);
            Assert.Contains("/issues/", fact.Source);
            Assert.NotEmpty(fact.Content);
            Assert.NotEmpty(fact.Id);
        });
    }

    [Fact]
    public async Task Phase1_IDisposableFinding_WithNoMatchingFact_ProducesNoContext()
    {
        // Store an unrelated fact with an orthogonal vector
        _store.Upsert("unrelated", "Some unrelated content.", "https://github.com/dotnet/runtime/issues/1", [0f, 1f, 0f]);

        // Query with a different vector → cosine = 0, below any threshold
        var engine      = new FixedVectorEngine([1f, 0f, 0f]);
        var adjudicator = new LlmAdjudicator(engine, _store, minScore: 0.5f);
        var finding     = MakeIDisposableFinding();

        await adjudicator.AdjudicateAsync([finding]);

        // dim mismatch (3 vs 3 but orthogonal) → score 0.0 → below threshold
        Assert.Null(finding.ExpertContext);
    }

    // ── Test double ───────────────────────────────────────────────────────────

    private sealed class FixedVectorEngine(float[] vec) : IEmbeddingEngine
    {
        public bool IsAvailable => true;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(vec);
    }
}
