// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Http;
using System.Text.Json;
using GauntletCI.Llm.Embeddings;
using Xunit;

namespace GauntletCI.Tests;

public sealed class VectorStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly VectorStore _store;

    public VectorStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gauntlet-test-{Guid.NewGuid():N}.db");
        _store  = new VectorStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── CosineSimilarity ──────────────────────────────────────────────────────

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var v = new float[] { 1f, 2f, 3f };
        Assert.Equal(1f, VectorStore.CosineSimilarity(v, v), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };
        Assert.Equal(0f, VectorStore.CosineSimilarity(a, b), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { -1f, 0f };
        Assert.Equal(-1f, VectorStore.CosineSimilarity(a, b), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        var a = new float[] { 0f, 0f };
        var b = new float[] { 1f, 1f };
        Assert.Equal(0f, VectorStore.CosineSimilarity(a, b));
    }

    [Fact]
    public void CosineSimilarity_EmptyArrays_ReturnsZero()
    {
        Assert.Equal(0f, VectorStore.CosineSimilarity([], []));
    }

    // ── FloatsToBytes / BytesToFloats round-trip ──────────────────────────────

    [Fact]
    public void FloatsBytesRoundTrip_PreservesValues()
    {
        var orig  = new float[] { 1.5f, -0.3f, 42.0f, float.Epsilon };
        var bytes = VectorStore.FloatsToBytes(orig);
        var back  = VectorStore.BytesToFloats(bytes);
        Assert.Equal(orig, back);
    }

    // ── VectorStore CRUD ──────────────────────────────────────────────────────

    [Fact]
    public void Upsert_ThenCount_ReturnsOne()
    {
        _store.Upsert("rec1", "hello world", "test", [1f, 0f, 0f]);
        Assert.Equal(1, _store.Count());
    }

    [Fact]
    public void Upsert_DuplicateId_UpdatesRecord()
    {
        _store.Upsert("rec1", "original", "test", [1f, 0f]);
        _store.Upsert("rec1", "updated",  "test", [0f, 1f]);
        Assert.Equal(1, _store.Count());

        var result = _store.Search([0f, 1f], topK: 1);
        Assert.Single(result);
        Assert.Equal("updated", result[0].Content);
    }

    [Fact]
    public void Search_ReturnsClosestVector()
    {
        _store.Upsert("a", "performance tip",   "dotnet/runtime", [1f, 0f, 0f]);
        _store.Upsert("b", "memory allocation", "dotnet/runtime", [0f, 1f, 0f]);
        _store.Upsert("c", "thread safety",     "dotnet/runtime", [0f, 0f, 1f]);

        var results = _store.Search([1f, 0f, 0f], topK: 1);

        Assert.Single(results);
        Assert.Equal("a", results[0].Id);
        Assert.Equal(1f, results[0].Score, precision: 5);
    }

    [Fact]
    public void Search_TopK_LimitsResults()
    {
        for (var i = 0; i < 10; i++)
            _store.Upsert($"rec{i}", $"content {i}", "test", [MathF.Cos(i), MathF.Sin(i)]);

        var results = _store.Search([1f, 0f], topK: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_EmptyStore_ReturnsEmpty()
    {
        var results = _store.Search([1f, 0f, 0f], topK: 5);
        Assert.Empty(results);
    }

    [Fact]
    public void Search_EmptyQueryVector_ReturnsEmpty()
    {
        _store.Upsert("a", "something", "test", [1f, 0f]);
        var results = _store.Search([], topK: 5);
        Assert.Empty(results);
    }

    [Fact]
    public void Search_DifferentDimVectorsIgnored()
    {
        _store.Upsert("2d", "two dim",   "test", [1f, 0f]);
        _store.Upsert("3d", "three dim", "test", [1f, 0f, 0f]);

        // Query with 2D vector — 3D record should be ignored (dim mismatch)
        var results = _store.Search([1f, 0f], topK: 5);
        Assert.Single(results);
        Assert.Equal("2d", results[0].Id);
    }

    // ── NullEmbeddingEngine ───────────────────────────────────────────────────

    [Fact]
    public async Task NullEmbeddingEngine_IsNotAvailable()
    {
        Assert.False(NullEmbeddingEngine.Instance.IsAvailable);
    }

    [Fact]
    public async Task NullEmbeddingEngine_ReturnsEmptyArray()
    {
        var result = await NullEmbeddingEngine.Instance.EmbedAsync("hello");
        Assert.Empty(result);
    }

    // ── OllamaEmbeddingEngine ─────────────────────────────────────────────────

    [Fact]
    public async Task OllamaEmbeddingEngine_ParsesEmbeddingFromResponse()
    {
        var fakeResponse = JsonSerializer.Serialize(new { embedding = new float[] { 0.1f, 0.2f, 0.3f } });
        var handler = new FakeEmbedHandler(fakeResponse);
        var http    = new HttpClient(handler);

        using var engine = new OllamaEmbeddingEngine("nomic-embed-text", "http://localhost:11434", http);
        var result = await engine.EmbedAsync("test input");

        Assert.Equal(3, result.Length);
        Assert.Equal(0.1f, result[0], precision: 5);
    }

    [Fact]
    public async Task OllamaEmbeddingEngine_IsAvailable()
    {
        var handler = new FakeEmbedHandler(JsonSerializer.Serialize(new { embedding = new float[] { 1f } }));
        using var engine = new OllamaEmbeddingEngine("model", "http://localhost:11434", new HttpClient(handler));
        Assert.True(engine.IsAvailable);
    }

    [Fact]
    public async Task OllamaEmbeddingEngine_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        var handler = new ErrorHandler(HttpStatusCode.InternalServerError);
        using var engine = new OllamaEmbeddingEngine("model", "http://localhost:11434", new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => engine.EmbedAsync("test"));
    }

    [Fact]
    public async Task OllamaEmbeddingEngine_NullEmbeddingInResponse_ReturnsEmptyArray()
    {
        // Response with null embedding field
        var fakeResponse = JsonSerializer.Serialize(new { embedding = (float[]?)null });
        var handler = new FakeEmbedHandler(fakeResponse);
        using var engine = new OllamaEmbeddingEngine("model", "http://localhost:11434", new HttpClient(handler));

        var result = await engine.EmbedAsync("test");

        Assert.Empty(result);
    }

    [Fact]
    public async Task OllamaEmbeddingEngine_MissingEmbeddingField_ReturnsEmptyArray()
    {
        var fakeResponse = "{}";
        var handler = new FakeEmbedHandler(fakeResponse);
        using var engine = new OllamaEmbeddingEngine("model", "http://localhost:11434", new HttpClient(handler));

        var result = await engine.EmbedAsync("test");

        Assert.Empty(result);
    }

    [Fact]
    public void CosineSimilarity_MismatchedLengths_ReturnsZero()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 1f, 0f };
        Assert.Equal(0f, VectorStore.CosineSimilarity(a, b));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gauntlet-test-{Guid.NewGuid():N}.db");
        var store = new VectorStore(dbPath);
        store.Dispose();

        var ex = Record.Exception(() => store.Dispose());

        Assert.Null(ex);
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    [Fact]
    public void Search_HighDimensionalVectors_ReturnsCorrectResults()
    {
        // Create 128-dim vectors (typical embedding dimension)
        var vec1 = new float[128];
        var vec2 = new float[128];
        vec1[0] = 1f;
        vec2[127] = 1f;

        _store.Upsert("first", "content 1", "source", vec1);
        _store.Upsert("second", "content 2", "source", vec2);

        var query = new float[128];
        query[0] = 1f;

        var results = _store.Search(query, topK: 1);

        Assert.Single(results);
        Assert.Equal("first", results[0].Id);
    }

    private sealed class FakeEmbedHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class ErrorHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("Error", System.Text.Encoding.UTF8, "text/plain")
            });
    }
}
