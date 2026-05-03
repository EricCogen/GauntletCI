// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Extension methods for enriching findings after rule evaluation.
/// </summary>
public static class RuleOrchestratorExtensions
{
    /// <summary>
    /// Enriches all findings in an evaluation result using the provided enrichment pipeline.
    /// Skips any findings that cannot be enriched (e.g., timeout or error findings with no evidence).
    /// </summary>
    /// <param name="result">The evaluation result containing findings to enrich.</param>
    /// <param name="pipeline">The enrichment pipeline to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The same EvaluationResult with findings enriched in-place.</returns>
    public static async Task<EvaluationResult> EnrichAsync(
        this EvaluationResult result,
        EnrichmentPipeline pipeline,
        CancellationToken ct = default)
    {
        if (pipeline == null)
            throw new ArgumentNullException(nameof(pipeline));

        if (result?.Findings == null || result.Findings.Count == 0)
            return result;

        try
        {
            // Enrich all findings in the pipeline at once
            await pipeline.EnrichAsync(result.Findings, ct);
        }
        catch (OperationCanceledException)
        {
            throw;  // Propagate cancellation
        }
        catch (Exception ex)
        {
            // Log but don't throw - enrichment failures shouldn't break the analysis
            Console.Error.WriteLine($"[GauntletCI] Enrichment pipeline encountered an error: {ex.Message}");
        }

        return result;
    }
}

