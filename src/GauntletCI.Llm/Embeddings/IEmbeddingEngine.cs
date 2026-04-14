// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

public interface IEmbeddingEngine
{
    bool IsAvailable { get; }

    /// <summary>Returns an embedding vector for the given text, or an empty array if unavailable.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
