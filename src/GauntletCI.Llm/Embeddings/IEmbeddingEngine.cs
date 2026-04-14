// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// Abstraction over embedding backends used to convert text into fixed-dimension float vectors.
/// Implementations should be thread-safe and inexpensive to call repeatedly.
/// </summary>
public interface IEmbeddingEngine
{
    /// <summary>Returns <see langword="true"/> when the backing model is loaded and able to produce embeddings.</summary>
    bool IsAvailable { get; }

    /// <summary>Returns an embedding vector for the given text, or an empty array if unavailable.</summary>
    /// <param name="text">The text to embed; callers should keep this under the model's context window.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
