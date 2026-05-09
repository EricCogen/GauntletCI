// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// Processes raw maintainer records through an LLM to extract expert "Scientific Facts",
/// embeds them, and upserts into the VectorStore.
/// </summary>
public sealed class Distillery
{
    private readonly ILlmEngine _llm;
    private readonly IEmbeddingEngine _embedding;
    private readonly VectorStore _store;

    /// <summary>Initializes the distillery with the LLM, embedding engine, and vector store to use.</summary>
    /// <param name="llm">LLM engine used to extract expert facts from raw discussion text.</param>
    /// <param name="embedding">Embedding engine used to convert extracted facts into float vectors.</param>
    /// <param name="store">Vector store where embedded facts are persisted.</param>
    public Distillery(ILlmEngine llm, IEmbeddingEngine embedding, VectorStore store)
    {
        _llm       = llm;
        _embedding = embedding;
        _store     = store;
    }

    /// <summary>
    /// Seeds the vector store with hand-curated expert facts using the configured embedding engine.
    /// Skips embedding if the engine is unavailable (NullEmbeddingEngine).
    /// Returns the number of facts successfully seeded.
    /// </summary>
    public async Task<int> SeedAsync(
        IEnumerable<SeedFact> facts, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var fact in facts)
        {
            ct.ThrowIfCancellationRequested();
            var embedding = await _embedding.EmbedAsync(fact.Content, ct).ConfigureAwait(false);
            if (embedding.Length == 0) continue;
            _store.Upsert(fact.Id, fact.Content, fact.Source, embedding);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Distils raw maintainer records into expert facts via LLM + embedding, sorted by reactions descending.
    /// Returns the number of records successfully processed and stored.
    /// </summary>
    public async Task<int> DistillAsync(
        IEnumerable<DistillationInput> inputs,
        int maxRecords = 50,
        CancellationToken ct = default)
    {
        var count = 0;
        // Process highest-engagement issues first: they're most likely to contain expert-level discussion and validated fixes worth extracting as facts
        var sorted = inputs.OrderByDescending(r => r.Reactions).Take(maxRecords);

        foreach (var input in sorted)
        {
            ct.ThrowIfCancellationRequested();

            var fact = await ExtractFactAsync(input, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fact)) continue;

            var embedding = await _embedding.EmbedAsync(fact, ct).ConfigureAwait(false);
            if (embedding.Length == 0) continue;

            _store.Upsert(input.Id, fact, input.Source, embedding);
            count++;
        }
        return count;
    }

    private async Task<string> ExtractFactAsync(DistillationInput input, CancellationToken ct)
    {
        try
        {
            var prompt = PromptTemplates.ExtractExpertFact(input.Title, input.Body);
            return await _llm.CompleteAsync(prompt, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[distillery] LLM error for {input.Id}: {ex.Message}");
            return string.Empty;
        }
    }
}

/// <summary>Normalized input for the distillery pipeline.</summary>
/// <param name="Id">Unique identifier for this record (e.g., issue URL or composite key).</param>
/// <param name="Title">Issue or PR title used to orient the LLM prompt.</param>
/// <param name="Body">Full text of the discussion body; long bodies are truncated inside the prompt template.</param>
/// <param name="Source">URL of the originating discussion, stored for provenance.</param>
/// <param name="Reactions">Total reaction count used to rank records by community engagement.</param>
public sealed record DistillationInput(
    string Id,
    string Title,
    string Body,
    string Source,
    int Reactions);
