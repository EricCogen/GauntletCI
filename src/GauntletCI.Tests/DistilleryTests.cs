// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Llm;
using GauntletCI.Llm.Embeddings;
using Xunit;

namespace GauntletCI.Tests;

public sealed class DistilleryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly VectorStore _store;

    public DistilleryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"distillery-test-{Guid.NewGuid():N}.db");
        _store  = new VectorStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── SeedAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_WithEmbeddingEngine_UpsertsAllFacts()
    {
        var embedding  = new FakeEmbeddingEngine([1f, 0f]);
        var distillery = new Distillery(new NullLlmEngine(), embedding, _store);

        var facts = new[]
        {
            new SeedFact("id1", "HttpClient should be reused.", "https://github.com/dotnet/runtime/1"),
            new SeedFact("id2", "SemaphoreSlim preferred over lock in async.", "https://github.com/dotnet/runtime/2"),
        };

        var count = await distillery.SeedAsync(facts);

        Assert.Equal(2, count);
        Assert.Equal(2, _store.Count());
    }

    [Fact]
    public async Task SeedAsync_NullEmbeddingEngine_SkipsAll()
    {
        var distillery = new Distillery(new NullLlmEngine(), NullEmbeddingEngine.Instance, _store);
        var facts = new[] { new SeedFact("id1", "Some fact.", "source") };

        var count = await distillery.SeedAsync(facts);

        Assert.Equal(0, count);
        Assert.Equal(0, _store.Count());
    }

    // ── DistillAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DistillAsync_ValidInput_StoresExtractedFacts()
    {
        var llm        = new FakeLlmEngine("ValueTask must not be awaited twice.");
        var embedding  = new FakeEmbeddingEngine([0.5f, 0.5f]);
        var distillery = new Distillery(llm, embedding, _store);

        var inputs = new[]
        {
            new DistillationInput("dotnet/runtime#1:pr", "Title", "Body content", "https://github.com/dotnet/runtime/pull/1", 42),
        };

        var count = await distillery.DistillAsync(inputs, maxRecords: 10);

        Assert.Equal(1, count);
        Assert.Equal(1, _store.Count());
    }

    [Fact]
    public async Task DistillAsync_LlmReturnsEmpty_SkipsRecord()
    {
        var llm        = new FakeLlmEngine(string.Empty);
        var embedding  = new FakeEmbeddingEngine([1f, 0f]);
        var distillery = new Distillery(llm, embedding, _store);

        var inputs = new[]
        {
            new DistillationInput("dotnet/runtime#1:pr", "Title", "Body", "source", 10),
        };

        var count = await distillery.DistillAsync(inputs);

        Assert.Equal(0, count);
        Assert.Equal(0, _store.Count());
    }

    [Fact]
    public async Task DistillAsync_EmbeddingReturnsEmpty_SkipsRecord()
    {
        var llm        = new FakeLlmEngine("A real fact.");
        var embedding  = new FakeEmbeddingEngine([]); // empty → skip
        var distillery = new Distillery(llm, embedding, _store);

        var inputs = new[]
        {
            new DistillationInput("dotnet/runtime#1:pr", "Title", "Body", "source", 10),
        };

        var count = await distillery.DistillAsync(inputs);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DistillAsync_SortsByReactionsDescending()
    {
        var order = new List<string>();
        var llm   = new CapturingLlmEngine(id => { order.Add(id); return "fact"; });
        var embedding  = new FakeEmbeddingEngine([1f, 0f]);
        var distillery = new Distillery(llm, embedding, _store);

        var inputs = new[]
        {
            new DistillationInput("low",  "Low",  "body", "src", Reactions: 1),
            new DistillationInput("high", "High", "body", "src", Reactions: 99),
            new DistillationInput("mid",  "Mid",  "body", "src", Reactions: 42),
        };

        await distillery.DistillAsync(inputs, maxRecords: 10);

        Assert.Equal(["high", "mid", "low"], order);
    }

    [Fact]
    public async Task DistillAsync_RespectsMaxRecords()
    {
        var llm        = new FakeLlmEngine("fact");
        var embedding  = new FakeEmbeddingEngine([1f, 0f]);
        var distillery = new Distillery(llm, embedding, _store);

        var inputs = Enumerable.Range(0, 10)
            .Select(i => new DistillationInput($"id{i}", $"T{i}", "body", "src", i))
            .ToList();

        var count = await distillery.DistillAsync(inputs, maxRecords: 3);

        Assert.Equal(3, count);
    }

    // ── ExpertSeedFacts ───────────────────────────────────────────────────────

    [Fact]
    public void ExpertSeedFacts_HasTenFacts()
    {
        Assert.Equal(10, ExpertSeedFacts.All.Count);
    }

    [Fact]
    public void ExpertSeedFacts_AllHaveNonEmptyContent()
    {
        Assert.All(ExpertSeedFacts.All, f =>
        {
            Assert.NotEmpty(f.Id);
            Assert.NotEmpty(f.Content);
            Assert.NotEmpty(f.Source);
            Assert.StartsWith("https://", f.Source);
        });
    }

    [Fact]
    public void ExpertSeedFacts_AllIdsAreUnique()
    {
        var ids = ExpertSeedFacts.All.Select(f => f.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ── PromptTemplates.ExtractExpertFact ─────────────────────────────────────

    [Fact]
    public void ExtractExpertFact_TruncatesLongBody()
    {
        var longBody = new string('x', 3000);
        var prompt   = PromptTemplates.ExtractExpertFact("Title", longBody);
        Assert.Contains("…", prompt);
        Assert.True(prompt.Length < 3500, "Prompt should be well under full body length");
    }

    [Fact]
    public void ExtractExpertFact_IncludesTitleAndBody()
    {
        var prompt = PromptTemplates.ExtractExpertFact("My PR Title", "Some body text");
        Assert.Contains("My PR Title", prompt);
        Assert.Contains("Some body text", prompt);
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class FakeLlmEngine(string response) : ILlmEngine
    {
        public bool IsAvailable => true;
        public Task<string> EnrichFindingAsync(GauntletCI.Core.Model.Finding f, CancellationToken ct = default)
            => Task.FromResult(response);
        public Task<string> SummarizeReportAsync(IEnumerable<GauntletCI.Core.Model.Finding> findings, CancellationToken ct = default)
            => Task.FromResult(response);
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(response);
        public void Dispose() { }
    }

    private sealed class CapturingLlmEngine(Func<string, string> responder) : ILlmEngine
    {
        public bool IsAvailable => true;
        public Task<string> EnrichFindingAsync(GauntletCI.Core.Model.Finding f, CancellationToken ct = default)
            => Task.FromResult(responder(f.RuleId));
        public Task<string> SummarizeReportAsync(IEnumerable<GauntletCI.Core.Model.Finding> findings, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            var id = prompt.Contains("High") ? "high" :
                     prompt.Contains("Mid")  ? "mid"  :
                     prompt.Contains("Low")  ? "low"  : "unknown";
            return Task.FromResult(responder(id));
        }
        public void Dispose() { }
    }

    private sealed class FakeEmbeddingEngine(float[] vec) : IEmbeddingEngine
    {
        public bool IsAvailable => vec.Length > 0;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(vec);
    }
}
