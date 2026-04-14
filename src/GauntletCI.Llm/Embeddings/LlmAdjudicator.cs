// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// Queries the expert vector store for each finding and attaches the closest
/// matching expert fact as ExpertContext when the similarity score exceeds the threshold.
/// </summary>
public sealed class LlmAdjudicator
{
    private readonly IEmbeddingEngine _embedding;
    private readonly VectorStore _store;
    private readonly float _minScore;

    public LlmAdjudicator(IEmbeddingEngine embedding, VectorStore store, float minScore = 0.40f)
    {
        _embedding = embedding;
        _store     = store;
        _minScore  = minScore;
    }

    /// <summary>
    /// For each finding, embeds the rule context and searches the expert store.
    /// Attaches ExpertContext to findings where a match scores above the threshold.
    /// Silently skips if the embedding engine is unavailable or the store is empty.
    /// </summary>
    public async Task AdjudicateAsync(
        IEnumerable<Finding> findings,
        CancellationToken ct = default)
    {
        if (!_embedding.IsAvailable || _store.Count() == 0) return;

        foreach (var finding in findings)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var query     = BuildQuery(finding);
                var embedding = await _embedding.EmbedAsync(query, ct);
                if (embedding.Length == 0) continue;

                var results = _store.Search(embedding, topK: 1);
                if (results.Count == 0) continue;

                var top = results[0];
                if (top.Score >= _minScore)
                    finding.ExpertContext = new ExpertFact(top.Content, top.Source, top.Score);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[adjudicator] Error for {finding.RuleId}: {ex.Message}");
            }
        }
    }

    private static string BuildQuery(Finding finding) =>
        $"{finding.RuleName}: {finding.Summary}. {finding.WhyItMatters}";
}
