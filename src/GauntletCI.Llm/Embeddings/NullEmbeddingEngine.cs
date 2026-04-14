// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

/// <summary>No-op embedding engine used when no embedding backend is configured.</summary>
public sealed class NullEmbeddingEngine : IEmbeddingEngine
{
    /// <summary>Shared singleton to avoid allocating a new instance per call site.</summary>
    public static readonly NullEmbeddingEngine Instance = new();

    /// <summary>Always <see langword="false"/>; this engine produces no embeddings.</summary>
    public bool IsAvailable => false;

    /// <summary>Returns an empty array without calling any model.</summary>
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<float>());
}
