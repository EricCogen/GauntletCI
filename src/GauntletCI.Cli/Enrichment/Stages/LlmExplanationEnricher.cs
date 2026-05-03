// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Cli.Enrichment.Stages;

/// <summary>
/// Enriches high-confidence findings with LLM-generated natural-language explanations.
/// Skips findings that already have LlmExplanation or are below High confidence threshold.
/// </summary>
public class LlmExplanationEnricher : IFindingEnricher
{
    private readonly ILlmEngine _engine;

    public string StageName => "LlmExplanation";
    public bool IsAvailable => _engine.IsAvailable;
    public IReadOnlySet<string> DependsOn => new HashSet<string>();  // No dependencies

    /// <summary>
    /// Creates an LLM enricher using the provided engine.
    /// Disables itself if the engine is unavailable.
    /// </summary>
    public LlmExplanationEnricher(ILlmEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Enriches the finding with an LLM-generated explanation if:
    /// - The finding is not already enriched with LlmExplanation
    /// - Confidence is High (skip Medium/Low to preserve budget)
    /// - LlmEngine is available
    /// </summary>
    public async Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
    {
        if (finding is null)
            return false;

        // Skip if already enriched
        if (!string.IsNullOrWhiteSpace(finding.LlmExplanation))
            return false;

        // Only enrich high-confidence findings to conserve LLM budget
        if (finding.Confidence != Confidence.High)
            return false;

        try
        {
            var explanation = await _engine.EnrichFindingAsync(finding, ct);
            if (!string.IsNullOrWhiteSpace(explanation))
            {
                finding.LlmExplanation = explanation;
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;  // Don't suppress cancellation
        }
        catch
        {
            // Don't propagate LLM errors; other enrichers should continue
            return false;
        }

        return false;
    }
}
