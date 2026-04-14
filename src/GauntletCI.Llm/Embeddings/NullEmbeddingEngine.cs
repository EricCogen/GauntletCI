// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

/// <summary>No-op embedding engine used when no embedding backend is configured.</summary>
public sealed class NullEmbeddingEngine : IEmbeddingEngine
{
    public static readonly NullEmbeddingEngine Instance = new();
    public bool IsAvailable => false;
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<float>());
}
